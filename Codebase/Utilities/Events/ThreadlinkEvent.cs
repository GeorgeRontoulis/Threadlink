namespace Threadlink.Utilities.Events
{
	public sealed class VoidEvent : ThreadlinkEvent<VoidOutput, VoidInput> { }
	public sealed class VoidGenericEvent<T> : ThreadlinkEvent<VoidOutput, T> { }
	public sealed class GenericEvent<T> : ThreadlinkEvent<T, VoidInput> { }

	public class ThreadlinkEvent<Output, Input>
	{
		public System.Delegate[] InvocationList => ContainedEvent.GetInvocationList();

		private event ThreadlinkDelegate<Output, Input> ContainedEvent = null;

		public void Discard() { ContainedEvent = null; }

		/// <summary>
		/// Attempt to add the provided listener as a subscriber to this event.
		/// </summary>
		/// <param name="listener">The listener to add as a subscriber to the event.</param>
		/// <returns> <see langword="false"/> if the <paramref name="listener"/> 
		/// is already subscribed to this event. <see langword="true"/> otherwise.</returns>
		public bool TryAddListener(ThreadlinkDelegate<Output, Input> listener)
		{
			if (ContainedEvent == null)
			{
				ContainedEvent = listener;
				return true;
			}
			else if (ContainedEvent.Contains(listener) == false)
			{
				ContainedEvent += listener;
				return true;
			}
			else return false;
		}

		public void AddListener(ThreadlinkDelegate<Output, Input> listener)
		{
			if (ContainedEvent == null) ContainedEvent = listener;
			else ContainedEvent += listener;
		}

		public void Remove(ThreadlinkDelegate<Output, Input> listener)
		{
			ContainedEvent -= listener;

			if (ContainedEvent != null && ContainedEvent.GetListenerCount() <= 0) ContainedEvent = null;
		}

		/// <summary>
		/// Invoke this event.
		/// </summary>
		/// <param name="input">The input to pass to the event.</param>
		/// <returns>
		/// Output of type <typeparamref name="Output"/>.
		/// </returns>
		public virtual Output Invoke(Input input = default)
		{
			return ContainedEvent == null ? default : ContainedEvent.Invoke(input);
		}
	}
}