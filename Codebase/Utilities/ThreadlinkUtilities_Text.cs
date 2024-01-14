namespace Threadlink.Utilities.Text
{
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Text.RegularExpressions;
	using UnityEngine;

	public static class String
	{
		private static readonly StringBuilder StaticStringBuilder = new StringBuilder();

		private static string SPLIT_RE = @";(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
		private static string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";
		private static char[] TRIM_CHARS = { '\"' };

		public static string Construct(params object[] strings)
		{
			int length = strings.Length;

			for (int i = 0; i < length; i++) StaticStringBuilder.Append(strings[i]);

			string constructedString = StaticStringBuilder.ToString();

			StaticStringBuilder.Clear();

			return constructedString;
		}

		internal static List<Dictionary<string, object>> ReadAsCSV(this TextAsset textFile)
		{
			var list = new List<Dictionary<string, object>>();

			var lines = Regex.Split(textFile.text, LINE_SPLIT_RE);
			var lineLength = lines.Length;

			if (lineLength <= 1) return list;

			var header = Regex.Split(lines[0], SPLIT_RE);
			var headerLength = header.Length;

			for (var i = 1; i < lineLength; i++)
			{
				var values = Regex.Split(lines[i], SPLIT_RE);
				var valueLength = values.Length;

				if (valueLength == 0 || values[0] == "") continue;

				var entry = new Dictionary<string, object>();

				for (var j = 0; j < headerLength && j < valueLength; j++)
				{
					string value = values[j];
					value = value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\\", "");
					object finalvalue = value;

					int integerResult;
					float floatResult;

					if (int.TryParse(value, out integerResult))
						finalvalue = integerResult;
					else if (float.TryParse(value, out floatResult))
						finalvalue = floatResult;

					entry[header[j]] = finalvalue;
				}

				list.Add(entry);
			}

			return list;
		}

		public static List<string> ToLineList(this TextAsset textAsset)
		{
			var result = new List<string>();

			using (var reader = new StringReader(textAsset.text))
			{
				string line;

				while (string.IsNullOrEmpty((line = reader.ReadLine())) == false) result.Add(line);
			}

			return result;
		}

		internal static string ExtractCoroutineName(IEnumerator target)
		{
			return target.GetType().Name.Split('>')[0].TrimStart('<');
		}
	}
}