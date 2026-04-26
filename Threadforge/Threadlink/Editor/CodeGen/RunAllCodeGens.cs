namespace Threadlink.Editor.CodeGen
{
    using UnityEditor;

    internal static class AllCodeGens
    {
        [MenuItem("Threadlink/CodeGen/Run All CodeGens")]
        private static void RunAllCodeGens()
        {
            AddressableIDsCodeGen.RunAddressablesCodeGen();
            IrisEventsCodeGen.RunIrisEventsCodeGen();
            NexusSpawnPointsCodeGen.RunNexusSpawnPointsCodeGen();
            RNGDomainsCodeGen.RunRNGDomainsCodeGen();
            VaultFieldIDsCodeGen.RunVaultFieldsCodeGen();
        }
    }
}