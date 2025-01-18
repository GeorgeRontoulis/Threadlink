namespace Threadlink.Addressables
{
	using Core;

#if UNITY_EDITOR
	using Editor.Attributes;
#endif

	public abstract class Addressable : IIdentifiable<string>
	{
		public virtual string LinkID
		{
			get => assetAddress;
			set => _ = value;
		}

		public abstract bool Loaded { get; }

#if UNITY_EDITOR
		[AddressableAssetButton]
#endif
		public string assetAddress = string.Empty;

		public abstract void Unload();
	}

	public abstract class Addressable<T> : Addressable
	{
		public abstract T Result { get; }
	}
}