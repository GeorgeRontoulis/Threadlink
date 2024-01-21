namespace Threadlink.Utilities.Editor
{
	using System.Text;
	using Threadlink.Core;
	using UnityEngine;
	using Utilities.Collections;
	using String = Text.String;

	[CreateAssetMenu(menuName = "Threadlink/Component Blueprint")]
	internal sealed class ThreadlinkComponentBlueprint : ScriptableObject
	{
		private static readonly StringBuilder StringBuilder = new();

		private enum AccessModifier { Public, Internal }
		private enum Type { Struct, Class }

		[SerializeField] private AccessModifier accessModifier = 0;
		[SerializeField] private Type type = 0;

		[Space(10)]

		[SerializeField] private ScriptableVariable[] variables = new ScriptableVariable[0];

		internal string Compile()
		{
			string newLine = System.Environment.NewLine;
			string space = " ";
			string accessModLower = accessModifier.ToString().ToLower();

			string declaration = String.Construct(accessModLower, space,
			type.ToString().ToLower(), space, name, " : ", typeof(IThreadlinkComponent).Name);

			int length = variables.Length;

			for (int i = 0; i < length; i++)
			{
				var variable = variables[i];

				if (variable == null) continue;

				StringBuilder.Append(String.Construct(accessModLower, space,
				variable.TypeName, space, variable.name, " { get; set; }", i == length - 1 ? string.Empty : newLine));
			}

			string variablesCode = StringBuilder.ToString();

			StringBuilder.Clear();

			return String.Construct(declaration, newLine, "{", newLine, variablesCode, newLine, "}");
		}
	}
}