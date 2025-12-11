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

        internal TextAsset AssetGroupsTemplate => assetGroupsTemplate;

        [Header("Editor Resources:")]
        [Space(10)]

        [SerializeField] private TextAsset nativeIrisEventsTemplate = null;
        [SerializeField] private TextAsset userIrisEventsTemplate = null;
        [SerializeField] private MonoScript irisEventsScript = null;

        [Space(10)]

        [SerializeField] private TextAsset assetGroupsTemplate = null;
    }
}
