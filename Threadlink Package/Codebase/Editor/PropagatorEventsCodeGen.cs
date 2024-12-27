namespace Threadlink.Editor
{
	using System;
	using System.IO;
	using UnityEditor;
	using UnityEngine;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Threadlink.Editor.Attributes;
#endif

	[CreateAssetMenu(menuName = "Threadlink/Propagator/Custom Events Generator")]
	internal sealed class PropagatorEventsCodeGen : ScriptableObject
	{
#if ODIN_INSPECTOR || THREADLINK_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField] private TextAsset template = null;

#if ODIN_INSPECTOR || THREADLINK_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField] private MonoScript propagatorScript = null;

		[Space(10)]

		[SerializeField] private string[] customEventSignatures = new string[0];

#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Generate Custom Event Signatures")]
#endif
#pragma warning disable IDE0051
		private void GenerateCustomEventSignatures()
		{
			string templateContent = template.text;
			string separator = "," + Environment.NewLine;
			templateContent = templateContent.Replace("{CustomEntries}", string.Join(separator, customEventSignatures));

			File.WriteAllText(string.Join("/", Path.GetDirectoryName(AssetDatabase.GetAssetPath(propagatorScript)), "PropagatorEvents.cs"),
			CSharpier.CodeFormatter.Format(templateContent).Code);

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();
		}
	}
}
