namespace Threadlink.User
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Core.NativeSubsystems.Chronos;
    using Threadlink.Deterministic;
    using Threadlink.ECS;
    using Threadlink.Netcode;
    using Threadlink.Utilities.ECS;
    using Threadlink.Utilities.Objects;
    using Unity.Collections;
    using UnityEngine;

    [RequireComponent(typeof(Animator))]
    public sealed class NetworkAnimator : UnityNetworkBridge<NetworkAnimationSubsystem, DeterministicAnimatorState>, IDisposable
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ReadOnly]
#endif
        [SerializeField] private Animator animator;

        private NativeArray<int> floatHashes;
        private NativeArray<int> intHashes;
        private NativeArray<int> boolHashes;
        private NativeArray<int> triggerHashes;
        private NativeArray<byte> localTriggerCounters;

        protected override void OnValidate()
        {
            base.OnValidate();
            this.Set(ref animator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            floatHashes.DisposeSafely();
            intHashes.DisposeSafely();
            boolHashes.DisposeSafely();
            triggerHashes.DisposeSafely();
            localTriggerCounters.DisposeSafely();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            Dispose();
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Bind(in Entity entity, bool belongsToHost)
        {
            CacheAnimatorParameters();
            base.Bind(entity, belongsToHost);
        }

        /// <summary>
        /// Gameplay systems must call this instead of <see cref="Animator.SetTrigger(int)"/>.
        /// </summary>
        public void SetTrigger(int triggerHash)
        {
            int length = triggerHashes.Length;
            for (int i = 0; i < length; i++)
            {
                var hash = triggerHashes[i];

                if (hash == 0)
                {
                    return;
                }
                else if (hash == triggerHash)
                {
                    // Increment the counter. It will naturally wrap around 255 -> 0.
                    localTriggerCounters[i]++;

                    // Apply locally immediately for responsiveness
                    animator.SetTrigger(triggerHash);
                    return;
                }
            }
        }

        /// <summary>
        /// Gameplay systems must call this instead of <see cref="Animator.ResetTrigger(int)"/>.
        /// </summary>
        public void ResetTrigger(int triggerHash)
        {
            int length = triggerHashes.Length;
            for (int i = 0; i < length; i++)
            {
                var hash = triggerHashes[i];

                if (hash == 0)
                {
                    return;
                }
                else if (hash == triggerHash)
                {
                    // Decrement the counter. Wrap-around 0 -> 255 is expected.
                    localTriggerCounters[i]--;

                    // Apply locally immediately
                    animator.ResetTrigger(triggerHash);
                    return;
                }
            }
        }

        private void CacheAnimatorParameters()
        {
            floatHashes = new(4, Allocator.Persistent);
            intHashes = new(4, Allocator.Persistent);
            boolHashes = new(32, Allocator.Persistent);
            triggerHashes = new(8, Allocator.Persistent);
            localTriggerCounters = new(8, Allocator.Persistent);

            this.GuardAgainstEditorMemoryLeaks();

            int fCount = 0, iCount = 0, bCount = 0, tCount = 0;

            foreach (var param in animator.parameters)
            {
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Trigger:
                        if (tCount < 8)
                            triggerHashes[tCount++] = param.nameHash;
                        break;
                    case AnimatorControllerParameterType.Float:
                        if (fCount < 4) floatHashes[fCount++] = param.nameHash;
                        break;
                    case AnimatorControllerParameterType.Int:
                        if (iCount < 4) intHashes[iCount++] = param.nameHash;
                        break;
                    case AnimatorControllerParameterType.Bool:
                        if (bCount < 32) boolHashes[bCount++] = param.nameHash;
                        break;
                }
            }
        }

        protected unsafe override bool TryGetOutgoingState(out DeterministicAnimatorState state)
        {
            state = default;

            // 1. Pack Bools into Bitmask
            int length = boolHashes.Length;
            for (int i = 0; i < length; i++)
            {
                if (boolHashes[i] == 0)
                    break;

                if (animator.GetBool(boolHashes[i]))
                    state.boolMask |= (1 << i);
            }

            // 2. Pack fixed arrays
            length = floatHashes.Length;
            for (int i = 0; i < length; i++)
            {
                if (floatHashes[i] == 0)
                    break;

                var floatValue = animator.GetFloat(floatHashes[i]);
                state.rawDFPs[i] = ((DFP)floatValue).RawValue;
            }

            length = intHashes.Length;
            for (int i = 0; i < intHashes.Length; i++)
            {
                if (intHashes[i] == 0)
                    break;

                state.integers[i] = animator.GetInteger(intHashes[i]);
            }

            length = triggerHashes.Length;
            for (int i = 0; i < triggerHashes.Length; i++)
            {
                if (triggerHashes[i] == 0)
                    break;

                state.TriggerCounters[i] = localTriggerCounters[i];
            }

            // Note: Should probably add a dirty-check here similar to the Vector3 distance check 
            // in NetworkTransform to prevent spamming the network if parameters haven't changed.
            return true;
        }

        protected unsafe override void ApplyNetworkStateToUnity(Entity entity, DeterministicAnimatorState receivedState)
        {
            if (entity != linkedNetworkedEntity || (int)(receivedState.NetworkTick - lastValidNetworkTick) < 0)
                return;

            // 2. Unpack Bools
            int length = boolHashes.Length;
            for (int i = 0; i < length; i++)
            {
                if (boolHashes[i] == 0)
                    break;

                bool value = (receivedState.boolMask & (1 << i)) != 0;
                animator.SetBool(boolHashes[i], value);
            }

            // 3. Unpack Arrays
            length = floatHashes.Length;
            for (int i = 0; i < length; i++)
            {
                if (floatHashes[i] == 0)
                    break;

                animator.SetFloat(floatHashes[i], (float)DFP.FromRaw(receivedState.rawDFPs[i]), 0.15f, Chronos.DeltaTime);
            }

            length = intHashes.Length;
            for (int i = 0; i < length; i++)
            {
                if (intHashes[i] == 0)
                    break;

                animator.SetInteger(intHashes[i], receivedState.integers[i]);
            }

            length = triggerHashes.Length;
            for (int i = 0; i < length; i++)
            {
                int hash = triggerHashes[i];

                if (hash == 0)
                    break;

                var triggetCounter = receivedState.TriggerCounters[i];

                // Sequence math handles byte wrap-around natively in both directions
                var delta = (sbyte)(triggetCounter - localTriggerCounters[i]);

                if (delta > 0)
                    animator.SetTrigger(hash);
                else if (delta < 0)
                    animator.ResetTrigger(hash);

                // Always synchronize the local counter to the authoritative state
                localTriggerCounters[i] = triggetCounter;
            }
        }
    }
}