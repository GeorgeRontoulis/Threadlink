namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Users;
    using UnityEngine.UIElements;

    /// <summary>
    /// Dextra: input, screens and interaction for the session. Player 0 is ready from the first Awake with the project's
    /// input actions (Project Settings ▸ Threadlink ▸ Dextra) and its gameplay map enabled. Screens open on layers, take
    /// focus, block gameplay input while open, and give it back when they close.
    /// </summary>
    public sealed class Dextra : ThreadlinkSubsystem<Dextra>
    {
        private readonly List<PlayerInputContext> players = new List<PlayerInputContext>();
        private readonly List<IDisposable> gameplayClaims = new List<IDisposable>();
        private ScreenHost host;
        private InputBlocker blocker;
        private ScreenFactory factory;
        private GameObject root;

        /// <summary>Players joined this session. Player 0 always exists once input actions are configured.</summary>
        public static IReadOnlyList<PlayerInputContext> Players => Instance.players;

        /// <inheritdoc />
        protected override void OnSubsystemBoot()
        {
            root = new GameObject("Threadlink Screens") { hideFlags = HideFlags.DontSave };
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(root);
            // Screen prefabs load through the content library when they open, so booting needs no other subsystem.
            factory = new ScreenFactory(root.transform, () => ContentLibrary.Instance.Provider);
            if (DextraSettings.Current.Actions != null)
                Join(Array.Empty<InputDevice>(), announce: false);
            blocker = new InputBlocker(() => players.Count > 0 ? players[0] : null, () => DextraSettings.Current.InterfaceMap);
            blocker.BlockChanged = blocked => Iris.TryNotify(new GameplayInputBlockChanged(blocked));
            host = new ScreenHost(factory, blocker)
            {
                Opened = screen => Iris.TryNotify(new ScreenOpened(screen)),
                Closed = screen => Iris.TryNotify(new ScreenClosed(screen)),
            };
            InputUser.onChange += OnUserChange;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        // Announces the players that joined during boot, now that every subsystem has subscribed.
        /// <inheritdoc />
        protected override void OnSubsystemInit()
        {
            for (int i = 0; i < players.Count; i++)
                Iris.TryNotify(new PlayerJoined(players[i], i));
        }

        // Closes screens while the content they came from is still available.
        /// <inheritdoc />
        protected override void OnSubsystemDeinit() => host?.Dispose();

        /// <inheritdoc />
        protected override void OnSubsystemShutdown()
        {
            InputUser.onChange -= OnUserChange;
            InputSystem.onDeviceChange -= OnDeviceChange;
            Screens.DefaultPanel = null;
            host?.Dispose();
            foreach (var claim in gameplayClaims)
                claim.Dispose();
            gameplayClaims.Clear();
            foreach (var player in players)
                player.Dispose();
            players.Clear();
            if (root != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <inheritdoc />
        public override void Report(MonitorReport report)
        {
            report.Summary(players.Count + (players.Count == 1 ? " player, " : " players, ") + host.OpenCount + " screens");
            if (!report.Detailed)
                return;
            report.Section("Players");
            for (int i = 0; i < players.Count; i++)
                report.Field("Player " + i, players[i].Available ? players[i].LastDevice?.displayName ?? "Paired" : "No device", players[i].Available ? MonitorTone.Normal : MonitorTone.Caution);
            if (players.Count == 0)
                report.Note("No input actions are assigned in Project Settings ▸ Threadlink ▸ Dextra.", MonitorTone.Caution);
            report.Section("Screens");
            report.Field("Open", host.OpenCount);
        }

        /// <summary>A player's input. Player 0 uses every device not paired to another player.</summary>
        public static PlayerInputContext Player(int index = 0)
        {
            var dextra = Instance;
            if (index < 0 || index >= dextra.players.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    dextra.players.Count == 0 ? "No players: assign input actions in Project Settings ▸ Threadlink ▸ Dextra." : "Player " + index + " has not joined."
                );
            return dextra.players[index];
        }

        /// <summary>Adds a local player paired to <paramref name="devices"/>, with the gameplay map enabled.</summary>
        public static PlayerInputContext AddPlayer(params InputDevice[] devices) => Instance.Join(devices, announce: true);

        /// <summary>Adds a local player with its own <paramref name="actions"/>, paired to <paramref name="devices"/>.</summary>
        public static PlayerInputContext AddPlayer(InputActionAsset actions, params InputDevice[] devices) => Instance.Join(devices, announce: true, actions);

        /// <summary>Removes a local player. Later players move down one index.</summary>
        public static void RemovePlayer(PlayerInputContext player)
        {
            var dextra = Instance;
            int index = dextra.players.IndexOf(player);
            if (index < 0)
                throw new ArgumentException("This player has not joined.", nameof(player));
            dextra.gameplayClaims[index].Dispose();
            dextra.gameplayClaims.RemoveAt(index);
            dextra.players.RemoveAt(index);
            player.Dispose();
            Iris.TryNotify(new PlayerLeft(player, index));
        }

        private PlayerInputContext Join(InputDevice[] devices, bool announce, InputActionAsset actions = null)
        {
            var settings = DextraSettings.Current;
            actions ??= settings.Actions;
            if (actions == null)
                throw new InvalidOperationException("Assign input actions in Project Settings ▸ Threadlink ▸ Dextra.");
            bool free = devices.Length == 0;
            if (free)
                devices = UnpairedDevices();
            var player = new PlayerInputContext(actions, devices);
            if (free)
                player.Lend(devices);
            int index = players.Count;
            players.Add(player);
            if (settings.SaveBindings)
            {
                string path = BindingsPath(index);
                try
                {
                    if (File.Exists(path))
                        player.ImportOverrides(File.ReadAllText(path));
                }
                catch (Exception error)
                {
                    Debug.LogWarning("Threadlink: saved bindings for player " + index + " were ignored. " + error.Message);
                }
            }
            player.BindingsChanged = OnBindingsChanged;
            player.ControlsChanged = changed => Iris.TryNotify(new ControlsChanged(changed, changed.LastDevice));
            string gameplay = settings.MapName(player.Actions, settings.GameplayMap);
            gameplayClaims.Add(string.IsNullOrEmpty(gameplay) ? new Nothing() : player.ClaimByName(0, false, gameplay));
            if (announce)
                Iris.TryNotify(new PlayerJoined(player, index));
            return player;
        }

        private void OnBindingsChanged(PlayerInputContext player)
        {
            if (DextraSettings.Current.SaveBindings)
                Save(player);
            Iris.TryNotify(new BindingsChanged(player));
        }

        private PlayerInputContext PlayerOf(InputUser user)
        {
            foreach (var player in players)
                if (player.UserId == user.id)
                    return player;
            return null;
        }

        private void OnUserChange(InputUser user, InputUserChange change, InputDevice device)
        {
            if (change != InputUserChange.DeviceLost && change != InputUserChange.DeviceRegained)
                return;
            var player = PlayerOf(user);
            if (player == null)
                return;
            if (change == InputUserChange.DeviceLost)
                Iris.TryNotify(new DeviceLost(player, device));
            else
                Iris.TryNotify(new DeviceRegained(player, device));
        }

        // Player 0 uses every device no other player owns, including devices connected after it joined. A device
        // reconnecting to the player that lost it is left to the Input System, which announces it as regained.
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Added || players.Count == 0)
                return;
            foreach (var user in InputUser.all)
                foreach (var lost in user.lostDevices)
                    if (lost == device)
                        return;
            players[0].Lend(device);
        }

        private InputDevice[] UnpairedDevices()
        {
            var taken = new HashSet<InputDevice>();
            foreach (var player in players)
                foreach (var user in UnityEngine.InputSystem.Users.InputUser.all)
                    if (user.id == player.UserId)
                        foreach (var device in user.pairedDevices)
                            taken.Add(device);
            var free = new List<InputDevice>();
            foreach (var device in InputSystem.devices)
                if (!taken.Contains(device))
                    free.Add(device);
            return free.ToArray();
        }

        private void Save(PlayerInputContext player)
        {
            int index = players.IndexOf(player);
            if (index < 0)
                return;
            try
            {
                string path = BindingsPath(index);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, player.ExportOverrides());
            }
            catch (Exception error)
            {
                Debug.LogWarning("Threadlink: bindings for player " + index + " could not be saved. " + error.Message);
            }
        }

        private static string BindingsPath(int player) =>
            Path.Combine(Application.persistentDataPath, "Threadlink", "Input", "player-" + player + ".json");

        /// <summary>Screens: UI that opens on a layer, takes focus and blocks gameplay input until it closes.</summary>
        public static class Screens
        {
            /// <summary>Number of open screens.</summary>
            public static int OpenCount => Instance.host.OpenCount;

            /// <summary>Panel settings for UI Toolkit documents opened as screens, overriding Project Settings ▸ Threadlink ▸ Dextra.</summary>
            public static PanelSettings DefaultPanel { get; set; }

            /// <summary>Opens a mapped screen prefab (a ContentPrefabs ID). The prefab needs a ToolkitScreen or UGuiScreen.</summary>
            public static UniTask<ScreenHandle> OpenAsync(ContentPrefabId screen, ScreenLayer layer = ScreenLayer.Menu, bool blocksInput = true, CancellationToken token = default) =>
                screen.IsAssigned
                    ? Instance.host.OpenAsync(screen.Identity, layer, true, blocksInput, token)
                    : UniTask.FromException<ScreenHandle>(new ArgumentException("The screen ID is empty.", nameof(screen)));

            /// <summary>Opens a UI Toolkit document as a screen, using the panel settings from Project Settings ▸ Threadlink ▸ Dextra.</summary>
            public static UniTask<ScreenHandle> OpenAsync(VisualTreeAsset document, ScreenLayer layer = ScreenLayer.Menu, bool blocksInput = true, CancellationToken token = default)
            {
                var dextra = Instance;
                return dextra.host.OpenAsync(dextra.factory.Register(document, layer), layer, true, blocksInput, token);
            }

            /// <summary>Closes a screen and returns focus and input to what was below it.</summary>
            public static UniTask CloseAsync(ScreenHandle screen, CancellationToken token = default) =>
                Instance.host.CloseAsync(screen, token);
        }

        private sealed class Nothing : IDisposable
        {
            public void Dispose() { }
        }

        /// <summary>Creates screens from catalog prefabs or UI Toolkit documents.</summary>
        private sealed class ScreenFactory : IScreenFactory
        {
            private readonly Transform parent;
            private readonly ContentScreenFactory content;
            private readonly Dictionary<Guid, (VisualTreeAsset document, ScreenLayer layer)> documents = new Dictionary<Guid, (VisualTreeAsset, ScreenLayer)>();
            private readonly Dictionary<VisualTreeAsset, Guid> identities = new Dictionary<VisualTreeAsset, Guid>();

            internal ScreenFactory(Transform parent, Func<ContentProvider> provider)
            {
                this.parent = parent;
                content = new ContentScreenFactory(provider, instance =>
                {
                    instance.transform.SetParent(parent, false);
                    return instance.GetComponent<IScreenView>();
                });
            }

            internal Guid Register(VisualTreeAsset document, ScreenLayer layer)
            {
                if (document == null)
                    throw new ArgumentNullException(nameof(document));
                if (!identities.TryGetValue(document, out var id))
                    identities.Add(document, id = Guid.NewGuid());
                documents[id] = (document, layer);
                return id;
            }

            public UniTask<IScreenView> CreateAsync(Guid screen, CancellationToken token)
            {
                if (!documents.TryGetValue(screen, out var entry))
                    return content.CreateAsync(screen, token);
                token.ThrowIfCancellationRequested();
                var panel = Screens.DefaultPanel != null ? Screens.DefaultPanel : DextraSettings.Current.Panel;
                if (panel == null)
                {
                    // Without configured panel settings, share the panel of a document already on screen.
                    var existing = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
                    panel = existing != null ? existing.panelSettings : null;
                }
                if (panel == null)
                    throw new InvalidOperationException("Assign Panel Settings in Project Settings ▸ Threadlink ▸ Dextra to open UI Toolkit documents.");
                var instance = new GameObject(entry.document.name);
                instance.SetActive(false);
                instance.transform.SetParent(parent, false);
                var document = instance.AddComponent<UIDocument>();
                document.panelSettings = panel;
                document.visualTreeAsset = entry.document;
                document.sortingOrder = (int)entry.layer * 100;
                var view = instance.AddComponent<ToolkitScreen>();
                return UniTask.FromResult<IScreenView>(new DocumentView(view, instance));
            }

            private sealed class DocumentView : IScreenView, IScreenObject
            {
                private readonly IScreenView view;
                private GameObject instance;

                internal DocumentView(IScreenView view, GameObject instance)
                {
                    this.view = view;
                    this.instance = instance;
                }

                public UniTask PrepareAsync(CancellationToken token) => view.PrepareAsync(token);

                public GameObject Instance => instance;

                public void SetVisible(bool visible) => view.SetVisible(visible);

                public void SetFocused(bool focused) => view.SetFocused(focused);

                public void Dispose()
                {
                    if (instance == null)
                        return;
                    try
                    {
                        view.Dispose();
                    }
                    finally
                    {
                        if (Application.isPlaying)
                            UnityEngine.Object.Destroy(instance);
                        else
                            UnityEngine.Object.DestroyImmediate(instance);
                        instance = null;
                    }
                }
            }
        }
    }
}
