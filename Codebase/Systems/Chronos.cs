namespace Threadlink.Systems
{
	using Core;
	using System;
	using UnityEngine;
	using Utilities.Events;

	/// <summary>
	/// System responsible for Time Management during Threadlink's runtime.
	/// </summary>
	public sealed class Chronos : LinkableBehaviourSingleton<Chronos>
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
				bool Approx(float compare) { return Mathf.Approximately(value, compare); }

				if (Approx(0f) || Approx(1f)) Time.timeScale = value; else LogInvalidTimescaleWarning();
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
				bool Approx(float compare) { return Mathf.Approximately(value, compare); }

				if (Approx(Time.timeScale)) return;

				void UpdateTimescale() { Time.timeScale = value; }

				if (Approx(0f))
				{
					UpdateTimescale();
					OnGamePaused.Invoke();
				}
				else if (Approx(1f))
				{
					UpdateTimescale();
					OnGameResumed.Invoke();
				}
				else LogInvalidTimescaleWarning();
			}
		}

		public static double Framerate => 1d / (double)DeltaTime;

		public static float TotalPlaytime { get; set; }
		public static float DeltaTime { get; private set; }
		public static float SmoothDeltaTime { get; private set; }
		public static float FixedDeltaTime { get; private set; }
		public static float UnscaledDeltaTime { get; private set; }

		public static VoidEvent OnGamePaused => Instance.onGamePaused;
		public static VoidEvent OnGameResumed => Instance.onGameResumed;
		public static VoidEvent OnCountPlaytime => Instance.onCountPlaytime;

		private VoidEvent onCountPlaytime = new();
		private VoidEvent onGameResumed = new();
		private VoidEvent onGamePaused = new();

		public override void Discard()
		{
			onGamePaused.Discard();
			onGameResumed.Discard();
			onCountPlaytime.Discard();

			onGamePaused = null;
			onGameResumed = null;
			onCountPlaytime = null;
			base.Discard();
		}

		public override void Boot()
		{
			Instance = this;
			TotalPlaytime = 0;
		}

		public override void Initialize()
		{
			Iris.SubscribeToUpdate(UpdateStandardTime);
			Iris.SubscribeToFixedUpdate(UpdatePhysicsTime);
		}

		private VoidOutput UpdateStandardTime(VoidInput _)
		{
			DeltaTime = Time.deltaTime;
			SmoothDeltaTime = Time.smoothDeltaTime;
			UnscaledDeltaTime = Time.unscaledDeltaTime;

			onCountPlaytime.Invoke();

			return default;
		}

		private VoidOutput UpdatePhysicsTime(VoidInput _)
		{
			FixedDeltaTime = Time.fixedDeltaTime;
			return default;
		}

		public static void SetPlaytimeCountingState(bool state)
		{
			if (state) OnCountPlaytime.TryAddListener(IncrementTotalPlaytime);
			else OnCountPlaytime.Remove(IncrementTotalPlaytime);
		}

		internal static void ResetPlaytime()
		{
			SetPlaytimeCountingState(false);
			TotalPlaytime = 0;
		}

		private static void LogInvalidTimescaleWarning()
		{
			Scribe.SystemLog<ArgumentException>(Instance.LinkID,
			"Invalid Timescale requested! Valid values are 0 and 1. Check your Timescale assignments!");
		}

		private static VoidOutput IncrementTotalPlaytime(VoidInput _)
		{
			TotalPlaytime += UnscaledDeltaTime;
			return default;
		}
	}
}
