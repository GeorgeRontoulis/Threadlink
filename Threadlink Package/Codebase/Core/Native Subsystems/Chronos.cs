namespace Threadlink.Core.Subsystems.Chronos
{
	using Core;
	using Propagator;
	using Scribe;
	using System;
	using UnityEngine;

	/// <summary>
	/// System responsible for Time Management during Threadlink's runtime.
	/// </summary>
	public sealed class Chronos : ThreadlinkSubsystem<Chronos>
	{
		/// <summary>
		/// Gets or sets the current Timescale.
		/// Setting the timescale using this property only changes Unity's internal Time.TimeScale value.
		/// Use <paramref name="TimeScale"/> to apply the change along with certain events and callbacks instead. Valid Timescale values are 0 and 1.
		/// </summary>
		public static float RawTimeScale
		{
			get => Time.timeScale;
			set
			{
				bool Approx(float compare) => Mathf.Approximately(value, compare);

				if (Approx(0f) || Approx(1f)) Time.timeScale = value; else Throw();
			}
		}

		/// <summary>
		/// Gets or sets the current Timescale.
		/// Setting the timescale using this property invokes events and callbacks that may be undesired in some situations.
		/// Use <paramref name="RawTimeScale"/> to only change Unity's internal Time.TimeScale value instead. Valid Timescale values are 0 and 1.
		/// </summary>
		public static float TimeScale
		{
			get => Time.timeScale;
			set
			{
				bool Approx(float compare) => Mathf.Approximately(value, compare);

				if (Approx(Time.timeScale)) return;

				void UpdateTimescale() => Time.timeScale = value;

				if (Approx(0f))
				{
					UpdateTimescale();
					Propagator.Publish(PropagatorEvents.OnPaused);
				}
				else if (Approx(1f))
				{
					UpdateTimescale();
					Propagator.Publish(PropagatorEvents.OnResumed);
				}
				else Throw();
			}
		}

		public static double CurrentFramerate => 1d / (double)DeltaTime;

		public static float CurrentFrameTimeSinceStart { get; private set; }
		public static float TotalPlaytime { get; private set; }
		public static float DeltaTime { get; private set; }
		public static float SmoothDeltaTime { get; private set; }
		public static float FixedDeltaTime { get; private set; }
		public static float UnscaledDeltaTime { get; private set; }

		private enum TotalPlaytimeCountingMode { Scaled, Unscaled }
		[SerializeField] private TotalPlaytimeCountingMode totalPlaytimeCountingMode = 0;

		[NonSerialized] private Action countPlaytime = null;

		public override void Discard()
		{
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnUpdate, UpdateStandardTime);
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnFixedUpdate, UpdatePhysicsTime);

			countPlaytime = null;

			base.Discard();
		}

		public override void Boot()
		{
			base.Boot();
			Propagator.Subscribe<Action>(PropagatorEvents.OnUpdate, UpdateStandardTime);
			Propagator.Subscribe<Action>(PropagatorEvents.OnFixedUpdate, UpdatePhysicsTime);
			TotalPlaytime = 0;
		}

		private void UpdateStandardTime()
		{
			CurrentFrameTimeSinceStart = Time.time;
			DeltaTime = Time.deltaTime;
			SmoothDeltaTime = Time.smoothDeltaTime;
			UnscaledDeltaTime = Time.unscaledDeltaTime;

			if (countPlaytime != null)
			{
				countPlaytime.Invoke();
				Propagator.Publish(PropagatorEvents.OnPlaytimeCount, TotalPlaytime);
			}
		}

		private void UpdatePhysicsTime()
		{
			FixedDeltaTime = Time.fixedDeltaTime;
		}

		public static void SetPlaytimeCountingState(bool state)
		{
			if (state)
			{
				Instance.countPlaytime = Instance.totalPlaytimeCountingMode.Equals(TotalPlaytimeCountingMode.Scaled) ?
				IncrementTotalPlaytimeScaled : IncrementTotalPlaytimeUnscaled;
			}
			else Instance.countPlaytime = null;
		}

		internal static void ResetPlaytime()
		{
			SetPlaytimeCountingState(false);
			TotalPlaytime = 0;
		}

		private static void Throw()
		{
			throw new InvalidOperationException(Scribe.FromSubsystem<Chronos>("Invalid Timescale provided!").ToString());
		}

		private static void IncrementTotalPlaytimeScaled() => TotalPlaytime += DeltaTime;
		private static void IncrementTotalPlaytimeUnscaled() => TotalPlaytime += UnscaledDeltaTime;
	}
}
