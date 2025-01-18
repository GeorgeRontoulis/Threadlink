namespace Threadlink.Core.StorageAPI
{
	using Core.ExtensionMethods;
	using System;
	using UnityEngine;
	using Utilities.Collections;
	using ExtensionMethods;
	using Subsystems.Scribe;
	using System.Collections.Generic;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;

#elif THREADLINK_INSPECTOR
	using Editor.Attributes;
#endif
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

	public abstract class ThreadlinkParcel : LinkableAsset, IComparable<string>
	{
		public bool allowCloning = false;

		public int CompareTo(string other) => string.Compare(name, other);

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
		public List<ThreadlinkParcel> Parcels => parcels;

#if UNITY_EDITOR && (ODIN_INSPECTOR || THREADLINK_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] private List<ThreadlinkParcel> parcels = new();

		public override void Discard()
		{
			if (IsInstance)
			{
				for (int i = parcels.Count - 1; i >= 0; i--) parcels[i].Discard();

				parcels.Clear();
				parcels.TrimExcess();
				parcels = null;
			}

			base.Discard();
		}

		internal void CloneParcels()
		{
			for (int i = parcels.Count - 1; i >= 0; i--)
			{
				var original = parcels[i];

				if (original.IsInstance || !original.allowCloning) continue;

				parcels[i] = original.Clone();
				parcels[i].OnCloned();
			}
		}

		public bool TryAdd<T>(string parcelName, out T parcel) where T : ThreadlinkParcel
		{
			bool result = IsInstance && parcels.BruteForceSearch(parcelName) >= 0;

			parcel = result ? Create<T>(parcelName) : null;

			if (result) parcels.Add(parcel);

			return result;
		}

		public bool Remove(string parcelName)
		{
			if (IsInstance == false) return false;

			int index = parcels.BruteForceSearch(parcelName);
			bool result = index >= 0;

			if (result)
			{
				var parcel = parcels[index];

				parcel.Discard();
				parcels.RemoveAt(index);
			}

			return result;
		}

		public bool TryGet<T>(string parcelID, out ThreadlinkParcel<T> result)
		{
			int index = parcels.BruteForceSearch(parcelID);
			bool validIndex = index >= 0;

			result = validIndex ? parcels[index] as ThreadlinkParcel<T> : null;
			return validIndex;
		}

		public bool TryGet<T>(string parcelID, out T result) where T : ThreadlinkParcel
		{
			int index = parcels.BruteForceSearch(parcelID);
			bool validIndex = index >= 0;

			result = validIndex ? parcels[index] as T : null;
			return validIndex;
		}

		public bool TryGet<T>(int parcelIndex, out ThreadlinkParcel<T> result)
		{
			if (!parcelIndex.IsWithinBoundsOf(parcels))
			{
				result = null;
				return false;
			}

			result = parcels[parcelIndex] is ThreadlinkParcel<T> castParcel ? castParcel : null;
			return result != null;
		}

		public bool TryGet<T>(int parcelIndex, out T result) where T : ThreadlinkParcel
		{
			if (!parcelIndex.IsWithinBoundsOf(parcels))
			{
				result = null;
				return false;
			}

			result = parcels[parcelIndex] is T castParcel ? castParcel : null;
			return result != null;
		}

		public T ValueOf<T>(string parcelID)
		{
			int index = parcels.BruteForceSearch(parcelID);
			return index >= 0 && parcels[index] is ThreadlinkParcel<T> castParcel ? castParcel.Value : default;
		}

		public bool TrySet<T>(string parcelID, T newValue)
		{
			bool found = TryGet<T>(parcelID, out var parcel);

			if (found) parcel.Value = newValue; else PostParcelNotFoundMessage();

			return found;
		}

		public bool TrySet<T>(int parcelIndex, T newValue)
		{
			bool found = TryGet<T>(parcelIndex, out var parcel);

			if (found) parcel.Value = newValue; else PostParcelNotFoundMessage();

			return found;
		}

		private void PostParcelNotFoundMessage()
		{
			Scribe.FromSubsystem<Threadlink>("Parcel to set was not found!").ToUnityConsole(this, Scribe.WARN);
		}
	}
}
