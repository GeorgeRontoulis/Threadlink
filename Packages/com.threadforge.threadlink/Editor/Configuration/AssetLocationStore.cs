namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>The project's folder choices for <see cref="AssetLocations"/>, kept in ProjectSettings.</summary>
    [FilePath(
        "ProjectSettings/ThreadlinkAssetLocations.asset",
        FilePathAttribute.Location.ProjectFolder
    )]
    internal sealed class AssetLocationStore : ScriptableSingleton<AssetLocationStore>
    {
        [Serializable]
        private struct Entry
        {
            public string Id;
            public string Folder;
        }

        [SerializeField]
        private string engineering = AssetLocations.DefaultEngineeringFolder;

        [SerializeField]
        private string design = AssetLocations.DefaultDesignFolder;

        [SerializeField]
        private List<Entry> overrides = new List<Entry>();

        public string Engineering =>
            string.IsNullOrEmpty(engineering)
                ? AssetLocations.DefaultEngineeringFolder
                : engineering;

        public string Design =>
            string.IsNullOrEmpty(design) ? AssetLocations.DefaultDesignFolder : design;

        public string Override(string id)
        {
            foreach (var entry in overrides)
                if (entry.Id == id && !string.IsNullOrEmpty(entry.Folder))
                    return entry.Folder;
            return null;
        }

        public void SetRoot(ConfigRoot root, string folder)
        {
            string value = string.IsNullOrWhiteSpace(folder)
                ? null
                : AssetLocations.Normalize(folder);
            if (root == ConfigRoot.Design)
                design = value ?? AssetLocations.DefaultDesignFolder;
            else
                engineering = value ?? AssetLocations.DefaultEngineeringFolder;
            Commit();
        }

        public void SetOverride(string id, string folder)
        {
            overrides.RemoveAll(entry => entry.Id == id);
            if (folder != null)
                overrides.Add(new Entry { Id = id, Folder = folder });
            overrides.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            Commit();
        }

        private void Commit()
        {
            Save(true);
            AssetLocations.RaiseChanged();
        }
    }
}
