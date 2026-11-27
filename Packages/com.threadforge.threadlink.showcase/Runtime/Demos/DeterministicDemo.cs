namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System.Linq;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Stateless random streams and fixed-point arithmetic that reproduce exactly on every machine.</summary>
    public sealed class DeterministicDemo : ShowcaseDemo
    {
        private static readonly StatelessRNG.Domain Loot = StatelessRNG.Domain.Named("Loot");
        private static readonly StatelessRNG.Domain Weather = StatelessRNG.Domain.Named("Weather");
        private ulong seed = 2026;
        private VisualElement plot;
        private Label status;

        public override string Title => "Deterministic";

        public override string Summary =>
            "A stream is a pure function of the seed, a domain and identity parts, so the same chest on day 3 always drops the same loot, in any order and on any machine. FP numbers give identical arithmetic everywhere.";

        public override string[] Packages => new[] { "deterministic" };

        private int[] Drops(StatelessRNG.Source source, ulong chest, ulong day)
        {
            var stream = source.CreateStream(Loot, chest, day);
            return Enumerable.Range(0, 5).Select(_ => stream.Range(1, 100)).ToArray();
        }

        private void Draw()
        {
            plot.Clear();
            var stream = new StatelessRNG.Source(seed).CreateStream(Weather, 7);
            for (int i = 0; i < 150; i++)
            {
                FPVector2 point = stream.InsideUnitCircle();
                var dot = new VisualElement();
                dot.style.position = Position.Absolute;
                dot.style.width = dot.style.height = 4;
                dot.style.backgroundColor = new Color(0.5f, 0.8f, 1f);
                dot.style.left = 70 + (float)point.X * 65;
                dot.style.top = 70 + (float)point.Y * 65;
                plot.Add(dot);
            }
        }

        private string Describe()
        {
            var a = new StatelessRNG.Source(seed);
            var b = new StatelessRNG.Source(seed);
            var third = FP.FromFraction(1, 3);
            return $"Seed {seed}. Machine A chest 12, day 3: {string.Join(", ", Drops(a, 12, 3))}\n"
                + $"Machine B, other chests first: {string.Join(", ", Drops(b, 99, 1))} … then chest 12, day 3: {string.Join(", ", Drops(b, 12, 3))}\n"
                + $"FP: 1/3 = {(double)third:0.##########}, (1/3) × 3 = {(double)(third * 3):0.##########}, √2 = {(double)FP.Sqrt(2):0.##########}";
        }

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            status = Value(Describe());
            plot = new VisualElement { style = { width = 140, height = 140, backgroundColor = new Color(0, 0, 0, 0.25f) } };
            panel.Add(Row(Action(context, "New seed", () =>
            {
                seed = (ulong)Random.Range(1, int.MaxValue);
                status.text = Describe();
                Draw();
            })));
            panel.Add(status);
            panel.Add(plot);
            Draw();
        }

        public override UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            var a = new StatelessRNG.Source(seed);
            var b = new StatelessRNG.Source(seed);
            Drops(b, 99, 1);
            Check(Drops(a, 12, 3).SequenceEqual(Drops(b, 12, 3)), "streams do not depend on other draws");
            Check(!Drops(a, 12, 3).SequenceEqual(Drops(a, 12, 4)), "identity parts separate streams");
            Check(FP.Sqrt(FP.FromInt(4)) == FP.FromInt(2), "fixed-point square root is exact for squares");
            return UniTask.CompletedTask;
        }
    }
}
