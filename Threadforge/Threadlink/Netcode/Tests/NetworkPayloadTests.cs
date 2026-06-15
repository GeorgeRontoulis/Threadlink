namespace Threadlink.Tests.Netcode
{
    using NUnit.Framework;
    using System;
    using Threadlink.Netcode;

    [TestFixture]
    internal sealed unsafe class NetworkPayloadTests
    {
        [Test]
        public void Identity_Size_Is_5_Bytes()
        {
            // 1 byte HeaderID + 4 bytes NetworkID (int), Pack=1 → no padding
            Assert.AreEqual(5, NetworkPayloadIdentity.Size);
        }

        [Test]
        public void ForState_SetsHeaderAndNetworkID()
        {
            var identity = NetworkPayloadIdentity.ForState(GamePayloadHeader.PositionUpdate, 42);
            Assert.AreEqual((byte)GamePayloadHeader.PositionUpdate, identity.HeaderID);
            Assert.AreEqual(42, identity.NetworkID);
        }

        [Test]
        public void ForState_HeaderID_Matches_Enum_Value()
        {
            foreach (GamePayloadHeader header in Enum.GetValues(typeof(GamePayloadHeader)))
            {
                var identity = NetworkPayloadIdentity.ForState(header, 1);
                Assert.AreEqual(unchecked((byte)header), identity.HeaderID,
                    $"HeaderID mismatch for {header}");
            }
        }

        [Test]
        public void ForRPC_SetsHeaderAndZeroNetworkID()
        {
            var identity = NetworkPayloadIdentity.ForRPC(GamePayloadHeader.EntitySpawnAction);
            Assert.AreEqual((byte)GamePayloadHeader.EntitySpawnAction, identity.HeaderID);
            Assert.AreEqual(0, identity.NetworkID);
        }

        [Test]
        public void TryDeserialize_ReturnsTrue_AndCorrectPayload()
        {
            var identity = NetworkPayloadIdentity.ForState(GamePayloadHeader.PositionUpdate, 7);
            var payload = new EntitySpawnPayload(99, 2, EntitySpawnPayload.ActionType.Spawn, 1000u);

            int headerSize = NetworkPayloadIdentity.Size;
            int payloadSize = sizeof(EntitySpawnPayload);
            int totalSize = headerSize + payloadSize;

            byte* buffer = stackalloc byte[totalSize];

            // Manually lay out the buffer the same way NetworkSerializer.Serialize does
            *(NetworkPayloadIdentity*)buffer = identity;
            *(EntitySpawnPayload*)(buffer + headerSize) = payload;

            var serializer = new NetworkSerializer();
            bool ok = serializer.TryDeserialize<EntitySpawnPayload>((IntPtr)buffer, totalSize, out var result);

            Assert.IsTrue(ok);
            Assert.AreEqual(99, result.NetworkID);
            Assert.AreEqual(2, result.OwnerIndex);
            Assert.AreEqual(EntitySpawnPayload.ActionType.Spawn, result.Action);
            Assert.AreEqual(1000u, result.NetworkTick);
        }

        [Test]
        public void TryDeserialize_ReturnsFalse_WhenBufferTooSmall()
        {
            // 4 bytes < NetworkPayloadIdentity.Size(5) + sizeof(EntitySpawnPayload)
            byte* buffer = stackalloc byte[4];
            var serializer = new NetworkSerializer();

            bool ok = serializer.TryDeserialize<EntitySpawnPayload>((IntPtr)buffer, 4, out var result);

            Assert.IsFalse(ok);
            Assert.AreEqual(default(EntitySpawnPayload), result);
        }

        [Test]
        public void TryDeserialize_ReturnsFalse_WhenExactlyOneByteTooSmall()
        {
            int threshold = NetworkPayloadIdentity.Size + sizeof(EntitySpawnPayload);
            byte* buffer = stackalloc byte[threshold];
            var serializer = new NetworkSerializer();

            bool ok = serializer.TryDeserialize<EntitySpawnPayload>((IntPtr)buffer, threshold - 1, out _);

            Assert.IsFalse(ok);
        }

        [Test]
        public void TryDeserialize_ReturnsTrue_WhenBufferIsExactSize()
        {
            int totalSize = NetworkPayloadIdentity.Size + sizeof(EntitySpawnPayload);
            byte* buffer = stackalloc byte[totalSize];

            var identity = NetworkPayloadIdentity.ForRPC(GamePayloadHeader.EntitySpawnAction);
            var payload = new EntitySpawnPayload(5, 0, EntitySpawnPayload.ActionType.Despawn, 0u);

            *(NetworkPayloadIdentity*)buffer = identity;
            *(EntitySpawnPayload*)(buffer + NetworkPayloadIdentity.Size) = payload;

            var serializer = new NetworkSerializer();
            bool ok = serializer.TryDeserialize<EntitySpawnPayload>((IntPtr)buffer, totalSize, out var result);

            Assert.IsTrue(ok);
            Assert.AreEqual(5, result.NetworkID);
            Assert.AreEqual(EntitySpawnPayload.ActionType.Despawn, result.Action);
        }

        [Test]
        public void EntitySpawnPayload_NetworkTick_RoundTrips()
        {
            var payload = new EntitySpawnPayload(1, 1, EntitySpawnPayload.ActionType.Spawn, 0u)
            {
                NetworkTick = 12345u
            };

            Assert.AreEqual(12345u, payload.NetworkTick);
        }
    }
}
