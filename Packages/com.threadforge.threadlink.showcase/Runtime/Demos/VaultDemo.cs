namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Vault definitions: designer assets prepared into immutable values.</summary>
    public sealed class VaultDemo : ShowcaseDemo
    {
        /// <summary>The prepared value code uses: immutable.</summary>
        public sealed class EnemyStats
        {
            /// <summary>Creates the value.</summary>
            public EnemyStats(string name, int health, float speed)
            {
                Name = name;
                Health = health;
                Speed = speed;
            }

            /// <summary>Display name.</summary>
            public string Name { get; }

            /// <summary>Hit points.</summary>
            public int Health { get; }

            /// <summary>Meters per second.</summary>
            public float Speed { get; }
        }

        /// <summary>The asset designers edit.</summary>
        public sealed class EnemyDefinition : VaultAsset<EnemyStats>
        {
            /// <summary>Display name.</summary>
            [Tooltip("Name shown in game.")]
            public string DisplayName = "Slime";

            /// <summary>Hit points.</summary>
            [Min(1)]
            [Tooltip("Starting hit points.")]
            public int Health = 30;

            /// <summary>Meters per second.</summary>
            [Tooltip("Movement speed in meters per second.")]
            public float Speed = 2;

            public override Guid Schema => new Guid("8a1f2c3d-4e5f-4061-8273-94a5b6c7d8e9");

            public override int SchemaVersion => 1;

            protected override EnemyStats BuildRuntime(VaultContext context)
            {
                if (Health < 1)
                    throw new InvalidOperationException("Health must be positive.");
                return new EnemyStats(DisplayName, Health, Speed);
            }
        }

        private EnemyDefinition slime;

        public override string Title => "Vault";

        public override string Summary =>
            "Designers edit Vault assets in the Inspector; code reads Vault.Get(asset), an immutable value prepared and validated once per session. Variants inherit from a parent and override only what they change.";

        public override string[] Packages => new[] { "core" };

        private EnemyDefinition Slime()
        {
            if (slime == null)
            {
                slime = ScriptableObject.CreateInstance<EnemyDefinition>();
                slime.name = "Slime";
            }
            return slime;
        }

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            var stats = Vault.Get(Slime());
            panel.Add(Value($"{stats.Name}: {stats.Health} health, {stats.Speed} m/s"));
            panel.Add(Row(Action(context, "Prepare again", () => context.Log("Same prepared value: " + ReferenceEquals(stats, Vault.Get(Slime()))))));
        }

        public override UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            var stats = Vault.Get(Slime());
            Check(stats.Name == "Slime" && stats.Health == 30, "the prepared value carries the authored data");
            Check(ReferenceEquals(stats, Vault.Get(Slime())), "a definition is prepared once per session");
            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            if (slime != null)
                UnityEngine.Object.Destroy(slime);
        }
    }
}
