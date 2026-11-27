namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine.InputSystem;

    /// <summary>
    /// A type-safe reference to an input action, by its stable action ID. Generated as <c>GameInput.Map.Action</c> from the
    /// project's input actions (Project Settings ▸ Threadlink ▸ Dextra); renaming an action keeps its ID.
    /// </summary>
    public readonly struct InputId : IEquatable<InputId>
    {
        /// <summary>The action's ID.</summary>
        public readonly Guid Value;

        /// <summary>"Map/Action", for messages and diagnostics.</summary>
        public readonly string Path;

        /// <summary>Creates an ID. Generated code calls this.</summary>
        public InputId(Guid value, string path)
        {
            Value = value;
            Path = path;
        }

        /// <summary>
        /// The action in <paramref name="actions"/>: by ID, or by "Map/Action" path when the asset was authored separately
        /// with the same names. Null when it has neither.
        /// </summary>
        public InputAction Find(InputActionAsset actions)
        {
            if (actions == null)
                return null;
            var found = Value != Guid.Empty ? actions.FindAction(Value) : null;
            return found ?? (string.IsNullOrEmpty(Path) ? null : actions.FindAction(Path));
        }

        /// <summary>The ID of an action created or loaded at runtime.</summary>
        public static InputId Of(InputAction action) =>
            action != null ? new InputId(action.id, action.actionMap != null ? action.actionMap.name + "/" + action.name : action.name) : throw new ArgumentNullException(nameof(action));

        /// <inheritdoc />
        public bool Equals(InputId other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object other) => other is InputId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => Value.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => Path ?? Value.ToString("D");
    }

    /// <summary>An input action whose value is <typeparamref name="T"/>, such as <c>InputId&lt;Vector2&gt;</c> for movement.</summary>
    public readonly struct InputId<T> : IEquatable<InputId<T>>
        where T : struct
    {
        /// <summary>The action's ID.</summary>
        public readonly Guid Value;

        /// <summary>"Map/Action", for messages and diagnostics.</summary>
        public readonly string Path;

        /// <summary>Creates an ID. Generated code calls this.</summary>
        public InputId(Guid value, string path)
        {
            Value = value;
            Path = path;
        }

        /// <summary>The ID of an action created or loaded at runtime.</summary>
        public static InputId<T> Of(InputAction action)
        {
            var id = InputId.Of(action);
            return new InputId<T>(id.Value, id.Path);
        }

        /// <summary>Every typed action is also an action.</summary>
        public static implicit operator InputId(InputId<T> id) => new InputId(id.Value, id.Path);

        /// <inheritdoc />
        public bool Equals(InputId<T> other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object other) => other is InputId<T> id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => Value.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => Path ?? Value.ToString("D");
    }

    /// <summary>A type-safe reference to an action map, generated as <c>GameInput.Maps.Name</c>.</summary>
    public readonly struct InputMapId : IEquatable<InputMapId>
    {
        /// <summary>The map's ID.</summary>
        public readonly Guid Value;

        /// <summary>The map's name, for messages.</summary>
        public readonly string Name;

        /// <summary>Creates an ID. Generated code calls this.</summary>
        public InputMapId(Guid value, string name)
        {
            Value = value;
            Name = name;
        }

        /// <summary>The map in <paramref name="actions"/>: by ID, or by name when the asset was authored separately. Null when it has neither.</summary>
        public InputActionMap Find(InputActionAsset actions)
        {
            if (actions == null)
                return null;
            var found = Value != Guid.Empty ? actions.FindActionMap(Value) : null;
            return found ?? (string.IsNullOrEmpty(Name) ? null : actions.FindActionMap(Name));
        }

        /// <summary>The ID of a map created or loaded at runtime.</summary>
        public static InputMapId Of(InputActionMap map) => map != null ? new InputMapId(map.id, map.name) : throw new ArgumentNullException(nameof(map));

        /// <inheritdoc />
        public bool Equals(InputMapId other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object other) => other is InputMapId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => Value.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => Name ?? Value.ToString("D");
    }

    /// <summary>Marks a string field that stores an action map of the project's input actions; the Inspector shows a dropdown.</summary>
    public sealed class InputMapReferenceAttribute : UnityEngine.PropertyAttribute { }
}
