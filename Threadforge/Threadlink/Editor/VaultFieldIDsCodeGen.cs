namespace Threadlink.Editor.CodeGen
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Shared;
    using UnityEditor;

    internal static class VaultFieldIDsCodeGen
    {
        private const string PLACEHOLDER = "{User-Defined Data Field IDs}";

        [MenuItem("Threadlink/Run Vault Fields CodeGen")]
        private static void RunVaultFieldsCodeGen()
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig))
                return;

            if (EnumCodeGen.TryGenerateEnum(editorConfig.NativeVaultFieldsTemplate,
            editorConfig.UserVaultFieldsTemplate, editorConfig.VaultFieldsScript, PLACEHOLDER))
            {
                Scribe.Send<Threadlink>("Vault Fields CodeGen finished!").ToUnityConsole(DebugType.Info);
            }
            else Scribe.Send<Threadlink>("Could not run Vault Fields CodeGen!").ToUnityConsole(DebugType.Error);
        }
    }
}