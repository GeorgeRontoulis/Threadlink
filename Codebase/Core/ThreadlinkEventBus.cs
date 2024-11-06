namespace Threadlink.Core
{
#if UNITY_EDITOR
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif
	using UnityEngine;
	using UnityEditor;
	using Utilities.Editor;
	using Utilities.Collections;
	using Utilities.Text;
	using System;
	using Utilities.UnityLogging;
	using System.IO;
	using CSharpier;

#endif
	/// <summary>
	/// Global bus that generates and stores events compatible with the Threadlink Framework.
	/// The event bus offers an intuitive workflow for declaring, organizing and invoking events,
	/// reducing the complexity of having to keep track of multiple references across the 
	/// project and eliminating race conditions that arise with event subscriptions.
	/// </summary>
	[CreateAssetMenu(menuName = "Threadlink/Event Bus")]
	public partial class ThreadlinkEventBus : ScriptableObject
	{
#if UNITY_EDITOR
		private static readonly string NewLine = Environment.NewLine;
#if ODIN_INSPECTOR
		[DictionaryDrawerSettings(KeyLabel = "Event Names", ValueLabel = "Event Types", KeyColumnWidth = 250)]
#endif
		[SerializeField] private SerializedDictionary<string, string> eventsRegistry = new();

		[Space(10)]

		[SerializeField] private string[] necessaryUsings = new string[0];

		[Space(10)]

#if ODIN_INSPECTOR
		[ReadOnly]
#endif
		[SerializeField] private TextAsset codeGenTemplate = null;

#if ODIN_INSPECTOR
		[PropertySpace(10)]
		[Button]
#else
		[ContextMenu("Generate Events Script File")]
#endif
#pragma warning disable IDE0051
		private void GenerateEventsScriptFile()
		{
			const string fieldDeclaration = "private readonly ThreadlinkEvent<{Generics}> {EventName} = new();";

			string eventAccessorDeclaration =
			"public event ThreadlinkDelegate<{Generics}> {AccessorName}" +
			NewLine +
			"{" +
			NewLine +
			"add => {EventName}.OnInvoke += value;" +
			NewLine +
			"remove => {EventName}.OnInvoke -= value;" +
			NewLine +
			"}";

			#region Input Validation:
			foreach (var type in eventsRegistry.Values)
			{
				if (type.Contains("ThreadlinkEvent<") == false)
				{
					UnityConsole.Notify(DebugNotificationType.Error, this, "All events must be of type ThreadlinkEvent<Output,Input>!");
					return;
				}
			}
			#endregion

			var sections = new string[3];

			#region Accessor Generation:
			var accessors = new string[eventsRegistry.Count];
			int index = 0;

			foreach (var eventEntry in eventsRegistry)
			{
				string eventName = eventEntry.Key;
				string eventType = eventEntry.Value.Replace(" ", string.Empty);

				if (eventType.Contains("ThreadlinkEvent<") == false)
				{
					UnityConsole.Notify(DebugNotificationType.Error, this, "All events must be of type ThreadlinkEvent<Output,Input>!");
					return;
				}

				string accessor = eventAccessorDeclaration.Replace("{AccessorName}", eventName.FirstToUpper());
				accessor = accessor.Replace("{Generics}", eventType.ExtractContentInAngleBrackets());
				accessor = accessor.Replace("{EventName}", eventName.FirstToLower());
				accessor += NewLine;

				accessors[index] = accessor;
				index++;
			}

			sections[0] = string.Join(NewLine, accessors);
			#endregion

			#region Field and Method Generation:
			var fields = new string[eventsRegistry.Count];
			var methods = new string[eventsRegistry.Count];
			index = 0;

			string eventTypeTemplate = "ThreadlinkEvent<{Generics}>";
			const string voidDeclaration = "ThreadlinkEvent<Empty,Empty>";

			string methodImplementation =
			"public {Output} Invoke{UpperEventName}Event({Input} input = default)" +
			NewLine +
			"{" +
			NewLine +
			"return {EventName}.Invoke(input);" +
			NewLine +
			"}";

			foreach (var eventEntry in eventsRegistry)
			{
				string eventName = eventEntry.Key;
				string eventType = eventEntry.Value.Replace(" ", string.Empty);

				string field = fieldDeclaration.Replace("{EventName}", eventName.FirstToLower());

				string extractedGenerics = eventType.ExtractContentInAngleBrackets();

				field = field.Replace("{Generics}", extractedGenerics);

				string constructedType = eventTypeTemplate.Replace("{Generics}", extractedGenerics);
				var extractedTypes = constructedType.ExtractCommaSeparatedContentInAngleBrackets();

				if (field.Contains(voidDeclaration))
					field = field.Replace(voidDeclaration, "VoidEvent");
				else if (extractedGenerics.Contains("Empty,"))
				{
					field = field.Replace(constructedType, "GenericInputEvent<{GInput}>");
					field = field.Replace("{GInput}", extractedTypes[1]);
				}
				else if (extractedGenerics.Contains(",Empty"))
				{
					field = field.Replace(constructedType, "GenericOutputEvent<{GOutput}>");
					field = field.Replace("{GOutput}", extractedTypes[0]);
				}

				fields[index] = field;

				string method = methodImplementation.Replace("{Output}", extractedTypes[0]);
				method = method.Replace("{Input}", extractedTypes[1]);
				string methodName = eventName.FirstToUpper();
				method = method.Replace("{UpperEventName}", methodName);
				method = method.Replace("{EventName}", eventName.FirstToLower());

				methods[index] = method;

				index++;
			}

			sections[1] = string.Join(NewLine, fields);
			sections[2] = string.Join(NewLine, methods);
			#endregion

			string result = codeGenTemplate.text.Replace("{EventCodeGen}", string.Join(NewLine + NewLine, sections));
			result = result.Replace("{Usings}", string.Join(NewLine, necessaryUsings));

			var writer = new StreamWriter(Directory.GetParent(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)))
			+ "/ThreadlinkEventBusPartial.cs");

			writer.Write(CodeFormatter.Format(result).Code);
			writer.Close();

			EditorUtilities.SaveAllAssets();
			AssetDatabase.Refresh();
		}
#endif
	}
}