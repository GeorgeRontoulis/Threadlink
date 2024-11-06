namespace Threadlink.Core
{
	using Utilities.Collections;
	using Utilities.Events;

	/// <summary>
	/// Base interface for all Threadlink-Compatible entities.
	/// </summary>
	public interface ILinkable : IIdentifiable { }

	public interface IBootable : ILinkable { public void Boot(); }
	public interface IInitializable : ILinkable { public void Initialize(); }

	public interface IDiscardable : ILinkable
	{
		/// <summary>
		/// Event raised right before this entity is discarded.
		/// </summary>
		public event ThreadlinkDelegate<Empty, Empty> OnDiscard;

		/// <summary>
		/// Nullifies all properties and fields of this <see cref="IDiscardable"/> and destroys it.
		/// You can subscribe to <see cref="OnDiscard"/> to listen for an event right before that happens.
		/// </summary>
		public Empty Discard(Empty _ = default);
	}
}