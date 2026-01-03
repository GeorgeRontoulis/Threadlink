namespace Threadlink.Animation
{
    #region Editor Code:
#if UNITY_EDITOR
    using UnityEditor;
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif
#endif
    #endregion

    using UnityEngine;

    [CreateAssetMenu(menuName = "Threadlink/Animation/Animator Hash")]
    public sealed class AnimatorHash : ScriptableObject
    {
        public int Value => hashValue;

        #region Editor Code:
#if UNITY_EDITOR
        private bool DrawExtras => !string.IsNullOrEmpty(stringValue);

        [SerializeField] private string stringValue = string.Empty;

#if ODIN_INSPECTOR
        [ShowIf(nameof(DrawExtras)), ReadOnly]
#endif
#endif
        #endregion
        [SerializeField] private int hashValue = 0;

        #region Editor Code:
#if UNITY_EDITOR
#pragma warning disable IDE0051
#if ODIN_INSPECTOR
        [ShowIf(nameof(DrawExtras)), Button]
#else
		[ContextMenu("Generate Hash")]
#endif
        private void GenerateHash()
        {
            hashValue = Animator.StringToHash(stringValue);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            AssetDatabase.Refresh();
        }
#endif
        #endregion
    }
}