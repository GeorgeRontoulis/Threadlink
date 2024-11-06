namespace Threadlink.Utilities.Events
{
	using System.Runtime.InteropServices;

	public delegate Output ThreadlinkDelegate<Output, Input>(Input input);

	[StructLayout(LayoutKind.Sequential, Size = 0)] public struct Empty { }

	public sealed class VoidEvent : ThreadlinkEvent<Empty, Empty> { }
	public sealed class GenericInputEvent<T> : ThreadlinkEvent<Empty, T> { }
	public sealed class GenericOutputEvent<T> : ThreadlinkEvent<T, Empty> { }

	public abstract class ThreadlinkEvent { public abstract void Discard(); }

	public class ThreadlinkEvent<Output, Input> : ThreadlinkEvent
	{
		public System.Delegate[] InvocationList => ContainedEvent?.GetInvocationList();

		public event ThreadlinkDelegate<Output, Input> OnInvoke
		{
			add => TryAddListener(value);
			remove => Remove(value);
		}

		private event ThreadlinkDelegate<Output, Input> ContainedEvent = null;

		public override void Discard() { ContainedEvent = null; }

		public bool Contains(ThreadlinkDelegate<Output, Input> listener)
		{
			return ContainedEvent.Contains(listener);
		}

		/// <summary>
		/// Attempt to add the provided listener as a subscriber to this event.
		/// </summary>
		/// <param name="listener">The listener to add as a subscriber to the event.</param>
		/// <returns> <see langword="false"/> if the <paramref name="listener"/> 
		/// is already subscribed to this event. <see langword="true"/> otherwise.</returns>
		private bool TryAddListener(ThreadlinkDelegate<Output, Input> listener)
		{
			if (ContainedEvent == null)
			{
				ContainedEvent = listener;
				return true;
			}
			else if (Contains(listener) == false)
			{
				ContainedEvent += listener;
				return true;
			}
			else return false;
		}

		private void Remove(ThreadlinkDelegate<Output, Input> listener)
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