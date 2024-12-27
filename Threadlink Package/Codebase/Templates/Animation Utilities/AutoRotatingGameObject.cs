namespace Threadlink.Templates.AnimationUtilities
{
	using Core;
	using Core.Subsystems.Chronos;
	using Core.Subsystems.Propagator;
	using System;
	using UnityEngine;
	using Utilities.Flags;

	public sealed class AutoRotatingGameObject : LinkableBehaviour, IBootable
	{
		[Flags]
		private enum Options : byte
		{
			None = 0,
			IgnoreTimescale = 1 << 0,
			StartOnInitialization = 1 << 1,
		}

		[SerializeField] private Vector3 degreesPerSecond = Vector3.zero;
		[SerializeField] private Space space = Space.Self;

		[Space(10)]

		[SerializeField] private Options options = Options.None;

		public override void Discard()
		{
			SetRotatingState(false);
			base.Discard();
		}

		public void Boot()
		{
			if (options.HasFlagUnsafe(Options.StartOnInitialization)) SetRotatingState(true);
		}

		public void SetRotatingState(bool state)
		{
			if (state)
				Propagator.Subscribe<Action>(PropagatorEvents.OnUpdate, Rotate);
			else
				Propagator.Unsubscribe<Action>(PropagatorEvents.OnUpdate, Rotate);
		}

		public void Rotate()
		{
			float deltaTime = options.HasFlagUnsafe(Options.IgnoreTimescale) ?
			Chronos.UnscaledDeltaTime : Chronos.DeltaTime;

			cachedTransform.Rotate(degreesPerSecond * deltaTime, space);
		}
	}
}