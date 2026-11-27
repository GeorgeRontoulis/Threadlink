namespace Threadforge.Threadlink.Starter
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.Localization;
    using UnityEngine.Localization.Settings;
    using UnityEngine.UIElements;

    /// <summary>
    /// The starter's game flow: menu, new game, continue, load, play, pause, save, settings and recovery. It is one
    /// subsystem; everything it uses (scenes, audio, input, screens, saves) is already running when the first scene starts.
    /// </summary>
    public sealed class StarterGame : ThreadlinkSubsystem<StarterGame>
    {
        private StarterContent content;
        private StarterPreferences preferences = new StarterPreferences();
        private StarterSave save;
        private StarterLevel level;
        private PreparedLevel prepared;
        private ScreenHandle menu,
            hud,
            pause;
        private int slot = -1;
        private int lastSlot = -1;
        private double autosaveClock;
        private bool started;

        /// <summary>The starter's designer data.</summary>
        public static StarterContent Content => Instance.content;

        /// <summary>The level being played, or null in the menu.</summary>
        public static StarterLevel Level => Instance.level;

        /// <inheritdoc />
        public override void Report(MonitorReport report)
        {
            report.Summary(level != null ? "Playing slot " + slot : started ? "Menu" : "Waiting for the bootstrap scene");
            if (!report.Detailed)
                return;
            report.Section("Game");
            report.Field("Slot", slot);
            report.Field("Collected", save?.Collected.Count ?? 0);
            report.Field("Played", (save?.PlaySeconds ?? 0).ToString("0") + " s");
        }

        /// <summary>Starts the game from the bootstrap scene: applies preferences and shows the menu.</summary>
        public static async UniTask StartAsync(StarterContent content)
        {
            var game = Instance;
            if (game.started)
                return;
            string problem = content != null ? content.Validate() : "No starter content is assigned.";
            if (problem != null)
            {
                Debug.LogError("Threadlink Starter: " + problem, content);
                return;
            }
            game.started = true;
            game.content = content;
            if (Dextra.Players.Count == 0)
                Dextra.AddPlayer(content.Input);
            Dextra.Screens.DefaultPanel = content.Panel;
            Dextra.Player().Action(StarterInput.Gameplay.Pause).performed += _ => game.TogglePause();
            var preferences = await Sentinel.Saves.LoadAsync<StarterPreferences>(0);
            if (preferences.Succeeded)
                game.preferences = preferences.Value;
            game.preferences.Apply();
            await game.ShowMenuAsync();
        }

        /// <summary>Called by the level when its scene starts: restores the save and starts gameplay.</summary>
        internal void Attach(StarterLevel attached)
        {
            level = attached;
            autosaveClock = 0;
            level.Restore(save, prepared);
            OpenHudAsync().Forget();
        }

        internal void Detach(StarterLevel detached)
        {
            if (level == detached)
                level = null;
        }

        /// <summary>Records a collected item and plays its sound.</summary>
        internal void Collected(StarterCollectible item)
        {
            if (save == null || save.Collected.Contains(item.Identity))
                return;
            save.Collected.Add(item.Identity);
            if (content.Sounds.Pickup != null)
                Aura.Play(content.Sounds.Pickup, item.transform.position);
        }

        /// <summary>The player reached the exit.</summary>
        internal async UniTask ExitReachedAsync()
        {
            int missing = prepared.RequiredItems - save.Collected.Count;
            if (missing > 0)
            {
                await MessageAsync("Not yet", "Find " + missing + " more " + (missing == 1 ? "lantern" : "lanterns") + ".");
                return;
            }
            await SaveAsync(false);
            await MessageAsync("Level complete", "You found every lantern in " + save.PlaySeconds.ToString("0") + " seconds.");
            await ShowMenuAsync();
        }

        internal void Tick(float deltaTime)
        {
            if (save == null || level == null)
                return;
            save.PlaySeconds += deltaTime;
            autosaveClock += deltaTime;
            if (content.AutosaveSeconds > 0 && autosaveClock >= content.AutosaveSeconds && pause == null && !level.Busy && !Nexus.IsTransitioning)
            {
                autosaveClock = 0;
                SaveAsync(true).Forget();
            }
            if (hud != null && !hud.IsClosed)
            {
                hud.Root.Q<Label>("items").text = save.Collected.Count + " / " + prepared.RequiredItems;
                hud.Root.Q<Label>("prompt").text = PromptText(level.Prompt);
            }
        }

        private async UniTask ShowMenuAsync()
        {
            await CloseAllAsync();
            save = null;
            slot = -1;
            await Nexus.LoadAsync(content.MenuScene);
            Aura.PlayMusic(content.Sounds.MenuMusic);
            menu = await OpenAsync(content.Screens.MainMenu, ScreenLayer.Menu);
            var root = menu.Root;
            var account = Sentinel.Account;
            root.Q<Label>("profile").text = account.HasValue ? account.Value.Value : string.Empty;
            var saved = Sentinel.Saves.List<StarterSave>();
            var resume = root.Q<Button>("continue");
            resume.SetEnabled(saved.Contains(lastSlot) || saved.Length > 0);
            resume.clicked += () => LoadAsync(saved.Contains(lastSlot) ? lastSlot : saved.Max()).Forget();
            root.Q<Button>("new-game").clicked += () =>
            {
                int free = Enumerable.Range(0, content.SlotCount).FirstOrDefault(index => !saved.Contains(index));
                PlayAsync(free, new StarterSave { Level = content.FirstLevel.Identity.ToString("D") }).Forget();
            };
            root.Q<Button>("load").clicked += () => SlotsAsync().Forget();
            root.Q<Button>("settings").clicked += () => SettingsAsync().Forget();
            root.Q<Button>("quit").clicked += Quit;
        }

        private async UniTask PlayAsync(int target, StarterSave data)
        {
            await CloseAllAsync();
            slot = lastSlot = target;
            save = data;
            prepared = Vault.Get(content.FirstLevel);
            try
            {
                await Nexus.LoadAsync(prepared.Scene);
            }
            catch (Exception error)
            {
                await MessageAsync(Say(content.Text.LevelFailed), error.Message);
                await ShowMenuAsync();
            }
        }

        private async UniTask LoadAsync(int target)
        {
            var loaded = await Sentinel.Saves.LoadAsync<StarterSave>(target);
            if (loaded.Succeeded)
            {
                await PlayAsync(target, loaded.Value);
                return;
            }
            if (loaded.Error == PersistenceError.CorruptData)
                await RecoveryAsync(target, loaded.Message);
            else
                await MessageAsync(Say(content.Text.NothingToLoad), Say(content.Text.NothingToLoadDetail, target + 1, loaded.Error));
        }

        private async UniTask SaveAsync(bool quiet)
        {
            if (save == null || level == null)
                return;
            level.Capture(save);
            // The title is a label for this save, in the language it was saved in.
            var saved = await Sentinel.Saves.SaveAsync(slot, save, Say(prepared.DisplayName) + " · " + save.Collected.Count + "/" + prepared.RequiredItems);
            if (hud != null && !hud.IsClosed)
                hud.Root.Q<Label>("autosave").text = saved.Succeeded
                    ? Say(quiet ? content.Text.Autosaved : content.Text.Saved, DateTime.Now.ToString("t"))
                    : Say(content.Text.SaveFailedNote, saved.Error);
            if (!saved.Succeeded && !quiet)
                await MessageAsync(Say(content.Text.SaveFailed), saved.Error == PersistenceError.Conflict ? Say(content.Text.SaveConflict) : saved.Message ?? saved.Error.ToString());
        }

        private void TogglePause()
        {
            if (level == null || Nexus.IsTransitioning)
                return;
            if (pause != null && !pause.IsClosed)
                CloseAsync(pause).Forget();
            else
                PauseAsync().Forget();
        }

        private async UniTask PauseAsync()
        {
            pause = await OpenAsync(content.Screens.Pause, ScreenLayer.Menu);
            var root = pause.Root;
            root.Q<Button>("resume").clicked += () => CloseAsync(pause).Forget();
            root.Q<Button>("save").clicked += async () =>
            {
                await SaveAsync(false);
                root.Q<Label>("status").text = Say(content.Text.SavedToSlot, slot + 1);
            };
            root.Q<Button>("load").clicked += () => SlotsAsync().Forget();
            root.Q<Button>("settings").clicked += () => SettingsAsync().Forget();
            root.Q<Button>("menu").clicked += () => ShowMenuAsync().Forget();
        }

        private async UniTask SlotsAsync()
        {
            var screen = await OpenAsync(content.Screens.Slots, ScreenLayer.Modal);
            var list = screen.Root.Q<VisualElement>("slots");
            var saved = Sentinel.Saves.List<StarterSave>();
            for (int i = 0; i < content.SlotCount; i++)
            {
                int index = i;
                var button = new Button(async () =>
                {
                    await CloseAsync(screen);
                    if (saved.Contains(index))
                        await LoadAsync(index);
                    else
                        await PlayAsync(index, new StarterSave { Level = content.FirstLevel.Identity.ToString("D") });
                })
                {
                    text = Say(saved.Contains(i) ? content.Text.Slot : content.Text.EmptySlot, i + 1),
                };
                list.Add(button);
            }
            screen.Root.Q<Button>("back").clicked += () => CloseAsync(screen).Forget();
        }

        private async UniTask SettingsAsync()
        {
            var screen = await OpenAsync(content.Screens.Settings, ScreenLayer.Modal);
            var root = screen.Root;
            var volumes = root.Q<VisualElement>("volumes");
            foreach (string category in preferences.Volumes.Keys.ToArray())
            {
                var slider = new Slider(Label("volume-", category), 0, 1) { value = preferences.Volumes[category] };
                slider.RegisterValueChangedCallback(change =>
                {
                    preferences.Volumes[category] = change.newValue;
                    Aura.SetVolume(new AudioCategory(category), change.newValue);
                });
                volumes.Add(slider);
            }
            var muted = root.Q<Toggle>("muted");
            muted.value = preferences.Muted;
            muted.RegisterValueChangedCallback(change =>
            {
                preferences.Muted = change.newValue;
                preferences.Apply();
            });
            var bindings = root.Q<VisualElement>("bindings");
            void ShowBindings()
            {
                bindings.Clear();
                var player = Dextra.Player();
                foreach (var action in StarterInput.Maps.Gameplay.Find(player.Actions).actions)
                {
                    var id = InputId.Of(action);
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    row.Add(new Label(Label("action-", action.name)) { style = { width = 120 } });
                    row.Add(new Button(async () =>
                    {
                        await player.RebindAsync(id);
                        ShowBindings();
                    }) { text = player.Prompt(id) });
                    bindings.Add(row);
                }
            }
            ShowBindings();
            root.Q<Button>("reset-bindings").clicked += () =>
            {
                Dextra.Player().ResetBindings();
                ShowBindings();
            };
            root.Q<Button>("back").clicked += async () =>
            {
                await Sentinel.Saves.SaveAsync(0, preferences, "Preferences");
                await CloseAsync(screen);
            };
        }

        private async UniTask RecoveryAsync(int target, string detail)
        {
            var screen = await OpenAsync(content.Screens.Recovery, ScreenLayer.Modal);
            var root = screen.Root;
            root.Q<Label>("title").text = Say(content.Text.Damaged, target + 1);
            root.Q<Label>("detail").text = detail ?? Say(content.Text.DamagedDetail);
            root.Q<VisualElement>("versions").Add(new Button(async () =>
            {
                var restored = await Sentinel.Saves.RestorePreviousAsync<StarterSave>(target);
                await CloseAsync(screen);
                if (restored.Succeeded)
                    await LoadAsync(target);
                else
                    await MessageAsync(Say(content.Text.NothingToRestore), restored.Message ?? restored.Error.ToString());
            }) { text = Say(content.Text.RestorePrevious) });
            root.Q<Button>("back").clicked += () => CloseAsync(screen).Forget();
        }

        private async UniTask MessageAsync(string title, string body)
        {
            var screen = await OpenAsync(content.Screens.Message, ScreenLayer.Modal);
            screen.Root.Q<Label>("title").text = title;
            screen.Root.Q<Label>("body").text = body;
            var closed = new UniTaskCompletionSource();
            screen.Root.Q<Button>("ok").clicked += async () =>
            {
                await CloseAsync(screen);
                closed.TrySetResult();
            };
            await closed.Task;
        }

        private async UniTask OpenHudAsync()
        {
            hud = await OpenAsync(content.Screens.Hud, ScreenLayer.Hud, blocksInput: false);
            hud.Root.Q<Label>("level").text = Say(prepared.DisplayName);
        }

        // Text in the selected language. Entries with {0} receive the arguments.
        private static string Say(LocalizedString text, params object[] arguments) =>
            text == null || text.IsEmpty ? string.Empty : arguments.Length == 0 ? text.GetLocalizedString() : text.GetLocalizedString(arguments);

        // A label for a name, such as an audio category: the table's "<prefix><name>" entry, or the name itself.
        private string Label(string prefix, string name)
        {
            if (content.Text.Table.IsEmpty)
                return name;
            var table = LocalizationSettings.StringDatabase.GetTable(content.Text.Table.TableReference);
            var entry = table != null ? table.GetEntry(prefix + name.ToLowerInvariant()) : null;
            return entry != null ? entry.GetLocalizedString() : name;
        }

        // The HUD asks every frame; the text is looked up when the prompt or the language changes.
        private LocalizedString shownPrompt;
        private Locale shownLocale;
        private string shownPromptText = string.Empty;

        private string PromptText(LocalizedString prompt)
        {
            var locale = prompt != null ? LocalizationSettings.SelectedLocale : shownLocale;
            if (!ReferenceEquals(prompt, shownPrompt) || locale != shownLocale)
            {
                shownPrompt = prompt;
                shownLocale = locale;
                shownPromptText = Say(prompt);
            }
            return shownPromptText;
        }

        private async UniTask<ScreenHandle> OpenAsync(VisualTreeAsset document, ScreenLayer layer, bool blocksInput = true)
        {
            var screen = await Dextra.Screens.OpenAsync(document, layer, blocksInput);
            if (layer != ScreenLayer.Hud && content.Sounds.ScreenOpen != null)
                Aura.Play(content.Sounds.ScreenOpen);
            return screen;
        }

        private async UniTask CloseAsync(ScreenHandle screen)
        {
            if (screen == null || screen.IsClosed)
                return;
            await Dextra.Screens.CloseAsync(screen);
            if (screen.Layer != ScreenLayer.Hud && content.Sounds.ScreenClose != null)
                Aura.Play(content.Sounds.ScreenClose);
        }

        private async UniTask CloseAllAsync()
        {
            foreach (var screen in new[] { pause, menu, hud })
                if (screen != null && !screen.IsClosed)
                    await Dextra.Screens.CloseAsync(screen);
            pause = menu = hud = null;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
