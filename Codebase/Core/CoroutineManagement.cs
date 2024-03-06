namespace Threadlink.Core
{
	using System;
	using System.Collections;

	/// <summary>
	/// Class representing a batched coroutine.
	/// This should only be used in combination with <typeparamref name="ThreadlinkCoroutineBatch"/>
	/// </summary>
	internal sealed class ThreadlinkCoroutine
	{
		private IEnumerator InternalProcess { get; set; }

		internal ThreadlinkCoroutine(IEnumerator targetCoroutine)
		{
			InternalProcess = targetCoroutine;
		}

		internal IEnumerator Monitor(Action onCompleted)
		{
			yield return InternalProcess;

			onCompleted?.Invoke();

			InternalProcess = null;
		}
	}

	/// <summary>
	/// A batch of coroutines.
	/// The container automatically runs all of its coroutines immeditally
	/// after initialization. You can check when the entire batch has finished
	/// executing by checking the <paramref name="IsDone"/> property.
	/// </summary>
	public sealed class ThreadlinkCoroutineBatch
	{
		public bool IsDone => CoroutineCount <= 0;

		private int CoroutineCount { get; set; }

		public ThreadlinkCoroutineBatch(params IEnumerator[] coroutines)
		{
			CoroutineCount = coroutines.Length;

			ThreadlinkCoroutine[] trackableCoroutines = new ThreadlinkCoroutine[CoroutineCount];

			for (int i = 0; i < CoroutineCount; i++)
			{
				trackableCoroutines[i] = new(coroutines[i]);
				Threadlink.LaunchCoroutine(trackableCoroutines[i].Monitor(DecreaseCountByOne), false);
			}
		}

		private void DecreaseCountByOne() { CoroutineCount--; }
	}
}