namespace Threadlink.Templates.AnimationUtilities
{
	using Core;
	using System;
	using Systems;
	using UnityEngine;
	using Utilities.Events;

	public sealed class AutoRotatingGameObject : LinkableBehaviour, IInitializable
	{
		[Flags]
		private enum Options
		{
			None = 0,
			IgnoreTimescale = 1 << 0,
			StartOnInitialization = 1 << 1,
		}

		[SerializeField] private Vector3 degreesPerSecond = Vector3.zero;
		[SerializeField] private Space space = Space.Self;

		[Space(10)]

		[SerializeField] private Options options = Options.None;

		public override Empty Discard(Empty _ = default)
		{
			SetRotatingState(false);
			return base.Discard(_);
		}

		public void Initialize()
		{
			if (options.HasFlag(Options.StartOnInitialization)) SetRotatingState(true);
		}

		public void SetRotatingState(bool state)
		{
			if (state) Iris.OnUpdate += Rotate;
			else Iris.OnUpdate -= Rotate;
		}

		public Empty Rotate(Empty _)
		{
			float deltaTime = options.HasFlag(Options.IgnoreTimescale) ?
			Chronos.UnscaledDeltaTime : Chronos.DeltaTime;

			selfTransform.Rotate(degreesPerSecond * deltaTime, space);

			return default;
		}
	}
}