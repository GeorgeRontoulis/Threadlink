namespace Threadlink.Utilities.Serialization
{
	using FullSerializer;
	using System.IO;
	using Text;
	using UnityEngine;
	using UnityLogging;

	public interface IRetrievable
	{
		public bool IsValid { get; set; }
	}

	public static class Serialization
	{
		private const string saveFileExtension = ".sav";

		private static readonly fsSerializer Serializer = new();

		public static class PersistentDirectoryManager
		{
			private static string ConstructPath(string folderName)
			{
				return TLZString.Construct(Application.persistentDataPath, "/", folderName);
			}

			/// <summary>
			/// Attempts to create a folder at the specified path.
			/// Does nothing if the folder already exists.
			/// </summary>
			/// <param name="folderName">The path the folder will be created at.</param>
			internal static void TryCreatePersistentFolder(string folderName)
			{
				if (string.IsNullOrEmpty(folderName)) return;

				string path = ConstructPath(folderName);

				if (Directory.Exists(path) == false) Directory.CreateDirectory(path);
			}

			/// <summary>
			/// Attempts to delete a folder at the specified path.
			/// Does nothing if the folder does not exist.
			/// </summary>
			/// <param name="folderName">The path to the folder we want to delete.</param>
			public static void DeletePersistentFolder(string folderName, bool recursive)
			{
				if (string.IsNullOrEmpty(folderName)) return;

				string path = ConstructPath(folderName);

				if (Directory.Exists(path)) Directory.Delete(path, recursive);
			}

			internal static string GetPersistentFolderPath(string folderName)
			{
				if (string.IsNullOrEmpty(folderName)) return null;

				string finalPath = ConstructPath(folderName);

				return Directory.Exists(finalPath) ? finalPath : null;
			}
		}

		private static string GetSaveFilePath(string folderName, string fileName)
		{
			PersistentDirectoryManager.TryCreatePersistentFolder(folderName);

			string folderDirectory = PersistentDirectoryManager.GetPersistentFolderPath(folderName);

			return TLZString.Construct(folderDirectory, "/", fileName, saveFileExtension);
		}

		public static void SaveRetrievableData<T>(T retrievableData, string folderName, string fileName) where T : IRetrievable
		{
			var writer = new StreamWriter(GetSaveFilePath(folderName, fileName));

			Serializer.TrySerialize(retrievableData, out var data).AssertSuccess();

			writer.Write(fsJsonPrinter.CompressedJson(data));
			writer.Close();
			writer.Dispose();
		}

		public static T LoadRetrievableData<T>(string folderName, string fileName) where T : IRetrievable, new()
		{
			string filePath = GetSaveFilePath(folderName, fileName);
			bool validPath = string.IsNullOrEmpty(filePath) == false && File.Exists(filePath);

			if (validPath)
			{
				var reader = new StreamReader(filePath);
				string text = reader.ReadToEnd();

				reader.Close();
				reader.Dispose();

				return TryDeserialize<T>(text);
			}
			else return GetInvalidData<T>();
		}

		internal static T TryDeserialize<T>(string input) where T : IRetrievable, new()
		{
			var data = fsJsonParser.Parse(input);
			T deserialized = default;

			var result = Serializer.TryDeserialize(data, ref deserialized).AssertSuccess();

			return result.Equals(fsResult.Success) && deserialized.IsValid ? deserialized : GetInvalidData<T>();
		}

		private static T GetInvalidData<T>() where T : IRetrievable, new()
		{
			UnityConsole.Notify(DebugNotificationType.Warning, context: null,
			typeof(T).Name, " could not be loaded. File not found.");

			var data = new T { IsValid = false };
			return data;
		}
	}
}