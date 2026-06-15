namespace Threadlink.Netcode
{
    using Core.NativeSubsystems.Iris;
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.ECS;
    using Threadlink.Shared;
    using UnityEngine;

    public partial class Netrunner
    {
        public delegate void FlowEventResolutionDelegate(in FlowEvent flowEvent);

        private const ThreadlinkIDs.Iris.Events NETWORK_TICK_EVENT = ThreadlinkIDs.Iris.Events.OnUpdate;
        public const byte TICK_RATE = 30; // 30 TPS

        public uint CurrentTick { get; internal set; }

        private readonly double TickInterval = 1d / TICK_RATE;
        private RingBuffer<FlowEvent> sessionFlowEventsBuffer = default;
        private double tickAccumulator = 0d;
        private double lastUpdateTime = 0d;

        public event FlowEventResolutionDelegate OnFlowEventFired = null;
        public event Action OnNetworkTick = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BootNetworkUpdateLoop()
        {
            lastUpdateTime = Time.realtimeSinceStartupAsDouble;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StartNetworkUpdateLoop() => Iris.Subscribe<Action>(NETWORK_TICK_EVENT, TickNetwork);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopNetworkUpdateLoop() => Iris.Unsubscribe<Action>(NETWORK_TICK_EVENT, TickNetwork);

        private void TickNetwork()
        {
            double currentTime = Time.realtimeSinceStartupAsDouble;
            double deltaTime = currentTime - lastUpdateTime;

            lastUpdateTime = currentTime;
            tickAccumulator += deltaTime;

            transport?.RunCallbacks();

            while (tickAccumulator >= TickInterval)
            {
                tickAccumulator -= TickInterval;
                ++CurrentTick;

                while (sessionFlowEventsBuffer.TryPop(out var flowEvent))
                    OnFlowEventFired?.Invoke(flowEvent);

                ReceiveData();
                OnNetworkTick?.Invoke();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushFlowEvent(FlowEvent.Tag tag, int playerIndex)
        {
            if (sessionFlowEventsBuffer.IsCreated)
                sessionFlowEventsBuffer.Push(new FlowEvent(tag, playerIndex));
        }
    }
}
