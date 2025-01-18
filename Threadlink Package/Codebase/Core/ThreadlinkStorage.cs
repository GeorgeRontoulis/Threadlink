namespace Threadlink.Core.StorageAPI
{
	using Core.ExtensionMethods;
	using System;
	using UnityEngine;
	using Utilities.Collections;
	using ExtensionMethods;
	using Subsystems.Scribe;

#if UNITY_EDITOR && ODIN_INSPECTOR
	using Sirenix.OdinInspector;
	using System.Linq;
	using System.Collections.Generic;
	using UnityEditor;
#endif

	namespace ExtensionMethods
	{
		public static class ThreadlinkStorageExtensions
		{
			/// <summary>
			/// Timesaver method for cloning this storage as well its stored parcels, 
			/// setting it up for safe modification.
			/// </summary>
			/// <param name="storage">The <see cref="ThreadlinkStorage"/> to deploy.</param>
			/// <returns>The deployed storage copy.</returns>
			public static ThreadlinkStorage Deploy(this ThreadlinkStorage storage)
			{
				var copy = storage.IsInstance ? storage : storage.Clone();
				copy.CloneParcels();

				return copy;
			}
		}
	}

	public abstract class ThreadlinkParcel : LinkableAsset
	{
		[SerializeField] private bool allowCloning = true;

		public virtual void OnCloned() { }
	}

	public abstract class ThreadlinkParcel<T> : ThreadlinkParcel
	{
		public event Action<T> OnValueChanged = null;

		public virtual T Value
		{
			get => value;
			set
			{
				this.value = value;
				OnValueChanged?.Invoke(value);
			}
		}

		[SerializeField] private T value = default;

		public override void Discard()
		{
			OnValueChanged = null;
			if (IsInstance) value = default;
			base.Discard();
		}
	}

	/// <summary>
	/// Structure where <see cref="ThreadlinkParcel"/>s are stored.
	/// Provides lookup as well as update methods for parcel modification.
	/// Always remember to call <see cref="ThreadlinkStorageExtensions.Deploy(ThreadlinkStorage)"/> on the storage
	/// before modifying any of the stored parcels, unless you specifically want to modify the original structure.
	/// </summary>
	[CreateAssetMenu(menuName = "Threadlink/Core/Threadlink Storage")]
	public sealed class ThreadlinkStorage : LinkableAsset
	{
		public SerializedDictionary<string, ThreadlinkParcel> Parcels => parcels;

#if UNITY_EDITOR && ODIN_INSPECTOR
		[DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
		[ReadOnly]
#endif
		[SerializeField] private SerializedDictionary<string, ThreadlinkParcel> parcels = new();

		#region Inspector Logic:
#if UNITY_EDITOR && ODIN_INSPECTOR
		private IEnumerable<ValueDropdownItem> GetParcelTypes()
		{
			var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Threadlink.User");

			if (assembly == null) return null;

			return assembly.GetTypes().
			Where(type => typeof(ThreadlinkParcel).IsAssignableFrom(type) && !type.IsAbstract).
			Select(type => new ValueDropdownItem(type.Name, type));
		}

		[PropertySpace(10)]

		[BoxGroup(GroupID = "Add", GroupName = "Add Parcel", ShowLabel = true)]
		[ValueDropdown(nameof(GetParcelTypes), FlattenTreeView = true, NumberOfItemsBeforeEnablingSearch = 16)]
		[ShowInInspector]
		private Type parcelType = null;

		[BoxGroup(GroupID = "Add", GroupName = "Add Parcel", ShowLabel = true)]
		[Button(Style = ButtonStyle.Box)]
		private void AddNewParcel(string parcelName)
		{
			if (parcels.ContainsKey(parcelName))
				throw new InvalidOperationException("A parcel with the same name already exists in this Storage!");

			var newParcel = CreateInstance(parcelType) as ThreadlinkParcel;
			newParcel.name = parcelName;

			AssetDatabase.AddObjectToAsset(newParcel, this);

			parcels.Add(parcelName, newParcel);

			EditorUtility.SetDirty(this);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		[BoxGroup(GroupID = "Add", GroupName = "Add Parcel", ShowLabel = true)]
		[Button(Style = ButtonStyle.Box)]
		private void AddExistingParcel(ThreadlinkParcel existingParcel)
		{
			if (existingParcel == null) return;

			var clone = Instantiate(existingParcel);
			clone.name = existingParcel.name;

			AssetDatabase.AddObjectToAsset(clone, this);

			parcels.Add(clone.name, clone);

			EditorUtility.SetDirty(this);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		[BoxGroup(GroupID = "Remove", GroupName = "Remove Parcel", ShowLabel = true)]
		[Button(Style = ButtonStyle.Box)]
		private void RemoveParcel(string parcelName)
		{
			if (parcels.TryGetValue(parcelName, out var parcel))
			{
				parcels.Remove(parcelName);

				AssetDatabase.RemoveObjectFromAsset(parcel);
				EditorUtility.SetDirty(this);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
		}
#endif
		#endregion

		public override void Discard()
		{
			if (IsInstance)
			{
				var parcels = this.parcels.Values;
				foreach (var parcel in parcels) parcel.Discard();

				this.parcels.Clear();
				this.parcels.TrimExcess();
				this.parcels = null;
			}

			base.Discard();
		}

		internal void CloneParcels()
		{
			int length = parcels.Count;
			var originalParcels = parcels.Values.ToArray();

			parcels.Clear();

			for (int i = 0; i < length; i++)
			{
				var original = originalParcels[i];

				bool isAlreadyInstance = original.IsInstance;

				var clonedParcel = isAlreadyInstance ? original : original.Clone();

				if (isAlreadyInstance == false) clonedParcel.OnCloned();

				parcels.Add(clonedParcel.name, clonedParcel);
			}
		}

		public bool TryAdd<T>(string parcelName, out T parcel) where T : ThreadlinkParcel
		{
			if (IsInstance && parcels.ContainsKey(parcelName) == false)
			{
				parcel = Create<T>(parcelName);
				parcels.Add(parcelName, parcel);
				return true;
			}
			else
			{
				parcel = null;
				return false;
			}
		}

		public bool Remove(string parcelName)
		{
			if (IsInstance && parcels.ContainsKey(parcelName))
			{
				parcels[parcelName].Discard();

				return parcels.Remove(parcelName);
			}

			return false;
		}

		public bool TryGet<T>(string parcelID, out ThreadlinkParcel<T> result)
		{
			bool found = parcels.TryGetValue(parcelID, out var parcel) && parcel is ThreadlinkParcel<T>;

			result = found ? parcel as ThreadlinkParcel<T> : null;
			return found;
		}

		public bool TryGet<T>(string parcelID, out T result) where T : ThreadlinkParcel
		{
			bool found = parcels.TryGetValue(parcelID, out var parcel);

			result = parcel as T;
			return found;
		}

		public T ValueOf<T>(string parcelID)
		{
			return parcels.TryGetValue(parcelID, out var parcel) && parcel is ThreadlinkParcel<T> castParcel ? castParcel.Value : default;
		}

		public bool TrySet<T>(string parcelID, T newValue)
		{
			bool found = TryGet<T>(parcelID, out var parcel);

			if (found)
				parcel.Value = newValue;
			else
				throw new InvalidOperationException(Scribe.FromSubsystem<Threadlink>("Parcel to set was not found!").ToString());

			return found;
		}
	}
}
