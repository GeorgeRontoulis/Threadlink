#if ODIN_INSPECTOR && THREADLINK_TIMELINE
namespace Threadlink.Editor
{
    using Sirenix.OdinInspector.Editor;
    using UnityEditor;
    using Vault;

    [CustomEditor(typeof(VaultMarker))]
    internal sealed class VaultMarkerEditor : Editor
    {
        private PropertyTree odinTree;

        private void OnEnable()
        {
            odinTree = PropertyTree.Create(targets);
        }

        private void OnDisable()
        {
            odinTree?.Dispose();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            const string CONFIG_NAME = "configuration";
            const string OPT_NAME = "options";

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.name == OPT_NAME || iterator.name == CONFIG_NAME)
                    continue;

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            odinTree.BeginDraw(true);

            odinTree.GetPropertyAtPath(OPT_NAME)?.Draw();
            odinTree.GetPropertyAtPath(CONFIG_NAME)?.Draw();

            odinTree.EndDraw();
        }
    }
}
#endif