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
			Type ownerType = owner.GetType();
			PropertyInfo[] properties = ownerType.GetProperties();

			if (properties == null)
			{
				UnityConsole.Notify(DebugNotificationType.Warning, "No Properties found!");
				return null;
			}

			int length = properties.Length;

			for (int i = 0; i < length; i++)
			{
				PropertyInfo info = properties[i];
				Type propertyTpe = info.PropertyType;

				if (propertyTpe.IsArray && propertyTpe.GetElementType() == typeof(ArrayType)) return (ArrayType[])info.GetValue(owner);
			}

			UnityConsole.Notify("Could not find requested array in Properties. Checking Fields.");

			FieldInfo[] fields = ownerType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

			if (properties == null)
			{
				UnityConsole.Notify(DebugNotificationType.Warning, "No Fields found!");
				return null;
			}

			length = fields.Length;

			for (int i = 0; i < length; i++)
			{
				FieldInfo info = fields[i];
				Type fieldType = info.FieldType;

				if (fieldType.IsArray && fieldType.GetElementType() == typeof(ArrayType)) return (ArrayType[])info.GetValue(owner);
			}

			UnityConsole.Notify(DebugNotificationType.Warning, "Could not find requested array in Fields. Returning NULL.");

			return null;
		}

		public static Dictionary<Key, Value> TryGetDictionaryOfType<Key, Value>(this object owner)
		{
			Type ownerType = owner.GetType();
			PropertyInfo[] properties = ownerType.GetProperties();

			if (properties == null)
			{
				UnityConsole.Notify(DebugNotificationType.Warning, "No Properties found!");
				return null;
			}

			int length = properties.Length;

			for (int i = 0; i < length; i++)
			{
				PropertyInfo info = properties[i];
				Type propertyTpe = info.PropertyType;

				if (propertyTpe.IsGenericType && propertyTpe.GetGenericTypeDefinition().Equals(typeof(Dictionary<Key, Value>)))
					return (Dictionary<Key, Value>)info.GetValue(owner);
			}

			UnityConsole.Notify(DebugNotificationType.Warning, "Could not find requested Dictionary in Properties. Returning NULL.");

			return null;
		}

		public static IEnumerable<string> GetAllUnityComponents()
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			List<string> componentTypes = new();

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
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			List<string> componentTypes = new();

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
			Type interfaceType = typeof(T);
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			List<string> componentTypes = new();

			int length = assemblies.Length;

			Func<Type, bool> func = t => t.GetInterfaces().Contains(interfaceType) && t.IsAbstract == false && t.IsGenericType == false;

			for (int i = 0; i < length; i++)
			{
				var assembly = assemblies[i];
				var requestedTypes = assembly.GetTypes().Where(func);

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

				if (asset != null) items.Add(new ValueDropdownItem(asset.name, asset));
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

				if (asset != null) items.Add(new ValueDropdownItem(asset.name, asset.name));
			}

			return items;
		}
#endif
	}
}