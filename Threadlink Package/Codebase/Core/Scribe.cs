namespace Threadlink.Core.Subsystems.Scribe
{
	using Core;
	using Cysharp.Text;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Text.RegularExpressions;
	using UnityEngine;

	/// <summary>
	/// Threadlink's Text and Logging Subsystem.
	/// </summary>
	public static class Scribe
	{
		public enum ConsoleLogType { Info, Warning, Error }

		public const ConsoleLogType INFO = ConsoleLogType.Info;
		public const ConsoleLogType WARN = ConsoleLogType.Warning;
		public const ConsoleLogType ERROR = ConsoleLogType.Error;
		public const char WHITESPACE = (char)32;

		private const string SPLIT_RE = @";(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
		private const string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";
		private static readonly char[] TRIM_CHARS = { '\"' };

		public static Utf8ValueStringBuilder ToNonAllocText(params object[] input)
		{
			using var stringBuilder = ZString.CreateUtf8StringBuilder(true);
			int length = input.Length;

			for (int i = 0; i < length; i++) stringBuilder.Append(input[i]);

			return stringBuilder;
		}

		public static Utf8ValueStringBuilder FromSubsystem<T>(params object[] input) where T : ThreadlinkSubsystem
		{
			using var stringBuilder = ZString.CreateUtf8StringBuilder(true);
			int length = input.Length;

			stringBuilder.Append("[");
			stringBuilder.Append(typeof(T).Name);
			stringBuilder.Append("] - ");

			for (int i = 0; i < length; i++) stringBuilder.Append(input[i]);

			return stringBuilder;
		}

		public static void ToUnityConsole(this Utf8ValueStringBuilder nonAllocInput, ConsoleLogType logType = INFO)
		{
			string loggedMessage = nonAllocInput.ToString();

			switch (logType)
			{
				case ConsoleLogType.Info:
				Debug.Log(loggedMessage);
				break;
				case ConsoleLogType.Warning:
				Debug.LogWarning(loggedMessage);
				break;
				case ConsoleLogType.Error:
				Debug.LogError(loggedMessage);
				break;
			}
		}

		public static void ToUnityConsole(this Utf8ValueStringBuilder nonAllocInput, UnityEngine.Object context, ConsoleLogType logType = INFO)
		{
			string loggedMessage = nonAllocInput.ToString();

			switch (logType)
			{
				case ConsoleLogType.Info:
				Debug.Log(loggedMessage, context);
				break;
				case ConsoleLogType.Warning:
				Debug.LogWarning(loggedMessage, context);
				break;
				case ConsoleLogType.Error:
				Debug.LogError(loggedMessage, context);
				break;
			}
		}

		public static void ToUnityConsole(UnityEngine.Object context, ConsoleLogType logType = INFO, params object[] input)
		{
			string loggedMessage = ToNonAllocText(input).ToString();

			switch (logType)
			{
				case ConsoleLogType.Info:
				Debug.Log(loggedMessage, context);
				break;
				case ConsoleLogType.Warning:
				Debug.LogWarning(loggedMessage, context);
				break;
				case ConsoleLogType.Error:
				Debug.LogError(loggedMessage, context);
				break;
			}
		}

		#region Text Operations:
		public static string FirstToUpper(this string input)
		{
			if (string.IsNullOrEmpty(input)) return input;

			return ToNonAllocText(char.ToUpper(input[0]), input[1..]).ToString();
		}

		public static string FirstToLower(this string input)
		{
			if (string.IsNullOrEmpty(input)) return input;

			return ToNonAllocText(char.ToLower(input[0]), input[1..]).ToString();
		}

		public static string LastToUpper(this string input)
		{
			if (string.IsNullOrEmpty(input)) return input;

			return ToNonAllocText(input[..^1], char.ToUpper(input[^1])).ToString();
		}

		public static string[] ExtractCommaSeparatedContentInAngleBrackets(this string input)
		{
			Match match = Regex.Match(input, @"<([^>]+)>");
			if (match.Success)
			{
				return match.Groups[1].Value.Split(',');
			}

			return Array.Empty<string>();
		}

		public static string ExtractContentInAngleBrackets(this string input)
		{
			var match = Regex.Match(input, @"<([^>]+)>");
			return match.Success ? match.Groups[1].Value : string.Empty;
		}

		public static string RemoveFirstLast(string input)
		{
			if (string.IsNullOrEmpty(input) == false && input.Length > 2)
				return input.Remove(input.Length - 1).Remove(0, 1);
			else
				return string.Empty;
		}

		public static string FirstLastToUpper(string input)
		{
			if (string.IsNullOrEmpty(input) || input.Length == 1) return input.ToUpper();

			return ToNonAllocText(char.ToUpper(input[0]), RemoveFirstLast(input), char.ToUpper(input[^1])).ToString();
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

				if (valueLength == 0 || string.IsNullOrEmpty(values[0])) continue;

				var entry = new Dictionary<string, object>();

				for (var j = 0; j < headerLength && j < valueLength; j++)
				{
					string value = values[j];
					value = value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\\", string.Empty);
					object finalvalue = value;

					if (int.TryParse(value, out int integerResult))
						finalvalue = integerResult;
					else if (float.TryParse(value, out float floatResult))
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

				while (string.IsNullOrEmpty(line = reader.ReadLine()) == false) result.Add(line);
			}

			return result;
		}

		public static List<string> ToLineList(this TextAsset textAsset, List<string> buffer)
		{
			buffer.Clear();

			using (var reader = new StringReader(textAsset.text))
			{
				string line;

				while (string.IsNullOrEmpty(line = reader.ReadLine()) == false) buffer.Add(line);
			}

			return buffer;
		}

		internal static string ExtractName(this IEnumerator target)
		{
			return target.GetType().Name.Split('>')[0].TrimStart('<');
		}

		public static Utf8ValueStringBuilder SeparateUpperCaseAndCommas(string input, bool convertToUpperCase = false)
		{
			if (string.IsNullOrEmpty(input)) return default;

			using var utf8sb = ZString.CreateUtf8StringBuilder();
			utf8sb.Append(input[0]);

			char whitespace = WHITESPACE;
			const char comma = ',';
			const char dash = '-';
			int length = input.Length;

			for (int i = 1; i < length; i++)
			{
				char current = input[i];
				char previous = input[i - 1];

				if (char.IsUpper(current) && char.IsWhiteSpace(previous) == false && previous != dash)
					utf8sb.Append(whitespace);

				utf8sb.Append(convertToUpperCase ? char.ToUpper(current) : current);

				if (current == comma && (i + 1 < input.Length && char.IsUpper(input[i + 1]) == false))
					utf8sb.Append(whitespace);
			}

			return utf8sb;
		}
		#endregion
	}
}