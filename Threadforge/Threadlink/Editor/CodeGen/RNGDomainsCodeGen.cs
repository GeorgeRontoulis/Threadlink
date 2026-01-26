namespace Threadlink.Editor.CodeGen
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Shared;
    using UnityEditor;

    internal static class RNGDomainsCodeGen
    {
        private const string PLACEHOLDER = "{User-Defined RNG Domains}";

        [MenuItem("Threadlink/CodeGen/Run RNG Domains CodeGen")]
        private static void RunRNGDomainsCodeGen()
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig))
                return;

            if (EnumCodeGen.TryGenerateEnum(editorConfig.NativeRNGDomainsTemplate,
            editorConfig.UserRNGDomainsTemplate, editorConfig.RNGDomainsScript, PLACEHOLDER))
            {
                Scribe.Send<Threadlink>("RNG Domains CodeGen finished!").ToUnityConsole(DebugType.Info);
            }
            else Scribe.Send<Threadlink>("Could not run RNG Domains CodeGen!").ToUnityConsole(DebugType.Error);
        }
    }
}