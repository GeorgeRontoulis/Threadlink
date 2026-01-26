namespace Threadlink.Editor
{
    using UnityEditor;
    using UnityEngine;

    public sealed class PopupInspector : EditorWindow
    {
        private Object Asset { get; set; }
        private Editor AssetEditor { get; set; }

        public static bool TryInspect(Object asset)
        {
            if (asset == null) return false;

            var window = GetWindow<PopupInspector>();

            if (window == null)
                window = CreateWindow<PopupInspector>();

            window.titleContent = new(asset.name, string.Empty);
            window.Asset = asset;
            window.AssetEditor = Editor.CreateEditor(asset);

            return window.AssetEditor != null;
        }

        private void OnGUI()
        {
            if (Asset == null || AssetEditor == null)
                return;

            AssetEditor.OnInspectorGUI();
        }

        private void OnDestroy()
        {
            Asset = null;
            AssetEditor = null;
        }
    }
}
