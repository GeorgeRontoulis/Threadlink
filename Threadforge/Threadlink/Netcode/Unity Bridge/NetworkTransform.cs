namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using Threadlink.Core.NativeSubsystems.Chronos;
    using Threadlink.Deterministic;
    using Threadlink.ECS;
    using Threadlink.Utilities.ECS;
    using UnityEngine;

    public sealed class NetworkTransform : UnityNetworkBridge<NetworkTransformSubsystem, DeterministicTransform>
    {
        private static readonly DFP PosAndScaleThreshold = (DFP)0.0001f;
        private const float INTERPOLATION_SPEED = 7f;

        private Vector3 targetNetworkPosition = default;
        private Quaternion targetNetworkRotation = Quaternion.identity;
        private Vector3 targetNetworkScale = default;

        private Vector3 lastSentPosition = default;
        private Quaternion lastSentRotation = Quaternion.identity;
        private Vector3 lastSentScale = default;

        public override void Bind(in Entity entity, bool belongsToHost)
        {
            targetNetworkPosition = cachedTransform.localPosition;
            targetNetworkRotation = cachedTransform.localRotation;
            targetNetworkScale = cachedTransform.localScale;

            lastSentPosition = targetNetworkPosition;
            lastSentRotation = targetNetworkRotation;
            lastSentScale = targetNetworkScale;
            base.Bind(entity, belongsToHost);
        }

        protected override bool TryGetOutgoingState(out DeterministicTransform state)
        {
            cachedTransform.GetLocalPositionAndRotation(out var pos, out var rot);
            var scale = cachedTransform.localScale;

            var angle = Quaternion.Angle(rot, lastSentRotation);
            var sqrMagnitude = Vector3.SqrMagnitude(pos - lastSentPosition);

            bool positionChanged = (DFP)sqrMagnitude > PosAndScaleThreshold;
            bool rotationChanged = (DFP)angle > DFP.One;

            sqrMagnitude = Vector3.SqrMagnitude(scale - lastSentScale);
            bool scaleChanged = (DFP)sqrMagnitude > PosAndScaleThreshold;

            if (positionChanged || rotationChanged || scaleChanged)
            {
                lastSentPosition = pos;
                lastSentRotation = rot;
                lastSentScale = scale;

                state = new DeterministicTransform
                {
                    rawPositionX = ((DFP)pos.x).RawValue,
                    rawPositionY = ((DFP)pos.y).RawValue,
                    rawPositionZ = ((DFP)pos.z).RawValue,

                    rawRotationX = ((DFP)rot.x).RawValue,
                    rawRotationY = ((DFP)rot.y).RawValue,
                    rawRotationZ = ((DFP)rot.z).RawValue,
                    rawRotationW = ((DFP)rot.w).RawValue,

                    rawScaleX = ((DFP)scale.x).RawValue,
                    rawScaleY = ((DFP)scale.y).RawValue,
                    rawScaleZ = ((DFP)scale.z).RawValue
                };

                return true;
            }

            state = default;
            return false;
        }

        /// <summary>
        /// We stagger the state application process by only applying the changes in local memory.
        /// <see cref="UpdateTransform"/> can then be used to smoothly interpolate to the new position.
        /// This ensures smooth movement regardless of <see cref="Netrunner"/>'s Tick Rate.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="receivedState"></param>
        protected override void ApplyNetworkStateToUnity(Entity entity, DeterministicTransform receivedState)
        {
            if (entity != linkedNetworkedEntity || (int)(receivedState.NetworkTick - lastValidNetworkTick) < 0)
                return;

            lastValidNetworkTick = receivedState.NetworkTick;

            targetNetworkPosition.x = (float)DFP.FromRaw(receivedState.rawPositionX);
            targetNetworkPosition.y = (float)DFP.FromRaw(receivedState.rawPositionY);
            targetNetworkPosition.z = (float)DFP.FromRaw(receivedState.rawPositionZ);

            targetNetworkRotation.x = (float)DFP.FromRaw(receivedState.rawRotationX);
            targetNetworkRotation.y = (float)DFP.FromRaw(receivedState.rawRotationY);
            targetNetworkRotation.z = (float)DFP.FromRaw(receivedState.rawRotationZ);
            targetNetworkRotation.w = (float)DFP.FromRaw(receivedState.rawRotationW);

            targetNetworkScale.x = (float)DFP.FromRaw(receivedState.rawScaleX);
            targetNetworkScale.y = (float)DFP.FromRaw(receivedState.rawScaleY);
            targetNetworkScale.z = (float)DFP.FromRaw(receivedState.rawScaleZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateTransform()
        {
            if (!linkedNetworkedEntity.IsValid())
                return;

            cachedTransform.GetLocalPositionAndRotation(out var pos, out var rot);

            var time = Chronos.DeltaTime * INTERPOLATION_SPEED;

            var interpolatedPosition = Vector3.Lerp(pos, targetNetworkPosition, time);
            var interpolatedRotation = Quaternion.Lerp(rot, targetNetworkRotation, time);

            cachedTransform.SetLocalPositionAndRotation(interpolatedPosition, interpolatedRotation);

            cachedTransform.localScale = Vector3.Lerp(cachedTransform.localScale, targetNetworkScale, time);
        }
    }
}