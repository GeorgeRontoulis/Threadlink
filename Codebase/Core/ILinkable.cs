namespace Threadlink.Core
{
	using Utilities.Collections;
	using Utilities.Events;

	/// <summary>
	/// Base interface for all Threadlink-Compatible objects.
	/// </summary>
	public interface ILinkable : IIdentifiable
	{
		/// <summary>
		/// Event raised right before this object is discarded.
		/// </summary>
		public VoidEvent OnBeforeDiscarded { get; }

		/// <summary>
		/// Nullifies all properties and fields of this <typeparamref name="ILinkable"/> and destroys it.
		/// You can use <typeparamref name="OnBeforeDiscarded"/> to get a callback right before that happens.
		/// </summary>
		public VoidOutput Discard(VoidInput _ = default);

		public void Boot();
		public void Initialize();
	}
}