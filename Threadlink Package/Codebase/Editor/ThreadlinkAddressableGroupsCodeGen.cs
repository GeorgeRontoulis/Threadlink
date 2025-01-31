namespace Threadlink.Editor
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using UnityEditor;
	using UnityEditor.AddressableAssets;
	using UnityEngine;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Threadlink.Editor.Attributes;
#endif

	[CreateAssetMenu(menuName = "Threadlink/Core/Addressable Groups Generator")]
	internal sealed class ThreadlinkAddressableGroupsCodeGen : ScriptableObject
	{
#if ODIN_INSPECTOR || THREADLINK_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField] private TextAsset template = null;

#if ODIN_INSPECTOR || THREADLINK_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField] private MonoScript pointersScript = null;

		[Space(10)]

#if ODIN_INSPECTOR || THREADLINK_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField]
		private List<string> discoveredAddressableGroups = new();

#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Generate Custom Event Signatures")]
#endif
#pragma warning disable IDE0051
		private void GenerateAddressableGroupSignatures()
		{
			string templateContent = template.text;
			string separator = "," + Environment.NewLine;

			var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);

			if (settings == null) throw new NullReferenceException("There is no Addressable Settings asset in the project!");

			var groups = settings.groups;

			discoveredAddressableGroups.Clear();

			for (int i = 0; i < groups.Count; i++)
			{
				var groupName = groups[i].name;

				if (groupName.Equals("Built In Data") || groupName.Contains("Localization")) continue;

				discoveredAddressableGroups.Add(groupName.Replace(" ", string.Empty).Replace("-", "_"));
			}

			templateContent = templateContent.Replace("{CustomEntries}", string.Join(separator, discoveredAddressableGroups));

			File.WriteAllText(string.Join("/", Path.GetDirectoryName(AssetDatabase.GetAssetPath(pointersScript)), "ThreadlinkAddressableGroups.cs"),
			CSharpier.CodeFormatter.Format(templateContent).Code);

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();
		}
	}
}
