namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Zeroed native blocks and memory hashes that are identical on every machine.</summary>
    public sealed unsafe class MemoryDemo : ShowcaseDemo
    {
        private const int Size = 64;
        private byte* frame;
        private byte* copy;
        private Label status;

        public override string Title => "Memory";

        public override string Summary =>
            "State that is copied, hashed and saved byte for byte lives in zeroed native blocks. XxHash64 over a block is identical on every machine and runtime, so two simulations can compare a single number to detect divergence.";

        public override string[] Packages => new[] { "memory" };

        private void Create()
        {
            frame = NativeAllocator.AllocateZeroed(Size);
            copy = NativeAllocator.AllocateZeroed(Size);
            for (int i = 0; i < Size; i++)
                frame[i] = (byte)i;
            NativeAllocator.Copy(copy, frame, Size);
        }

        private string Describe() =>
            $"Frame hash {MemoryHash.XxHash64(frame, Size):X16}\nCopy hash  {MemoryHash.XxHash64(copy, Size):X16}\nEqual: {NativeAllocator.Equal(frame, copy, Size)}";

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            Create();
            status = Value(Describe());
            panel.Add(Row(
                Action(context, "Change one byte of the frame", () =>
                {
                    frame[Size / 2]++;
                    status.text = Describe();
                }),
                Action(context, "Copy the frame", () =>
                {
                    NativeAllocator.Copy(copy, frame, Size);
                    status.text = Describe();
                })));
            panel.Add(status);
        }

        public override UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            Create();
            Check(MemoryHash.XxHash64(frame, Size) == 0xF7C67301DB6713F0UL, "XxHash64 matches the reference value");
            frame[Size / 2]++;
            Check(!NativeAllocator.Equal(frame, copy, Size) && MemoryHash.XxHash64(frame, Size) != MemoryHash.XxHash64(copy, Size), "a one-byte change changes the hash");
            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            NativeAllocator.Free(frame);
            NativeAllocator.Free(copy);
            frame = copy = null;
        }
    }
}
