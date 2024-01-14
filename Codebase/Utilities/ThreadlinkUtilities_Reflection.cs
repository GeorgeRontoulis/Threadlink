namespace Threadlink.Utilities.Reflection
{
	using System;
	using System.Reflection;
	using Threadlink.Utilities.UnityLogging;

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
	}
}