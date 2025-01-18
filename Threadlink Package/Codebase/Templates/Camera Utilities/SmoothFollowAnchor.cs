namespace Threadlink.Templates.CameraUtilities
{
	using Core;
	using Core.Subsystems.Chronos;
	using Core.Subsystems.Propagator;
	using System;
	using UnityEngine;
	using Utilities.Flags;

	public sealed class SmoothFollowAnchor : LinkableBehaviour, IBootable
	{
		[Flags]
		private enum Options : byte
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

		public override void Discard()
		{
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnUpdate, Follow);
			followTarget = null;
			base.Discard();
		}

		public void Boot()
		{
			if (followTarget != null)
			{
				if (options.HasFlagUnsafe(Options.DontDestroyOnLoad))
				{
					cachedTransform.SetParent(null);
					DontDestroyOnLoad(gameObject);
				}

				Propagator.Subscribe<Action>(PropagatorEvents.OnUpdate, Follow);
			}
		}

		private void Follow()
		{
			var target = followTarget.position + followOffset;

			if (options.HasFlagUnsafe(Options.SmoothFollow))
			{
				cachedTransform.position = Vector3.MoveTowards(cachedTransform.position,
				target, followSpeed * (options.HasFlagUnsafe(Options.IgnoreTimescale) ? Chronos.DeltaTime : Chronos.UnscaledDeltaTime));
			}
			else cachedTransform.position = target;
		}
	}
}