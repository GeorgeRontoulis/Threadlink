namespace Threadlink.Utilities.Mathematics
{
	using UnityEngine;

	public static class Mathematics
	{
		public struct WaveData
		{
			public float Frequency { get; private set; }
			public float Time { get; private set; }
			public float Amplitude { get; private set; }

			public WaveData(float frequency, float time, float amplitude = 1)
			{
				Amplitude = amplitude;
				Frequency = frequency;
				Time = time;
			}
		}

		public static float NormalizeBetween(float target, float min, float max) { return (target - min) / (max - min); }

		public static float Denormalize(float normalizedValue, float min, float max)
		{
			return normalizedValue * (max - min) + min;
		}

		public static int Clamp01ToInt(float value) { return Mathf.RoundToInt(Mathf.Clamp01(value)); }

		public static float SineWave(WaveData data)
		{
			return data.Amplitude * Mathf.Sin((Mathf.PI + Mathf.PI) * data.Frequency * data.Time);
		}

		public static float SineWave(WaveData data, float phase)
		{
			return data.Amplitude * Mathf.Sin((Mathf.PI + Mathf.PI) * data.Frequency * data.Time + phase);
		}

		public static float SquareWave(WaveData data)
		{
			return data.Amplitude * Mathf.Sign(Mathf.Sin((Mathf.PI + Mathf.PI) * data.Frequency * data.Time));
		}

		public static float SawtoothWave(WaveData data)
		{
			return (data.Amplitude + data.Amplitude) / Mathf.PI * Mathf.Atan(Mathf.Tan(Mathf.PI * data.Frequency * data.Time * 0.5f));
		}

		public static float TriangleWave(WaveData data)
		{
			return (data.Amplitude + data.Amplitude) / Mathf.PI * Mathf.Asin(Mathf.Sin((Mathf.PI + Mathf.PI) * data.Frequency * data.Time));
		}

		public static float PulseWave(WaveData data)
		{
			return data.Amplitude * Mathf.Sign(Mathf.Sin(Mathf.PI * data.Frequency * data.Time));
		}

		public static float StringWave(WaveData data)
		{
			// Simulate the fundamental frequency (first harmonic)
			float fundamental = data.Amplitude * Mathf.Sin(2 * Mathf.PI * data.Frequency * data.Time);

			// Simulate additional harmonics (adjust as needed)
			float harmonic1 = 0.7f * data.Amplitude * Mathf.Sin(2 * Mathf.PI * 2 * data.Frequency * data.Time);
			float harmonic2 = 0.5f * data.Amplitude * Mathf.Sin(2 * Mathf.PI * 3 * data.Frequency * data.Time);
			float harmonic3 = 0.3f * data.Amplitude * Mathf.Sin(2 * Mathf.PI * 4 * data.Frequency * data.Time);

			// Add harmonics to the fundamental
			float result = fundamental + harmonic1 + harmonic2 + harmonic3;

			// Apply a simple decay over time
			result *= Mathf.Exp(-0.1f * data.Time);

			return result;
		}
	}
}