namespace Threadlink.Editor.CodeGen
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Shared;
    using UnityEditor;

    internal static class IrisEventsCodeGen
    {
        private const string PLACEHOLDER = "{User-Defined Event IDs}";

        [MenuItem("Threadlink/Run Iris Events CodeGen")]
        private static void RunIrisEventsCodeGen()
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig))
                return;

            if (EnumCodeGen.TryGenerateEnum(editorConfig.NativeIrisEventsTemplate,
            editorConfig.UserIrisEventsTemplate, editorConfig.IrisEventsScript, PLACEHOLDER))
            {
                Scribe.Send<Threadlink>("Events CodeGen finished!").ToUnityConsole(DebugType.Info);
            }
            else Scribe.Send<Threadlink>("Could not run Iris Events CodeGen!").ToUnityConsole(DebugType.Error);
        }
    }
}