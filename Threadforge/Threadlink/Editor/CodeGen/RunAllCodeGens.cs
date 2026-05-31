namespace Threadlink.Editor.CodeGen
{
    using UnityEditor;

    internal static class AllCodeGens
    {
        [MenuItem("Threadlink/Run All CodeGens")]
        private static void RunAllCodeGens()
        {
            AddressableIDsCodeGen.Run();
            IrisEventsCodeGen.Run();
            NexusSpawnPointsCodeGen.Run();
            RNGDomainsCodeGen.Run();
            VaultFieldIDsCodeGen.Run();
            DextraInputModesCodeGen.Run();
        }
    }
}