namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.Threading;
    using NUnit.Framework;
    using UnityEngine.TestTools;

    public sealed class IntegerCodec : ISnapshotCodec<int>
    {
        public byte[] Encode(int value) => BitConverter.GetBytes(value);

        public int Decode(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.Length != 4)
                throw new FormatException();
            return BitConverter.ToInt32(bytes.ToArray(), 0);
        }
    }

    public class SnapshotSchemaTests
    {
        private static SnapshotSchema<int> Schema(Guid id, int version = 1) =>
            new SnapshotSchema<int>(
                id,
                version,
                new IntegerCodec(),
                value =>
                {
                    if (value < 0)
                        throw new ArgumentException("Negative state.");
                }
            );

        [Test]
        public void CurrentRoundTripAndValidationUseTheDeclaredSchema()
        {
            var id = Guid.NewGuid();
            var schema = Schema(id);
            byte[] bytes = schema.Write(17);
            Assert.That(bytes.Length, Is.EqualTo(32));
            Assert.That(new Guid(bytes.AsSpan(4, 16)), Is.EqualTo(id));
            Assert.That(schema.Read(bytes), Is.EqualTo(17));
            Assert.Throws<ArgumentException>(() => schema.Write(-1));
            BitConverter.GetBytes(-1).CopyTo(bytes, 28);
            Assert.Throws<ArgumentException>(() => schema.Read(bytes));
        }

        [Test]
        public void MigrationIsExplicitAndUnknownVersionNeverUsesLegacyReader()
        {
            var id = Guid.NewGuid();
            var old = Schema(id);
            var current = Schema(id, 2);
            int legacyCalls = 0;
            current.SetLegacyReader(bytes =>
            {
                legacyCalls++;
                return 99;
            });
            Assert.That(
                Assert
                    .Throws<SnapshotSchemaException>(() => current.Read(old.Write(7)))
                    .SourceVersion,
                Is.EqualTo(1)
            );
            Assert.That(legacyCalls, Is.Zero);
            current.AddMigration(1, bytes => new IntegerCodec().Decode(bytes) + 2);
            Assert.That(current.Read(old.Write(7)), Is.EqualTo(9));
            Assert.That(current.Read(new byte[] { 1 }), Is.EqualTo(99));
            Assert.Throws<SnapshotSchemaException>(() => Schema(Guid.NewGuid()).Read(old.Write(7)));
        }

        [Test]
        public void TruncationLengthAndByteLimitsAreRejected()
        {
            var schema = Schema(Guid.NewGuid());
            var bytes = schema.Write(5);
            Array.Resize(ref bytes, bytes.Length - 1);
            Assert.Throws<InvalidOperationException>(() => schema.Read(bytes));
            var small = new SnapshotSchema<int>(
                Guid.NewGuid(),
                1,
                new IntegerCodec(),
                _ => { },
                31
            );
            Assert.Throws<InvalidOperationException>(() => small.Write(5));
            Assert.Throws<SnapshotSchemaException>(() => schema.Read(new byte[] { 1, 2 }));
        }

        [UnityTest]
        public IEnumerator AutosaveNeedsAnExplicitBoundaryAndDoesNotOverlap()
        {
            var gate = new UniTaskCompletionSource<bool>();
            var schedule = new AutosaveSchedule(TimeSpan.FromSeconds(2));
            int saved = 0;
            var early = schedule.AdvanceAsync(
                TimeSpan.FromSeconds(1),
                true,
                _ =>
                {
                    saved++;
                    return UniTask.CompletedTask;
                }
            );
            Assert.That(early.GetAwaiter().GetResult(), Is.False);
            var blocked = schedule.AdvanceAsync(
                TimeSpan.FromSeconds(2),
                false,
                _ =>
                {
                    saved++;
                    return UniTask.CompletedTask;
                }
            );
            Assert.That(blocked.GetAwaiter().GetResult(), Is.False);
            var saving = schedule.AdvanceAsync(
                TimeSpan.FromSeconds(3),
                true,
                async _ =>
                {
                    saved++;
                    await gate.Task;
                }
            );
            Assert.That(schedule.IsSaving, Is.True);
            Assert.That(
                schedule
                    .AdvanceAsync(TimeSpan.FromSeconds(4), true, _ => UniTask.CompletedTask)
                    .GetAwaiter().GetResult(),
                Is.False
            );
            gate.TrySetResult(true);
            while (!saving.Status.IsCompleted())
                yield return null;
            Assert.That(saving.GetAwaiter().GetResult(), Is.True);
            Assert.That(saved, Is.EqualTo(1));
            schedule.IsPaused = true;
            Assert.That(
                schedule
                    .AdvanceAsync(TimeSpan.FromSeconds(9), true, _ => UniTask.CompletedTask)
                    .GetAwaiter().GetResult(),
                Is.False
            );
            Assert.Throws<ArgumentException>(
                () =>
                    schedule
                        .AdvanceAsync(TimeSpan.FromSeconds(1), true, _ => UniTask.CompletedTask)
                        .GetAwaiter()
                        .GetResult()
            );
        }
    }
}
