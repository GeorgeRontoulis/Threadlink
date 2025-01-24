namespace Threadlink.Utilities.Geometry
{
	using Unity.Mathematics;
	using UnityEngine;

	public static class Geometry
	{
		public static readonly Vector3 One = Vector3.one;
		public static readonly Vector3 Zero = Vector3.zero;
		public static readonly Vector3 Forward = Vector3.forward;
		public static readonly Vector3 Right = Vector3.right;
		public static readonly Vector3 Up = Vector3.up;
		public static readonly Vector3 XZ = new(1f, 0f, 1f);

		public static bool Approximately(Vector3 a, Vector3 b)
		{
			static bool Approx(float a, float b) => Mathf.Approximately(a, b);

			return Approx(a.x, b.x) && Approx(a.y, b.y) && Approx(a.z, b.z);
		}

		public static Vector2 ToUVCoordinates(this int2 input, int2 dimensions)
		{
			return new(input.x / (float)dimensions.x, input.y / (float)dimensions.y);
		}

		public static Vector2 ToScreenPosition(this Vector2 input, Vector2 dimensions)
		{
			return new(input.x * dimensions.x, input.y * dimensions.y);
		}

		public static Vector3 CubicInterpolation(Vector3 k0, Vector3 k1, float u)
		{
			float u2 = u * u;
			float u3 = u2 * u;
			return k0 * (2 * u3 - 3 * u2 + 1) + k1 * (3 * u2 - 2 * u3);
		}

		public static Vector3 VectorTo(this Transform start, Transform end) => end.position - start.position;
	}
}