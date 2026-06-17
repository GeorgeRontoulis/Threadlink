namespace Threadlink.Netcode
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Threadlink.Core.NativeSubsystems.Chronos;
    using Threadlink.Core.NativeSubsystems.Iris;
    using Threadlink.Deterministic;
    using Threadlink.ECS;
    using Threadlink.Utilities.ECS;
    using Threadlink.Utilities.Objects;
    using Unity.Collections;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using Utilities.Collections;

    [RequireComponent(typeof(Animator))]
    public sealed class NetworkPlayableAnimator : UnityNetworkBridge<NetworkAnimationSubsystem, DeterministicPlayableState>, IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PortData
        {
            public int Hash;
            public float StartWeight;
            public float TargetWeight;
            public float StartNormTime;
            public float TargetNormTime;
        }

        private const int MAX_SYNCED_CLIPS = DeterministicPlayableState.MAX_SYNCED_CLIPS; // Must match fixed-array size in the payload.

        // This should roughly match (1f / NetworkTickRateInSeconds). 
        // 15f implies a full transition blend takes ~66ms, perfect for 15-30Hz ticks.
        private const float INTERPOLATION_SPEED = 15f;
        private const double HEARTBEAT_INTERVAL = 0.25d; // Send full state every 500ms

        private List<AnimatorClipInfo> ClipInfoBuffer { get; set; }

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ReadOnly]
#endif
        [SerializeField] private Animator animator;

        // Host State
        private DeterministicPlayableState outgoingStateCache = default;
        private DeterministicPlayableState lastSentState = default;

        // Client State
        private PlayableGraph clientGraph;
        private AnimationMixerPlayable clientMixer;
        private float interpolationTimer = 0f;
        private double lastHeartbeatTime = 0f;

        private NativeArray<PortData> mixerPorts = default;

        protected override void OnValidate()
        {
            base.OnValidate();
            this.Set(ref animator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (clientGraph.IsValid())
                clientGraph.Destroy();

            mixerPorts.DisposeSafely();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            Dispose();
            base.Discard();
        }

        public void PreparePlayableGraph()
        {
            mixerPorts = new(MAX_SYNCED_CLIPS, Allocator.Persistent);
            CacheAndRegisterClips();

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;

            clientGraph = PlayableGraph.Create($"NetworkClientGraph_{name}");
            clientGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            this.PreventEditorMemoryLeaks();

            var output = AnimationPlayableOutput.Create(clientGraph, "NetworkAnimOutput", animator);
            clientMixer = AnimationMixerPlayable.Create(clientGraph, MAX_SYNCED_CLIPS);
            output.SetSourcePlayable(clientMixer);

            clientGraph.Play();
        }

        private void CacheAndRegisterClips()
        {
            if (!NetworkClipLibrary.TryGetSingleton(out var library))
                return;

            var controller = animator.runtimeAnimatorController;

            if (controller == null || controller.animationClips == null)
                return;

            int length = controller.animationClips.Length;

            for (int i = 0; i < length; i++)
            {
                var clip = controller.animationClips[i];

                if (clip != null)
                    library.RegisterClip(Animator.StringToHash(clip.name), clip);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StartLateUpdate()
        {
            ClipInfoBuffer ??= new(animator.runtimeAnimatorController.animationClips.Length);
            Iris.Subscribe<Action>(Shared.ThreadlinkIDs.Iris.Events.OnLateUpdate, OnLateUpdate);
        }

        private void OnLateUpdate()
        {
            outgoingStateCache = default;
            int clipIndex = 0;
            int layerCount = animator.layerCount;

            for (int layer = 0; layer < layerCount; layer++)
            {
                float layerWeight = layer == 0 ? 1f : animator.GetLayerWeight(layer);

                if (layerWeight <= 0.001f)
                    continue;

                animator.GetCurrentAnimatorClipInfo(layer, ClipInfoBuffer);
                var currentState = animator.GetCurrentAnimatorStateInfo(layer);

                ExtractList(ClipInfoBuffer, currentState, layerWeight, ref outgoingStateCache, ref clipIndex);
                ClipInfoBuffer.Clear();

                if (animator.IsInTransition(layer))
                {
                    animator.GetNextAnimatorClipInfo(layer, ClipInfoBuffer);
                    var nextState = animator.GetNextAnimatorStateInfo(layer);
                    ExtractList(ClipInfoBuffer, nextState, layerWeight, ref outgoingStateCache, ref clipIndex);
                    ClipInfoBuffer.Clear();
                }
            }
        }

        private unsafe void ExtractList(List<AnimatorClipInfo> clipInfos, AnimatorStateInfo stateInfo,
        float layerWeight, ref DeterministicPlayableState state, ref int index)
        {
            int length = clipInfos.Count;
            for (int i = 0; i < length; i++)
            {
                if (index >= MAX_SYNCED_CLIPS)
                    return;

                var info = clipInfos[i];
                float finalWeight = info.weight * layerWeight;

                if (finalWeight > 0.001f)
                {
                    var clip = info.clip;
                    state.ActiveClipHashes[index] = Animator.StringToHash(clip.name);
                    state.RawClipWeights[index] = ((DFP)finalWeight).RawValue;

                    float rawTime = stateInfo.normalizedTime;
                    float safeNormTime = clip.isLooping ? Mathf.Repeat(rawTime, 1f) : Mathf.Clamp01(rawTime);
                    state.RawClipNormalizedTimes[index] = ((DFP)safeNormTime).RawValue;

                    ++index;
                }
            }
        }

        protected internal unsafe override bool TryGetOutgoingState(out DeterministicPlayableState state)
        {
            state = outgoingStateCache;
            bool isDirty = false;

            fixed (DeterministicPlayableState* currentStatePtr = &state)
            fixed (DeterministicPlayableState* lastStatePtr = &lastSentState)
            {
                var currentBytes = (byte*)currentStatePtr;
                var lastBytes = (byte*)lastStatePtr;
                int size = sizeof(DeterministicPlayableState);

                for (int i = 0; i < size; i++)
                {
                    if (currentBytes[i] != lastBytes[i])
                    {
                        isDirty = true;
                        break;
                    }
                }
            }

            var currentTime = Time.timeAsDouble;

            if (!isDirty && (currentTime - lastHeartbeatTime >= HEARTBEAT_INTERVAL))
                isDirty = true;

            if (isDirty)
            {
                lastSentState = state;
                lastHeartbeatTime = currentTime;
                return true;
            }

            return false;
        }

        protected internal unsafe override void ApplyNetworkStateToUnity(in Entity entity, in DeterministicPlayableState receivedState)
        {
            if (!NetworkClipLibrary.TryGetSingleton(out var library))
            {
                Debug.LogWarning("Network Clip Library not found.");
                return;
            }

            if (entity != linkedNetworkedEntity || (int)(receivedState.NetworkTick - lastValidNetworkTick) <= 0)
            {
                Debug.LogWarning("Entity or Packet mismatch detected. Packet dropped.");
                return;
            }

            lastValidNetworkTick = receivedState.NetworkTick;

            if (!clientGraph.IsValid())
            {
                Debug.LogWarning("Invalid graph!");
                return;
            }

            float currentAlpha = Mathf.Clamp01(interpolationTimer);

            for (int p = 0; p < MAX_SYNCED_CLIPS; p++)
            {
                var snap = mixerPorts[p];
                if (snap.Hash == 0) continue;

                snap.StartWeight = Mathf.Lerp(snap.StartWeight, snap.TargetWeight, currentAlpha);

                float st = snap.StartNormTime;
                float tt = snap.TargetNormTime;

                bool isLooping = library.TryGetClip(snap.Hash, out var clip) && clip.isLooping;

                if (isLooping && tt < st - 0.5f)
                    tt += 1f;

                snap.StartNormTime = Mathf.Lerp(st, tt, currentAlpha) % 1f;
                mixerPorts[p] = snap;
            }

            for (int i = 0; i < MAX_SYNCED_CLIPS; i++)
            {
                int hash = receivedState.ActiveClipHashes[i];

                if (hash == 0)
                    continue;

                int portIndex = -1;

                for (int p = 0; p < MAX_SYNCED_CLIPS; p++)
                {
                    if (mixerPorts[p].Hash == hash)
                    {
                        portIndex = p;
                        break;
                    }
                }

                if (portIndex == -1)
                {
                    float lowestWeight = float.MaxValue;
                    for (int p = 0; p < MAX_SYNCED_CLIPS; p++)
                    {
                        var port = mixerPorts[p];

                        if (port.Hash == 0)
                        {
                            portIndex = p;
                            break;
                        }

                        if (port.TargetWeight < lowestWeight)
                        {
                            lowestWeight = port.TargetWeight;
                            portIndex = p;
                        }
                    }

                    if (portIndex == -1)
                        continue;

                    if (library.TryGetClip(hash, out var clip))
                    {
                        // Disconnect old playable on this port if present
                        var oldInput = clientMixer.GetInput(portIndex);
                        if (oldInput.IsValid())
                        {
                            clientGraph.Disconnect(clientMixer, portIndex);
                            clientGraph.DestroyPlayable(oldInput);
                        }

                        var newClipPlayable = AnimationClipPlayable.Create(clientGraph, clip);
                        newClipPlayable.SetApplyFootIK(true);
                        newClipPlayable.SetApplyPlayableIK(true);
                        newClipPlayable.Pause();
                        clientGraph.Connect(newClipPlayable, 0, clientMixer, portIndex);

                        // Initialize new port. THIS IS THE ONLY TIME WE RESET START VALUES!
                        var data = mixerPorts[portIndex];
                        data.Hash = hash;
                        data.StartWeight = 0f;
                        data.StartNormTime = 0f;//(float)DFP.FromRaw(receivedState.RawClipNormalizedTimes[i]);
                        mixerPorts[portIndex] = data;
                    }
                    else continue;
                }

                var update = mixerPorts[portIndex];
                update.TargetWeight = (float)DFP.FromRaw(receivedState.RawClipWeights[i]);
                update.TargetNormTime = (float)DFP.FromRaw(receivedState.RawClipNormalizedTimes[i]);
                mixerPorts[portIndex] = update;
            }

            for (int p = 0; p < MAX_SYNCED_CLIPS; p++)
            {
                bool foundInPacket = false;
                var port = mixerPorts[p];
                for (int i = 0; i < MAX_SYNCED_CLIPS; i++)
                {
                    if (receivedState.ActiveClipHashes[i] == port.Hash)
                    {
                        foundInPacket = true;
                        break;
                    }
                }

                if (!foundInPacket && port.Hash != 0)
                {
                    port.TargetWeight = 0f;
                    mixerPorts[p] = port;
                }
            }

            interpolationTimer = 0f;
        }

        public unsafe void UpdateAnimator()
        {
            if (!NetworkClipLibrary.TryGetSingleton(out var library) || !clientGraph.IsValid())
                return;

            interpolationTimer += Chronos.DeltaTime * INTERPOLATION_SPEED;
            float alpha = Mathf.Clamp01(interpolationTimer);

            for (int p = 0; p < MAX_SYNCED_CLIPS; p++)
            {
                var port = mixerPorts[p];

                if (port.Hash == 0)
                    continue;

                float currentWeight = Mathf.Lerp(port.StartWeight, port.TargetWeight, alpha);
                clientMixer.SetInputWeight(p, currentWeight);

                if (currentWeight <= 0.001f && port.TargetWeight <= 0.001f)
                {
                    port.Hash = 0;
                    port.StartWeight = 0f;
                    port.TargetWeight = 0f;
                    port.StartNormTime = 0f;
                    port.TargetNormTime = 0f;
                    mixerPorts[p] = port;
                    continue;
                }

                if (currentWeight > 0.001f)
                {
                    var input = clientMixer.GetInput(p);
                    if (input.IsValid() && library.TryGetClip(port.Hash, out var clip))
                    {
                        float t1 = port.StartNormTime;
                        float t2 = port.TargetNormTime;

                        if (clip.isLooping && t2 < t1 - 0.5f) t2 += 1f;

                        float targetNormTime = Mathf.Lerp(t1, t2, alpha);

                        if (clip.isLooping)
                            targetNormTime %= 1f;
                        else
                            targetNormTime = Mathf.Clamp01(targetNormTime);

                        float finalTime = targetNormTime * clip.length;
                        ((AnimationClipPlayable)input).SetTime(finalTime);
                    }
                }
            }

            clientGraph.Evaluate();
            animator.Update(0f);
        }
    }
}