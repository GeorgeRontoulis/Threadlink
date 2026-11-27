namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    /// <summary>
    /// Generates <c>GameScenes</c>: a <see cref="SceneId"/> per Build Profile scene, keyed by the scene asset's GUID, and
    /// registers their current paths with Nexus at subsystem registration.
    /// </summary>
    public sealed class SceneIdGenerator : IIdGenerator
    {
        /// <inheritdoc />
        public string Title => "Scenes";

        /// <inheritdoc />
        public string Source => "Scenes in the active Build Profile";

        /// <inheritdoc />
        public System.Collections.Generic.IEnumerable<string> Outputs => new[] { "GameScenes" };

        /// <inheritdoc />
        public void OpenSource() => UnityEditor.EditorApplication.ExecuteMenuItem("File/Build Profiles");

        /// <inheritdoc />
        public bool Generate()
        {
            var members = new List<GeneratedIds.Member>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scene in EditorBuildSettings.scenes)
            {
                string guid = scene.guid.ToString();
                if (string.IsNullOrEmpty(scene.path) || string.IsNullOrEmpty(guid) || guid == "00000000000000000000000000000000")
                    continue;
                string name = GeneratedIds.Identifier(Path.GetFileNameWithoutExtension(scene.path));
                if (!used.Add(name))
                    name = GeneratedIds.Identifier(Path.ChangeExtension(scene.path, null));
                var id = new Guid(guid);
                members.Add(
                    new GeneratedIds.Member
                    {
                        Name = name,
                        Key = id.ToString("D"),
                        Type = "global::Threadforge.Threadlink.SceneId",
                        Value = "new global::Threadforge.Threadlink.SceneId(new global::System.Guid(\"" + id.ToString("D") + "\"), " + GeneratedIds.Quote(scene.path) + ")",
                        Summary = scene.path + (scene.enabled ? string.Empty : " (disabled in the Build Profile)"),
                    }
                );
            }
            var footer = new StringBuilder();
            footer.AppendLine();
            footer.AppendLine("    [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]");
            footer.AppendLine("    private static void RegisterScenes() => global::Threadforge.Threadlink.Nexus.AddScenes(new[] { " + string.Join(", ", members.Select(member => member.Name)) + " });");
            return GeneratedIds.Write("GameScenes", "Build Profile scene IDs.", members, members.Count > 0 ? footer.ToString() : null);
        }
    }

    /// <summary>Shows <see cref="SceneReference"/> fields as a scene asset picker.</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    internal sealed class SceneReferenceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var guid = property.FindPropertyRelative("guid");
            var path = property.FindPropertyRelative("path");
            var field = new ObjectField(property.displayName)
            {
                objectType = typeof(SceneAsset),
                allowSceneObjects = false,
                tooltip = string.IsNullOrEmpty(property.tooltip) ? "Scene to load. Add it to the Build Profile." : property.tooltip,
            };
            field.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
            string current = AssetDatabase.GUIDToAssetPath(guid.stringValue);
            field.value = string.IsNullOrEmpty(current) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(current);
            if (!string.IsNullOrEmpty(current) && current != path.stringValue)
            {
                path.stringValue = current;
                property.serializedObject.ApplyModifiedProperties();
            }
            field.RegisterValueChangedCallback(change =>
            {
                string chosen = change.newValue != null ? AssetDatabase.GetAssetPath(change.newValue) : string.Empty;
                guid.stringValue = string.IsNullOrEmpty(chosen) ? string.Empty : new Guid(AssetDatabase.AssetPathToGUID(chosen)).ToString("D");
                path.stringValue = chosen;
                property.serializedObject.ApplyModifiedProperties();
            });
            return field;
        }
    }
}
