namespace Threadlink.Utilities.Reflection
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using Threadlink.Utilities.UnityLogging;
	using UnityEditor;
	using UnityEngine;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	public static class Reflection
	{
		public static ArrayType[] TryGetArrayOfType<OwnerType, ArrayType>(this OwnerType owner)
		{
			var ownerType = owner.GetType();
			var properties = ownerType.GetProperties();

			if (properties == null)
			{
				UnityConsole.Notify(DebugNotificationType.Warning, "No Properties found!");
				return null;
			}

			int length = properties.Length;

			for (int i = 0; i < length; i++)
			{
				var info = properties[i];
				var propertyTpe = info.PropertyType;

				if (propertyTpe.IsArray && propertyTpe.GetElementType() == typeof(ArrayType)) return (ArrayType[])info.GetValue(owner);
			}

			UnityConsole.Notify("Could not find requested array in Properties. Checking Fields.");

			var fields = ownerType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

			if (properties == null)
			{
				UnityConsole.Notify(DebugNotificationType.Warning, "No Fields found!");
				return null;
			}

			length = fields.Length;

			for (int i = 0; i < length; i++)
			{
				var info = fields[i];
				var fieldType = info.FieldType;

				if (fieldType.IsArray && fieldType.GetElementType() == typeof(ArrayType)) return (ArrayType[])info.GetValue(owner);
			}

			UnityConsole.Notify(DebugNotificationType.Warning, "Could not find requested array in Fields. Returning NULL.");

			return null;
		}

		public static Dictionary<Key, Value> TryGetDictionaryOfType<Key, Value>(this object owner)
		{
			var ownerType = owner.GetType();
			var properties = ownerType.GetProperties();

			if (properties == null)
			{
				UnityConsole.Notify(DebugNotificationType.Warning, "No Properties found!");
				return null;
			}

			int length = properties.Length;

			for (int i = 0; i < length; i++)
			{
				var info = properties[i];
				var propertyTpe = info.PropertyType;

				if (propertyTpe.IsGenericType && propertyTpe.GetGenericTypeDefinition().Equals(typeof(Dictionary<Key, Value>)))
					return (Dictionary<Key, Value>)info.GetValue(owner);
			}

			UnityConsole.Notify(DebugNotificationType.Warning, "Could not find requested Dictionary in Properties. Returning NULL.");

			return null;
		}

		public static IEnumerable<string> GetAllUnityComponents()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			var componentTypes = new List<string>();

			int length = assemblies.Length;

			for (int i = 0; i < length; i++)
			{
				var assembly = assemblies[i];
				var unityComponentTypes = assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Component)));

				foreach (var type in unityComponentTypes) componentTypes.Add(type.Name);
			}

			return componentTypes;
		}

		public static IEnumerable<string> GetAllDerivedTypesOf<T>() where T : class
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			var componentTypes = new List<string>();

			int length = assemblies.Length;

			for (int i = 0; i < length; i++)
			{
				var assembly = assemblies[i];
				var requestedTypes = assembly.GetTypes().Where(t => t.IsAbstract == false && t.IsSubclassOf(typeof(T)));

				foreach (var type in requestedTypes) componentTypes.Add(type.Name);
			}

			return componentTypes;
		}

		public static IEnumerable<string> GetAllTypesImplementing<T>()
		{
			var interfaceType = typeof(T);
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			var componentTypes = new List<string>();

			int length = assemblies.Length;

			bool IsValid(Type t) => t.GetInterfaces().Contains(interfaceType) && t.IsAbstract == false && t.IsGenericType == false;

			for (int i = 0; i < length; i++)
			{
				var assembly = assemblies[i];
				var requestedTypes = assembly.GetTypes().Where(IsValid);

				foreach (var type in requestedTypes) componentTypes.Add(type.FullName);
			}

			return componentTypes;
		}

#if UNITY_EDITOR && ODIN_INSPECTOR
		public static IEnumerable<ValueDropdownItem> CreateDropdownFor<T>() where T : UnityEngine.Object
		{
			var items = new List<ValueDropdownItem>();
			var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath<T>(path);

				if (asset != null) items.Add(new(asset.name, asset));
			}

			return items;
		}

		public static IEnumerable<ValueDropdownItem> CreateNameDropdownFor<T>() where T : UnityEngine.Object
		{
			var items = new List<ValueDropdownItem>();
			var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath<T>(path);

				if (asset != null) items.Add(new(asset.name, asset.name));
			}

			return items;
		}
#endif
	}
}