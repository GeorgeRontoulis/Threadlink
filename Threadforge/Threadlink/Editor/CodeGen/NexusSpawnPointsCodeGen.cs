namespace Threadlink.Editor.CodeGen
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Shared;
    using UnityEditor;

    internal static class NexusSpawnPointsCodeGen
    {
        private const string PLACEHOLDER = "{User-Defined Spawn Point IDs}";

        [MenuItem("Threadlink/CodeGen/Run Nexus Spawn Points CodeGen")]
        internal static void RunNexusSpawnPointsCodeGen()
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out ThreadlinkEditorConfig editorConfig))
                return;

            if (EnumCodeGen.TryGenerateEnum(editorConfig.NativeNexusSpawnPointsTemplate,
            editorConfig.UserNexusSpawnPointsTemplate, editorConfig.NexusSpawnPointsScript, PLACEHOLDER))
            {
                Scribe.Send<Threadlink>("Events CodeGen finished!").ToUnityConsole(DebugType.Info);
            }
            else Scribe.Send<Threadlink>("Could not run Iris Events CodeGen!").ToUnityConsole(DebugType.Error);
        }
    }
}