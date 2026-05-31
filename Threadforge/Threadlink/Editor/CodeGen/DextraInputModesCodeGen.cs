namespace Threadlink.Editor.CodeGen
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Shared;
    using UnityEditor;

    internal static class DextraInputModesCodeGen
    {
        private const string PLACEHOLDER = "{User-Defined Input Modes}";

        [MenuItem("Threadlink/CodeGen/Run Dextra Input Modes CodeGen")]
        internal static void Run()
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig))
                return;

            if (EnumCodeGen.TryGenerateEnum(editorConfig.NativeDextraModesTemplate,
            editorConfig.UserDextraModesTemplate, editorConfig.DextraModesScript, PLACEHOLDER))
            {
                Scribe.Send<Threadlink>("Dextra Input Modes CodeGen finished!").ToUnityConsole(DebugType.Info);
            }
            else Scribe.Send<Threadlink>("Could not run Dextra Input Modes CodeGen!").ToUnityConsole(DebugType.Error);
        }
    }
}