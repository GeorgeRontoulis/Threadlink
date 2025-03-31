namespace Threadlink.Core
{
	public interface INamable { public string Name { get; } }
	public interface IIdentifiable { public int ID { get; } }

	/// <summary>
	/// Boot = Unity's Awake
	/// </summary>
	public interface IBootable { public void Boot(); }

	/// <summary>
	/// Initialize = Unity's Start
	/// </summary>
	public interface IInitializable { public void Initialize(); }

	/// <summary>
	/// Base interface for Threadlink-Compatible entities that can be destroyed at runtime.
	/// </summary>
	public interface IDiscardable
	{
		/// <summary>
		/// Nullifies all properties and fields of this <see cref="IDiscardable"/> and destroys it.
		/// </summary>
		public void Discard();
	}
}