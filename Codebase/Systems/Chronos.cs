namespace Threadlink.Systems
{
	using Core;
	using System;
	using UnityEngine;
	using Utilities.Events;
	using Utilities.UnityLogging;

	/// <summary>
	/// System responsible for Time Management during Threadlink's runtime.
	/// </summary>
	public sealed class Chronos : LinkableSystem<LinkableEntity>
	{
		public static Chronos Instance { get; set; }

		public static event VoidDelegate OnGamePaused
		{
			add
			{
				ref VoidDelegate myEvent = ref Instance.onGamePaused;

				if (myEvent == null) myEvent += value;
				else
				{
					if (myEvent.Contains(value) == false) myEvent += value;
				}
			}
			remove { Instance.onGamePaused -= value; }
		}

		public static event VoidDelegate OnGameResumed
		{
			add
			{
				ref VoidDelegate myEvent = ref Instance.onGameResumed;


				if (myEvent == null) myEvent += value;
				else
				{
					if (myEvent.Contains(value) == false) myEvent += value;
				}
			}
			remove { Instance.onGameResumed -= value; }
		}

		public static event VoidDelegate OnCountPlaytime
		{
			add
			{
				ref VoidDelegate myEvent = ref Instance.onCountPlaytime;


				if (myEvent == null) myEvent += value;
				else
				{
					if (myEvent.Contains(value) == false) myEvent += value;
				}
			}
			remove { Instance.onCountPlaytime -= value; }
		}

		/// <summary>
		/// Gets or sets the current Timescale.
		/// Setting the timescale using this property only changes Unity's internal Time.TimeScale value.
		/// Use Chronos.TimeScale to apply the change along with certain events and callbacks instead. Valid Timescale values are 0 and 1.
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
		/// Use Chronos.RawTimeScale to only change the Time.TimeScale value instead. Valid Timescale values are 0 and 1.
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

					if (Instance.onGamePaused != null) Instance.onGamePaused();
				}
				else if (Approx(1f))
				{
					UpdateTimescale();

					if (Instance.onGameResumed != null) Instance.onGameResumed();
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

		private event VoidDelegate onCountPlaytime;
		private event VoidDelegate onGamePaused = null;
		private event VoidDelegate onGameResumed = null;

		public override void Boot()
		{
			Instance = this;
			onCountPlaytime = null;
			TotalPlaytime = 0;
			base.Boot();
		}

		public override void Initialize()
		{
			Iris.SubscribeToUpdate(UpdateStandardTime);
			Iris.SubscribeToFixedUpdate(UpdatePhysicsTime);
		}

		private void UpdateStandardTime()
		{
			DeltaTime = Time.deltaTime;
			SmoothDeltaTime = Time.smoothDeltaTime;
			UnscaledDeltaTime = Time.unscaledDeltaTime;

			onCountPlaytime?.Invoke();
		}

		private void UpdatePhysicsTime() { FixedDeltaTime = Time.fixedDeltaTime; }

		public static void SetPlaytimeCountingState(bool state)
		{
			if (state) OnCountPlaytime += IncrementTotalPlaytime;
			else OnCountPlaytime -= IncrementTotalPlaytime;
		}

		internal static void ResetPlaytime()
		{
			SetPlaytimeCountingState(false);
			TotalPlaytime = 0;
		}

		private static void LogInvalidTimescaleWarning()
		{
			Scribe.SystemLog(Instance.LinkID, DebugNotificationType.Warning,
			"Invalid Timescale requested! Valid values are 0 and 1. Check your Timescale assignments!");
		}

		private static void IncrementTotalPlaytime() { TotalPlaytime += UnscaledDeltaTime; }
	}
}
