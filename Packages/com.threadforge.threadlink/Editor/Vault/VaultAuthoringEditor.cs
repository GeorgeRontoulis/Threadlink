namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Typed-definition validation, override provenance and explicit migration through UI Toolkit.</summary>
    [CustomEditor(typeof(VaultAuthoring), true)]
    public sealed class VaultAuthoringEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var definition = (VaultAuthoring)target;
            var status = new Label();
            root.Add(new Label("Identity: " + definition.Identity));
            root.Add(new Label("Schema: " + definition.Schema));
            root.Add(
                new Label(
                    "Authored version: "
                        + definition.AuthoredVersion
                        + " / schema "
                        + definition.SchemaVersion
                )
            );
            root.Add(status);
            root.Add(
                new Button(() =>
                {
                    try
                    {
                        definition.ValidateDefinition();
                        status.text = "Vault asset is valid.";
                    }
                    catch (Exception error)
                    {
                        status.text = error.Message;
                    }
                })
                {
                    text = "Validate Vault Asset",
                }
            );
            root.Add(
                new Button(() =>
                {
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();
                    Undo.RegisterCompleteObjectUndo(target, "Migrate definition");
                    try
                    {
                        definition.ApplyAuthoringMigration();
                        EditorUtility.SetDirty(target);
                        AssetDatabase.SaveAssetIfDirty(target);
                        serializedObject.Update();
                        status.text = "Authoring migration completed.";
                    }
                    catch (Exception error)
                    {
                        Undo.RevertAllDownToGroup(group);
                        status.text = error.Message;
                    }
                })
                {
                    text = "Apply Explicit Authoring Migration",
                }
            );
            if (serializedObject.FindProperty("parent") != null)
                root.Add(
                    new Button(() =>
                    {
                        string path = AssetDatabase.GetAssetPath(target);
                        if (string.IsNullOrEmpty(path))
                        {
                            status.text = "Save the source asset first.";
                            return;
                        }
                        var variant = (VaultAuthoring)
                            ScriptableObject.CreateInstance(target.GetType());
                        string output = AssetDatabase.GenerateUniqueAssetPath(
                            Path.GetDirectoryName(path) + "/" + target.name + " Variant.asset"
                        );
                        AssetDatabase.CreateAsset(variant, output);
                        var data = new SerializedObject(variant);
                        data.FindProperty("parent").objectReferenceValue = target;
                        data.ApplyModifiedPropertiesWithoutUndo();
                        AssetDatabase.SaveAssetIfDirty(variant);
                        Selection.activeObject = variant;
                    })
                    {
                        text = "Create Typed Variant",
                    }
                );
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            var origins = new VisualElement();
            root.Add(origins);
            void RefreshOrigins()
            {
                origins.Clear();
                var iterator = serializedObject.GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (!iterator.type.StartsWith("VaultOverride", StringComparison.Ordinal))
                        continue;
                    var enabled = iterator.FindPropertyRelative("Enabled");
                    if (enabled == null)
                        continue;
                    string path = iterator.propertyPath;
                    try
                    {
                        origins.Add(
                            new Label(
                                iterator.displayName + " origin: " + FindOrigin(definition, path)
                            )
                        );
                    }
                    catch (Exception error)
                    {
                        origins.Add(new Label(error.Message));
                    }
                    origins.Add(
                        new Button(() =>
                        {
                            Undo.RecordObject(target, "Reset definition override");
                            serializedObject
                                .FindProperty(path)
                                .FindPropertyRelative("Enabled")
                                .boolValue = false;
                            serializedObject.ApplyModifiedProperties();
                        })
                        {
                            text = "Inherit " + iterator.displayName,
                        }
                    );
                }
            }
            RefreshOrigins();
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshOrigins());
            return root;
        }

        private static Guid FindOrigin(VaultAuthoring source, string property)
        {
            var seen = new HashSet<VaultAuthoring>();
            while (source != null)
            {
                if (!seen.Add(source))
                    throw new InvalidOperationException(
                        "Variant cycle prevents origin resolution."
                    );
                var serialized = new SerializedObject(source);
                var field = serialized.FindProperty(property)?.FindPropertyRelative("Enabled");
                if (field == null || field.boolValue || source.ParentSource == null)
                    return source.Identity;
                source = source.ParentSource;
            }
            throw new InvalidOperationException("Definition origin is unavailable.");
        }
    }
}
