// Threadlink 1.0 examples: content, scenes, audio, input, screens, interaction, Vault, saves, platform and ID domains.
// Compiled with the project; not used by any scene.
namespace Threadlink.User.Examples
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using MessagePack;
    using Threadforge.Threadlink;
    using UnityEngine;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.UIElements;

    /// <summary>
    /// What the Content mapping generates (Threadlink ▸ Content Mapping): one ID per ticked Addressables entry, split by
    /// how it loads and nested by Addressables group. A project's real classes are ContentAssets, ContentPrefabs and
    /// ContentScenes; these stand-ins have the same shape.
    /// </summary>
    public static class ExampleContent
    {
        public static class Characters
        {
            public static readonly ContentPrefabId Slime = new ContentPrefabId(101);
            public static readonly ContentAssetId HeroPortrait = new ContentAssetId(102);
        }

        public static class Caves
        {
            public static readonly ContentSceneId Level = new ContentSceneId(103);
            public static readonly ContentAssetId Drips = new ContentAssetId(104);
        }
    }

    /// <summary>Content: one request per load; pass an owner and it is released when the owner is destroyed.</summary>
    public sealed class HeroPortrait : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Renderer that shows the loaded portrait.")]
        private SpriteRenderer target;

        private async void Start() => target.sprite = (await Threadlink.LoadAsync<Sprite>(ExampleContent.Characters.HeroPortrait, this)).Asset;
    }

    public sealed class SlimeSpawner : MonoBehaviour
    {
        public async UniTask<ResourceRequest<GameObject>> SpawnAsync(Vector3 position)
        {
            // Releasing the request destroys the instance.
            return await Threadlink.InstantiateAsync(ExampleContent.Characters.Slime, position, Quaternion.identity, transform, destroyCancellationToken);
        }

        // A prefab can instantiate straight to one of its root components.
        public async UniTask<Enemy> SpawnEnemyAsync(Vector3 position) =>
            (await Threadlink.InstantiateAsync<Enemy>(ExampleContent.Characters.Slime, position, Quaternion.identity, transform, destroyCancellationToken)).Asset;

        // Addressables labels generate ContentLabels; one request holds every asset with the label.
        public async UniTask<int> CountForestCuesAsync()
        {
            var cues = await Threadlink.LoadAllAsync<AudioCue>(new ContentLabel("forest"), this);
            return cues.Asset.Count;
        }
    }

    /// <summary>Scenes: the next scene loads and prepares while the current one stays; failure keeps the current scene.</summary>
    public static class Travel
    {
        // GameScenes is generated from the Build Profile, keyed by scene GUID: renaming or moving a scene keeps it.
        public static async UniTask ToSampleAsync() => await Nexus.LoadAsync(GameScenes.SampleScene);

        public static async UniTask ToCavesAsync() => await Nexus.LoadAsync(ExampleContent.Caves.Level); // Addressables scene
    }

    /// <summary>Designer data refers to scenes with SceneReference: a scene picker in the Inspector.</summary>
    public sealed class Portal : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Scene loaded when something enters the portal.")]
        private SceneReference destination;

        private void OnTriggerEnter(Collider other) => Nexus.LoadAsync(destination).Forget();
    }

    /// <summary>A step every transition waits for before showing the next scene.</summary>
    public sealed class LoadingCurtain : ThreadlinkSubsystem<LoadingCurtain>
    {
        private IDisposable step;

        // Joining Nexus connects to another subsystem, so it happens in the initialization phase.
        protected override void OnSubsystemInit() =>
            step = Nexus.AddTransitionStep(
                async (transition, token) => await UniTask.WaitForSeconds(0.2f, cancellationToken: token),
                transition => Debug.Log("Now in " + transition.To.name)
            );

        protected override void OnSubsystemDeinit() => step?.Dispose();
    }

    /// <summary>Audio: cues are designer data; Aura needs no component in the scene.</summary>
    public sealed class Campfire : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Looping cue played at the campfire.")]
        private AudioCue crackle;

        private PlaybackHandle voice;

        private async void OnEnable()
        {
            voice = Aura.Play(crackle, transform);   // follows this transform, stops when it is destroyed
            if (voice != null)                        // null: a voice limit rejected it
                await voice.FadeToAsync(1, 2f);
        }

        private void OnDisable() => voice?.FadeToAsync(0, 0.5f, stopWhenFinished: true).Forget();

        // AudioCategories is generated from Project Settings ▸ Threadlink ▸ Aura.
        public static void ApplyAudioSettings(float music, bool muteEffects)
        {
            Aura.SetVolume(AudioCategories.Music, music);
            Aura.SetMute(AudioCategories.Effects, muteEffects);
        }

        public static async UniTask PlayCaveAmbienceAsync() => await Aura.PlayAsync(ExampleContent.Caves.Drips);
    }

    /// <summary>Input: player 0 is ready in Awake with the project's actions; rebinds are saved automatically.</summary>
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Movement speed in meters per second.")]
        private float speed = 5;

        // GameInput is generated from the project's input actions; each ID carries its value type.
        private void Update()
        {
            var player = Dextra.Player();
            Vector2 move = player.Read(GameInput.Gameplay.Move);
            transform.position += new Vector3(move.x, 0, move.y) * (speed * Time.deltaTime);
            if (player.WasPressed(GameInput.Gameplay.Jump))
                Debug.Log("Jump (" + player.Prompt(GameInput.Gameplay.Jump) + ")");
        }

        public static async UniTask RebindJumpAsync() => await Dextra.Player().RebindAsync(GameInput.Gameplay.Jump);

        /// <summary>A cutscene suspends gameplay input until the claim is disposed.</summary>
        public static System.IDisposable BlockGameplay() => Dextra.Player().Claim(200, true, GameInput.Maps.UI);
    }

    /// <summary>Screens: open a UI Toolkit document, bind it, close it; focus and gameplay input come back.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("UI document opened as the pause screen.")]
        private VisualTreeAsset pauseDocument;

        private ScreenHandle open;

        public async UniTask ToggleAsync()
        {
            if (open != null && !open.IsClosed)
            {
                await Dextra.Screens.CloseAsync(open);
                return;
            }
            open = await Dextra.Screens.OpenAsync(pauseDocument, ScreenLayer.Menu);
            open.Root.Q<Button>("resume").clicked += () => Dextra.Screens.CloseAsync(open).Forget();
        }
    }

    /// <summary>Interaction: derive from Interactable; an InteractionSensor on the player finds it.</summary>
    public sealed class Chest : Interactable
    {
        [SerializeField]
        [Tooltip("Coins given when the chest opens.")]
        private int coins = 5;

        private bool opened;

        public override string Prompt => opened ? "Empty" : "Open chest";

        public override bool CanInteract(GameObject actor) => base.CanInteract(actor) && !opened;

        protected override async UniTask InteractAsync(GameObject actor, CancellationToken token)
        {
            await UniTask.WaitForSeconds(0.3f, cancellationToken: token);
            opened = true;
            Wallet.Add(coins);
        }
    }

    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Sensor that finds interactables near the player.")]
        private InteractionSensor sensor;

        private void Update()
        {
            if (Dextra.Player().WasPressed(GameInput.Gameplay.Interact))
                sensor.InteractAsync().Forget();
        }
    }

    /// <summary>Definitions: designers edit the asset, code reads an immutable value prepared once per session.</summary>
    public sealed class EnemyStats
    {
        public EnemyStats(string name, int health, float speed)
        {
            Name = name;
            Health = health;
            Speed = speed;
        }

        public string Name { get; }

        public int Health { get; }

        public float Speed { get; }
    }

    [CreateAssetMenu(menuName = "Examples/Enemy Definition")]
    public sealed class EnemyDefinition : VaultAsset<EnemyStats>
    {
        [Tooltip("Name shown in game.")]
        public string DisplayName = "Slime";

        [Min(1)]
        [Tooltip("Starting hit points.")]
        public int Health = 30;

        [Tooltip("Movement speed in meters per second.")]
        public float Speed = 2;

        public override Guid Schema => new Guid("1f3c5e7a-9b2d-4f60-8a1b-2c3d4e5f6a7b");

        public override int SchemaVersion => 1;

        protected override EnemyStats BuildRuntime(VaultContext context) => new EnemyStats(DisplayName, Health, Speed);
    }

    public sealed class Enemy : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Vault asset with this enemy's stats.")]
        private EnemyDefinition definition;

        private int health;

        private void Start() => health = Vault.Get(definition).Health;
    }

    /// <summary>Saves: declare once; Sentinel saves per account, atomically, with conflicts, migration and recovery.</summary>
    [Save("example-game", 1)]
    [MessagePackObject]
    public sealed partial class GameSave
    {
        [Key(0)]
        public int Coins { get; set; }

        [Key(1)]
        public string Scene { get; set; } = "Forest";
    }

    public static class SaveFlow
    {
        public static async UniTask<bool> SaveAsync(int slot)
        {
            var saved = await Sentinel.Saves.SaveAsync(slot, new GameSave { Coins = Wallet.Instance.Coins }, "Forest");
            return saved.Succeeded;                     // Conflict, Unsupported, ... are explicit
        }

        public static async UniTask<GameSave> LoadAsync(int slot)
        {
            var loaded = await Sentinel.Saves.LoadAsync<GameSave>(slot);
            if (loaded.Error == PersistenceError.CorruptData && (await Sentinel.Saves.RestorePreviousAsync<GameSave>(slot)).Succeeded)
                loaded = await Sentinel.Saves.LoadAsync<GameSave>(slot);
            return loaded.Succeeded ? loaded.Value : new GameSave();
        }
    }

    /// <summary>Platform: capabilities route to the installed provider (local, Steam, ...); unsupported is an answer.</summary>
    public static class Rewards
    {
        // Achievements and Statistics are generated from what each platform authors (Steam: Project Settings ▸
        // Threadlink ▸ Steam). The same code works on every platform; one that lacks an ID answers NotFound.
        public static async UniTask FirstChestAsync()
        {
            var result = await Sentinel.UnlockAchievementAsync(Achievements.FirstChest);
            if (!result.Succeeded)
                Debug.Log("Achievement first-chest: " + result.Error);
        }

        public static async UniTask LanternFoundAsync(int found)
        {
            await Sentinel.SetAchievementProgressAsync(Achievements.AllLanterns, found / 3.0);
            if (Sentinel.TryGet<IStatistics>(out var statistics))
                await statistics.WriteAsync(Statistics.LanternsFound, found, Sentinel.Accounts.Capture().Token);
        }
    }

    /// <summary>ID domains: EnemyKind (Design/Identifiers) and RandomDomains (Engineering/Identifiers) are generated.</summary>
    public sealed class Spawner : MonoBehaviour
    {
        [Tooltip("Kind of enemy this spawner creates.")]
        [SerializeField] private EnemyKind kind = EnemyKind.Slime;   // a dropdown in the Inspector, stored as a stable int

        public int RollGold(StatelessRNG.Source world, ulong chestId, ulong day)
        {
            var loot = world.CreateStream(RandomDomains.Loot, chestId, day);
            return kind == EnemyKind.Wraith ? loot.Range(40, 90) : loot.Range(10, 50);
        }
    }
}
