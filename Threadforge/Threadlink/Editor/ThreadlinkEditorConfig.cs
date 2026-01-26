namespace Threadlink.Editor
{
    using UnityEditor;
    using UnityEngine;

    [CreateAssetMenu(fileName = "ThreadlinkConfig.Editor.asset", menuName = "Threadlink/Editor Config")]
    internal sealed class ThreadlinkEditorConfig : ScriptableObject
    {
        internal TextAsset NativeIrisEventsTemplate => nativeIrisEventsTemplate;
        internal TextAsset UserIrisEventsTemplate => userIrisEventsTemplate;
        internal MonoScript IrisEventsScript => irisEventsScript;

        internal TextAsset NativeRNGDomainsTemplate => nativeRNGDomainsTemplate;
        internal TextAsset UserRNGDomainsTemplate => userRNGDomainsTemplate;
        internal MonoScript RNGDomainsScript => rngDomainsScript;

        internal TextAsset NativeVaultFieldsTemplate => nativeVaultFieldsTemplate;
        internal TextAsset UserVaultFieldsTemplate => userVaultFieldsTemplate;
        internal MonoScript VaultFieldsScript => vaultFieldsScript;

        internal TextAsset SceneIDsTemplate => sceneIDsTemplate;
        internal TextAsset AssetIDsTemplate => assetIDsTemplate;
        internal TextAsset PrefabIDsTemplate => prefabIDsTemplate;

        internal MonoScript SceneIDsScript => sceneIDsScript;
        internal MonoScript AssetIDsScript => assetIDsScript;
        internal MonoScript PrefabIDsScript => prefabIDsScript;

        [Header("Editor Resources:")]
        [Space(10)]

        [SerializeField] private TextAsset nativeIrisEventsTemplate = null;
        [SerializeField] private TextAsset userIrisEventsTemplate = null;
        [SerializeField] private MonoScript irisEventsScript = null;

        [Space(10)]

        [SerializeField] private TextAsset nativeRNGDomainsTemplate = null;
        [SerializeField] private TextAsset userRNGDomainsTemplate = null;
        [SerializeField] private MonoScript rngDomainsScript = null;

        [Space(10)]

        [SerializeField] private TextAsset nativeVaultFieldsTemplate = null;
        [SerializeField] private TextAsset userVaultFieldsTemplate = null;
        [SerializeField] private MonoScript vaultFieldsScript = null;

        [Space(10)]

        [SerializeField] private TextAsset sceneIDsTemplate = null;
        [SerializeField] private TextAsset assetIDsTemplate = null;
        [SerializeField] private TextAsset prefabIDsTemplate = null;

        [Space(10)]

        [SerializeField] private MonoScript sceneIDsScript = null;
        [SerializeField] private MonoScript assetIDsScript = null;
        [SerializeField] private MonoScript prefabIDsScript = null;
    }
}
