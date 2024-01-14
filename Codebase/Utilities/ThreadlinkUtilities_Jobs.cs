namespace Threadlink.Utilities.Jobs
{
	using Unity.Jobs;
	using UnityEngine;

	public sealed class WaitForJobCompleted : CustomYieldInstruction
	{
		private readonly bool _isUsingTempJobAllocator;
		private readonly int _forceJobCompletionFrameCount;
		private JobHandle _jobHandle;

		public override bool keepWaiting
		{
			get
			{
				if (_isUsingTempJobAllocator && Time.frameCount >= _forceJobCompletionFrameCount) _jobHandle.Complete();
				if (_jobHandle.IsCompleted) _jobHandle.Complete();
				return _jobHandle.IsCompleted == false;
			}
		}

		public WaitForJobCompleted(JobHandle jobHandle, bool isUsingTempJobAllocator = true)
		{
			_jobHandle = jobHandle;
			_isUsingTempJobAllocator = isUsingTempJobAllocator;

			// force completion before running into native Allocator.TempJob's lifetime limit of 4 frames
			_forceJobCompletionFrameCount = Time.frameCount + 4;
		}
	}
}
