namespace Threadlink.Utilities.Serialization
{
	using Cysharp.Threading.Tasks;
	using System;
	using System.IO;

	public interface IThreadlinkSerializable
	{
		public bool IsValid { get; set; }
	}

	public static class ThreadlinkSerializationProxy
	{
#if PLATFORM_STANDALONE_WIN
		public const int STREAM_BUFFER_SIZE = 4096;

		public static async UniTask<DeserializedType> DeserializeAsync<DeserializedType>
		(Func<string, UniTask<DeserializedType>> deserializationMethod, string filePath)
		where DeserializedType : IThreadlinkSerializable, new()
		{
			if (File.Exists(filePath))
			{
				var deserializedData = await deserializationMethod.Invoke(filePath);
				deserializedData.IsValid = true;

				return deserializedData;
			}

			return new() { IsValid = false };
		}

		public static async UniTask SerializeAsync<SerializableType>
		(Func<SerializableType, string, UniTask> serializationMethod, SerializableType data, string destinationPath)
		where SerializableType : IThreadlinkSerializable
		{
			await serializationMethod.Invoke(data, destinationPath);
		}
#endif
	}
}
