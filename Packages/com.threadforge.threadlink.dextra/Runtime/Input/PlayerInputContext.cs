namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Users;

    /// <summary>Independent per-player action clone, device pairing, composable contexts and rebindings.</summary>
    public sealed class PlayerInputContext : IDisposable
    {
        private sealed class ClaimToken : IDisposable
        {
            internal PlayerInputContext Owner;
            internal string[] Maps;
            internal int Priority;
            internal bool BlocksLower;

            public void Dispose()
            {
                var owner = Owner;
                Owner = null;
                if (owner != null)
                {
                    owner.claims.Remove(this);
                    owner.Refresh();
                }
            }
        }

        // Devices a player uses because no other player owns them (player 0's free devices). An explicit assignment to
        // another player takes them over.
        private static readonly Dictionary<InputDevice, PlayerInputContext> lent = new Dictionary<InputDevice, PlayerInputContext>();

        private readonly List<ClaimToken> claims = new List<ClaimToken>();
        private InputUser user;
        private InputActionRebindingExtensions.RebindingOperation rebinding;
        private InputAction rebindingAction;
        private bool disposed;
        private CancellationTokenRegistration rebindingCancellation;
        private UniTaskCompletionSource<string[]> rebindResult;
        private string originalOverrides;

        /// <summary>Player-owned action definitions and overrides; never the original project asset.</summary>
        public InputActionAsset Actions { get; }

        /// <summary>The most recently used paired device, used for prompt resolution.</summary>
        public InputDevice LastDevice { get; private set; }

        /// <summary>Whether paired devices are currently available. InputUser retains lost-device reconnect information.</summary>
        public bool Available => !disposed && user.valid && user.pairedDevices.Count > 0;

        /// <summary>Explicit paired-user identity.</summary>
        public uint UserId => user.id;

        internal PlayerInputContext(InputActionAsset source, params InputDevice[] devices)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            Actions = UnityEngine.Object.Instantiate(source);
            Actions.Disable();
            user = InputUser.CreateUserWithoutPairedDevices();
            try
            {
                user.AssociateActionsWithUser(Actions);
                ReassignDevices(devices);
                foreach (var map in Actions.actionMaps)
                    map.actionTriggered += OnAction;
            }
            catch
            {
                user.UnpairDevicesAndRemoveUser();
                UnityEngine.Object.Destroy(Actions);
                throw;
            }
        }

        private void OnAction(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;
            InputControl control;
            try
            {
                control = context.control;
            }
            catch (IndexOutOfRangeException)
            {
                // After binding overrides are re-resolved, the Input System can deliver a callback whose control index
                // no longer exists. The device is unknown for that event; prompt resolution keeps the previous device.
                return;
            }
            if (control != null)
                RecordUse(control.device);
        }

        // Records the device the player used; a different kind of device (gamepad after keyboard) changes the controls.
        internal void RecordUse(InputDevice device)
        {
            var previous = LastDevice;
            LastDevice = device;
            if (previous != null && device != null && Kind(previous) != Kind(device))
                ControlsChanged?.Invoke(this);
        }

        /// <summary>The kind of controls a device offers: keyboard and mouse count as one.</summary>
        internal static string Kind(InputDevice device) =>
            device is Keyboard || device is Mouse ? "KeyboardMouse"
            : device is Gamepad ? "Gamepad"
            : device is Touchscreen ? "Touch"
            : device is Joystick ? "Joystick"
            : device is Pen ? "Pen"
            : device.layout;

        /// <summary>Raised when the player starts using a different kind of device.</summary>
        internal Action<PlayerInputContext> ControlsChanged;

        /// <summary>Uses devices no other player owns, such as one connected after the player joined, until another player is assigned them.</summary>
        internal void Lend(params InputDevice[] devices)
        {
            if (disposed)
                return;
            foreach (var device in devices)
            {
                if (device == null)
                    continue;
                var owner = InputUser.FindUserPairedToDevice(device);
                if (owner.HasValue && owner.Value.id != user.id)
                    continue;
                if (!owner.HasValue)
                    user = InputUser.PerformPairingWithDevice(device, user);
                lent[device] = this;
            }
        }

        private void Return(InputDevice device)
        {
            lent.Remove(device);
            if (user.valid)
                user.UnpairDevice(device);
            if (LastDevice == device)
                LastDevice = null;
        }

        private void Forget()
        {
            var mine = new List<InputDevice>();
            foreach (var pair in lent)
                if (pair.Value == this)
                    mine.Add(pair.Key);
            foreach (var device in mine)
                lent.Remove(device);
        }

        private void CheckOpen()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PlayerInputContext));
        }

        /// <summary>Explicitly reassigns devices. Devices paired to another user are rejected rather than silently stolen.</summary>
        public void ReassignDevices(params InputDevice[] devices)
        {
            CheckOpen();
            CancelRebind();
            if (devices == null)
                throw new ArgumentNullException(nameof(devices));
            foreach (var device in devices)
            {
                if (device == null)
                    throw new ArgumentException("Device is null.");
                var previous = InputUser.FindUserPairedToDevice(device);
                if (previous.HasValue && previous.Value.id != user.id && !(lent.TryGetValue(device, out var borrower) && borrower.user.id == previous.Value.id))
                    throw new InvalidOperationException("Device belongs to another player.");
            }
            foreach (var device in devices)
                if (lent.TryGetValue(device, out var borrower) && borrower != this)
                    borrower.Return(device);
            Forget();
            user.UnpairDevices();
            foreach (var device in devices)
                user = InputUser.PerformPairingWithDevice(device, user);
            LastDevice = devices.Length > 0 ? devices[0] : null;
        }

        /// <summary>
        /// Enables <paramref name="maps"/> while the returned claim lives. The highest blocking priority suppresses only
        /// lower-priority claims; disposing a claim restores what it covered.
        /// </summary>
        public IDisposable Claim(int priority, bool blocksLower, params InputMapId[] maps)
        {
            CheckOpen();
            if (maps == null)
                throw new ArgumentNullException(nameof(maps));
            var names = new string[maps.Length];
            for (int i = 0; i < maps.Length; i++)
                names[i] = (maps[i].Find(Actions) ?? throw new ArgumentException("This player's actions have no map " + maps[i] + ".", nameof(maps))).name;
            return ClaimByName(priority, blocksLower, names);
        }

        internal IDisposable ClaimByName(int priority, bool blocksLower, params string[] maps)
        {
            CheckOpen();
            if (maps == null)
                throw new ArgumentNullException(nameof(maps));
            foreach (var map in maps)
                Actions.FindActionMap(map, true);
            var claim = new ClaimToken
            {
                Owner = this,
                Priority = priority,
                BlocksLower = blocksLower,
                Maps = (string[])maps.Clone(),
            };
            claims.Add(claim);
            Refresh();
            return claim;
        }

        private void Refresh()
        {
            if (disposed)
                return;
            int threshold = int.MinValue;
            foreach (var claim in claims)
                if (claim.BlocksLower)
                    threshold = Math.Max(threshold, claim.Priority);
            var enabled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var claim in claims)
                if (claim.Priority >= threshold)
                    foreach (var map in claim.Maps)
                        enabled.Add(map);
            foreach (var map in Actions.actionMaps)
            {
                if (enabled.Contains(map.name))
                    map.Enable();
                else
                    map.Disable();
            }
            rebindingAction?.Disable();
        }

        /// <summary>The control the player would use for <paramref name="action"/> on the device used last, such as "Space"
        /// or a glyph from <paramref name="glyph"/>.</summary>
        public string Prompt(InputId action, Func<InputControl, string> glyph = null) => PromptFor(Action(action), glyph);

        internal string PromptByName(string actionName, Func<InputControl, string> glyph = null)
        {
            CheckOpen();
            return PromptFor(Actions.FindAction(actionName, true), glyph);
        }

        private string PromptFor(InputAction action, Func<InputControl, string> glyph)
        {
            InputControl fallback = null;
            foreach (var control in action.controls)
            {
                bool paired = false;
                foreach (var device in user.pairedDevices)
                    if (control.device == device)
                    {
                        paired = true;
                        break;
                    }
                if (!paired)
                    continue;
                fallback ??= control;
                if (control.device == LastDevice)
                    return glyph?.Invoke(control) ?? control.displayName;
            }
            return fallback == null
                ? action.GetBindingDisplayString()
                : glyph?.Invoke(fallback) ?? fallback.displayName;
        }

        /// <summary>Exports this player's overrides; persistence is an optional game integration.</summary>
        public string ExportOverrides()
        {
            CheckOpen();
            return Actions.SaveBindingOverridesAsJson();
        }

        /// <summary>Selects an authored binding/control scheme without silently pairing additional devices.</summary>
        public void SelectControlScheme(string scheme)
        {
            CheckOpen();
            user.ActivateControlScheme(scheme);
            Refresh();
        }

        /// <summary>Imports overrides without changing other players or project assets.</summary>
        public void ImportOverrides(string json)
        {
            CheckOpen();
            if (rebinding != null)
                throw new InvalidOperationException("Finish rebinding first.");
            var previous = ExportOverrides();
            try
            {
                Actions.LoadBindingOverridesFromJson(json, true);
            }
            catch
            {
                Actions.LoadBindingOverridesFromJson(previous, true);
                throw;
            }
            finally
            {
                Refresh();
            }
        }

        /// <summary>Resets overrides to authored defaults.</summary>
        public void ResetBindings()
        {
            try
            {
                ResetCore();
            }
            finally
            {
                BindingsChanged?.Invoke(this);
            }
        }

        private void ResetCore()
        {
            CheckOpen();
            if (rebinding != null)
                throw new InvalidOperationException("Finish rebinding first.");
            Actions.RemoveAllBindingOverrides();
            Refresh();
        }

        /// <summary>Reports bindings of other actions that use the same control as this binding.</summary>
        public string[] GetConflicts(InputId action, int bindingIndex = 0) => ConflictsFor(Action(action), bindingIndex);

        internal string[] GetConflictsByName(string actionName, int bindingIndex)
        {
            CheckOpen();
            return ConflictsFor(Actions.FindAction(actionName, true), bindingIndex);
        }

        private string[] ConflictsFor(InputAction action, int bindingIndex)
        {
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            string path = action.bindings[bindingIndex].effectivePath;
            var result = new List<string>();
            foreach (var other in Actions)
                for (int i = 0; i < other.bindings.Count; i++)
                    if (
                        !(other == action && i == bindingIndex)
                        && !other.bindings[i].isComposite
                        && !string.IsNullOrEmpty(path)
                        && string.Equals(
                            path,
                            other.bindings[i].effectivePath,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        result.Add(other.bindings[i].id.ToString());
            return result.ToArray();
        }

        /// <summary>Explicit conflict resolution by clearing another binding. No overrides are silently reassigned.</summary>
        public void ClearConflictingBinding(Guid binding)
        {
            ClearCore(binding);
            BindingsChanged?.Invoke(this);
        }

        private void ClearCore(Guid binding)
        {
            CheckOpen();
            foreach (var action in Actions)
                for (int i = 0; i < action.bindings.Count; i++)
                    if (action.bindings[i].id == binding)
                    {
                        action.ApplyBindingOverride(i, string.Empty);
                        return;
                    }
            throw new ArgumentException("Binding identity is not owned by this player.");
        }

        /// <summary>Rebinds one binding or each part of a composite, supports cancellation and reports conflicts.</summary>
        public UniTask<string[]> RebindAsync(InputId action, int bindingIndex = 0, CancellationToken token = default)
        {
            var resolved = Action(action);
            return RebindAndNotify(resolved.actionMap.name + "/" + resolved.name, bindingIndex, token);
        }

        private async UniTask<string[]> RebindAndNotify(string actionName, int bindingIndex, CancellationToken token)
        {
            var conflicts = await Rebind(actionName, bindingIndex, token);
            BindingsChanged?.Invoke(this);
            return conflicts;
        }

        /// <summary>Raised after a rebind, reset or conflict removal changes this player's bindings.</summary>
        internal Action<PlayerInputContext> BindingsChanged;

        private Dictionary<Guid, InputAction> byId;

        /// <summary>
        /// This player's copy of an action, by ID or, for actions authored in another asset with the same names, by its
        /// "Map/Action" path. Throws when the player's actions contain neither.
        /// </summary>
        public InputAction Action(InputId action)
        {
            CheckOpen();
            if (byId == null)
            {
                byId = new Dictionary<Guid, InputAction>();
                foreach (var each in Actions)
                    byId[each.id] = each;
            }
            if (byId.TryGetValue(action.Value, out var found))
                return found;
            found = action.Find(Actions) ?? throw new ArgumentException("This player's actions have no action " + action + ".", nameof(action));
            byId[action.Value] = found;
            return found;
        }

        /// <summary>Whether the action was pressed this frame.</summary>
        public bool WasPressed(InputId action) => Action(action).WasPressedThisFrame();

        /// <summary>Whether the action was released this frame.</summary>
        public bool WasReleased(InputId action) => Action(action).WasReleasedThisFrame();

        /// <summary>Whether the action is held.</summary>
        public bool IsPressed(InputId action) => Action(action).IsPressed();

        /// <summary>Reads the action's current value; the ID carries its type, such as <c>InputId&lt;Vector2&gt;</c>.</summary>
        public T Read<T>(InputId<T> action)
            where T : struct => Action(action).ReadValue<T>();

        internal UniTask<string[]> Rebind(
            string actionName,
            int bindingIndex,
            CancellationToken token = default
        )
        {
            CheckOpen();
            token.ThrowIfCancellationRequested();
            if (!Available)
                throw new InvalidOperationException("No paired device is currently available.");
            if (rebinding != null)
                throw new InvalidOperationException("One rebind per player may run at a time.");
            var action = Actions.FindAction(actionName, true);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            var indices = new List<int>();
            if (action.bindings[bindingIndex].isComposite)
            {
                for (
                    int i = bindingIndex + 1;
                    i < action.bindings.Count && action.bindings[i].isPartOfComposite;
                    i++
                )
                    indices.Add(i);
            }
            else
                indices.Add(bindingIndex);
            if (indices.Count == 0)
                throw new ArgumentException("Composite has no parts.");
            originalOverrides = ExportOverrides();
            rebindResult = new UniTaskCompletionSource<string[]>();
            var result = rebindResult.Task;
            var expectedResult = rebindResult;
            rebindingAction = action;
            action.Disable();
            var context = SynchronizationContext.Current;
            int ownerThread = Environment.CurrentManagedThreadId;
            rebindingCancellation = token.Register(() =>
            {
                if (Environment.CurrentManagedThreadId == ownerThread)
                {
                    if (ReferenceEquals(rebindResult, expectedResult))
                        CancelRebind();
                }
                else if (context != null)
                    context.Post(
                        _ =>
                        {
                            if (ReferenceEquals(rebindResult, expectedResult))
                                CancelRebind();
                        },
                        null
                    );
            });
            int step = 0;
            void Begin()
            {
                rebinding = action
                    .PerformInteractiveRebinding(indices[step])
                    .OnCancel(operation => Finish(true))
                    .OnComplete(operation =>
                    {
                        if (operation.selectedControl != null)
                            RecordUse(operation.selectedControl.device);
                        operation.Dispose();
                        rebinding = null;
                        step++;
                        if (step < indices.Count)
                            Begin();
                        else
                            Finish(false);
                    });
                foreach (var device in user.pairedDevices)
                    rebinding.WithControlsHavingToMatchPath(device.path + "/*");
                rebinding.Start();
                if (token.IsCancellationRequested)
                    CancelRebind();
            }
            void Finish(bool cancelled)
            {
                var completion = rebindResult;
                rebindResult = null;
                rebinding?.Dispose();
                rebinding = null;
                rebindingAction = null;
                rebindingCancellation.Dispose();
                if (cancelled)
                    Actions.LoadBindingOverridesFromJson(originalOverrides, true);
                Refresh();
                if (cancelled)
                    completion.TrySetCanceled();
                else
                {
                    var conflicts = new HashSet<string>();
                    foreach (int index in indices)
                    foreach (string conflict in GetConflictsByName(actionName, index))
                        conflicts.Add(conflict);
                    var array = new string[conflicts.Count];
                    conflicts.CopyTo(array);
                    completion.TrySetResult(array);
                }
            }
            try
            {
                Begin();
            }
            catch
            {
                rebinding?.Dispose();
                rebinding = null;
                rebindingAction = null;
                rebindingCancellation.Dispose();
                rebindResult = null;
                Refresh();
                throw;
            }
            return result;
        }

        /// <summary>Cancels the current rebind and restores the original overrides.</summary>
        public void CancelRebind() => rebinding?.Cancel();

        /// <summary>Leaves the session, cancelling rebinds, removing claims and unpairing only this player's devices.</summary>
        public void Dispose()
        {
            if (disposed)
                return;
            CancelRebind();
            disposed = true;
            Forget();
            foreach (var claim in claims)
                claim.Owner = null;
            claims.Clear();
            foreach (var map in Actions.actionMaps)
                map.actionTriggered -= OnAction;
            Actions.Disable();
            if (user.valid)
                user.UnpairDevicesAndRemoveUser();
            // Services may dispose contexts after leaving Play mode, where Destroy is not allowed.
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(Actions);
            else
                UnityEngine.Object.DestroyImmediate(Actions);
        }
    }
}
