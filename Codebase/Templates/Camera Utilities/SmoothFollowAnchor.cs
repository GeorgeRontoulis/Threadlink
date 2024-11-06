namespace Threadlink.Templates.CameraUtilities
{
	using Core;
	using System;
	using Systems;
	using UnityEngine;
	using Utilities.Events;

	internal sealed class SmoothFollowAnchor : LinkableBehaviour, IInitializable
	{
		[Flags]
		private enum Options
		{
			None = 0,
			SmoothFollow = 1 << 0,
			IgnoreTimescale = 1 << 1,
			DontDestroyOnLoad = 1 << 2,
		}

		[SerializeField] private Transform followTarget = null;

		[Space(10)]

		[SerializeField] private Vector3 followOffset = Vector3.zero;
		[SerializeField] private float followSpeed = 1;

		[Space(10)]

		[SerializeField] private Options options = Options.SmoothFollow;

		public void Initialize()
		{
			if (followTarget != null)
				Iris.OnUpdate += Follow;
			else
				this.LogException<NullReferenceException>();
		}

		private Empty Follow(Empty _)
		{
			var target = followTarget.position + followOffset;

			if (options.HasFlag(Options.SmoothFollow))
			{
				selfTransform.position = Vector3.MoveTowards(selfTransform.position,
				target, followSpeed * (options.HasFlag(Options.IgnoreTimescale) ? Chronos.DeltaTime : Chronos.UnscaledDeltaTime));
			}
			else selfTransform.position = target;

			return default;
		}

		public void DetachFromParent()
		{
			selfTransform.SetParent(null);
			if (options.HasFlag(Options.DontDestroyOnLoad)) DontDestroyOnLoad(gameObject);
		}

		public override Empty Discard(Empty _ = default)
		{
			Iris.OnUpdate -= Follow;
			followTarget = null;
			return base.Discard(_);
		}
	}
}