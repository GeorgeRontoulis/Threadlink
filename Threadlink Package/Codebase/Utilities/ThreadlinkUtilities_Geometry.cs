namespace Threadlink.Utilities.Geometry
{
	using Collections;
	using Core;
	using System.Collections.Generic;
	using UnityEngine;

	public static class Geometry
	{
		public static readonly Vector3 One = Vector3.one;
		public static readonly Vector3 Zero = Vector3.zero;
		public static readonly Vector3 Forward = Vector3.forward;
		public static readonly Vector3 Right = Vector3.right;
		public static readonly Vector3 Up = Vector3.up;

		public struct DistanceData<T>
		{
			public T ClosestEntity { get; private set; }
			public float Distance { get; private set; }

			public DistanceData(T closestEntity, float distance)
			{
				ClosestEntity = closestEntity;
				Distance = distance;
			}
		}

		public static float[] GetTriangleSizes(int[] tris, Vector3[] verts)
		{
			int triCount = tris.Length / 3;
			var sizes = new float[triCount];

			for (int i = 0; i < triCount; i++)
			{
				int iMul3 = i * 3;

				sizes[i] = .5f * Vector3.Cross(verts[tris[iMul3 + 1]] - verts[tris[iMul3]], verts[tris[iMul3 + 2]] - verts[tris[iMul3]]).magnitude;
			}

			return sizes;
		}

		public static KeyValuePair<float[], float> CalculateMeshAreas(float[] sizes)
		{
			int length = sizes.Length;
			var cumulativeSizes = new float[length];
			float total = 0;

			for (int i = 0; i < length; i++)
			{
				total += sizes[i];
				cumulativeSizes[i] = total;
			}

			return new(cumulativeSizes, total);
		}

		public static bool ConeCheck(Vector3 center, Vector3 pointA, Vector3 pointB, Vector3 direction)
		{
			// Calculate vectors from center to points A and B
			var vectorAC = pointA - center;
			var vectorBC = pointB - center;

			// Calculate the angle between vector AC and vector BC
			float angleABC = Vector3.SignedAngle(vectorAC.normalized, vectorBC.normalized, Vector3.up);

			// Calculate the angle between vector AC and the direction
			float angleACDir = Vector3.SignedAngle(vectorAC.normalized, direction.normalized, Vector3.up);

			// Check if the direction angle is within the cone angle
			return angleACDir > 0 && angleACDir <= angleABC;
		}

		public static bool Approximately(Vector3 a, Vector3 b)
		{
			static bool Approx(float a, float b) { return Mathf.Approximately(a, b); }

			return Approx(a.x, b.x) && Approx(a.y, b.y) && Approx(a.z, b.z);
		}

		public static Vector2 ToUVCoordinates(this Vector2Int input, Vector2Int dimensions)
		{
			return new(input.x / (float)dimensions.x, input.y / (float)dimensions.y);
		}

		public static Vector2 ToScreenPosition(this Vector2 input, Vector2 dimensions)
		{
			return new(input.x * dimensions.x, input.y * dimensions.y);
		}

		public static List<Vector3> CalculateBisectors(Vector3[] originalPath, float offsetAmount)
		{
			var bisectorPath = new List<Vector3>();

			int pathCount = originalPath.Length;

			for (int i = 0; i < pathCount; i++)
			{
				var currentPoint = originalPath[i];
				var nextPoint = originalPath[(i + 1) % pathCount];
				var prevPoint = originalPath[(i - 1 + pathCount) % pathCount];

				var bisector = CalculateBisector(prevPoint, currentPoint, nextPoint).normalized * offsetAmount;
				var bisectedPoint = currentPoint + bisector;

				bisectorPath.Add(bisectedPoint);
			}

			return bisectorPath;
		}

		public static Vector3 Interpolate(this AnimationCurve curve, Vector3 startVector, Vector3 endVector, float t)
		{
			return Vector3.Lerp(startVector, endVector, curve.Evaluate(t));
		}

		public static Vector3 CubicInterpolation(Vector3 k0, Vector3 k1, float u)
		{
			float u2 = u * u;
			float u3 = u2 * u;
			return k0 * (2 * u3 - 3 * u2 + 1) + k1 * (3 * u2 - 2 * u3);
		}

		public static Vector3 CalculateBisector(Vector3 pointA, Vector3 pointB, Vector3 pointC)
		{
			return ((pointB - pointA).normalized + (pointC - pointB).normalized).normalized;
		}

		public static Vector3 GetPerpendicularVector(Vector3 vectorB, Vector3 midPointB)
		{
			return midPointB + new Vector3(-vectorB.y, vectorB.x, vectorB.z);
		}

		public static int ManhattanDistance(MatrixPosition point1, MatrixPosition point2)
		{
			return Mathf.Abs(point1.C - point2.C) + Mathf.Abs(point1.R - point2.R);
		}

		public static Vector3 GetVector(this Transform end, Transform start) => end.position - start.position;

		public static void AverageCenter(this IReadOnlyList<MatrixPosition> positions, ref MatrixPosition storage)
		{
			int count = positions.Count;

			if (positions == null || count <= 0)
			{
				storage.C = -1;
				storage.R = -1;
			}

			float totalX = 0;
			float totalZ = 0;

			for (int i = 0; i < count; i++)
			{
				var position = positions[i];

				totalX += position.C;
				totalZ += position.R;
			}

			storage.C = Mathf.RoundToInt(totalX / count);
			storage.R = Mathf.RoundToInt(totalZ / count);
		}

		public static int IndexOfFurthestPointFrom(this IReadOnlyList<MatrixPosition> collection, MatrixPosition referencePoint)
		{
			if (collection == null || collection.Count <= 0) return -1; // Return -1 to indicate an invalid index

			int maxDistance = int.MinValue;
			int furthestIndex = -1;

			for (int i = 0; i < collection.Count; i++)
			{
				int distance = ManhattanDistance(collection[i], referencePoint);

				if (distance > maxDistance)
				{
					maxDistance = distance;
					furthestIndex = i;
				}
			}

			return furthestIndex;
		}

		public static List<MatrixPosition> GetFurthestFrom(this IReadOnlyList<List<MatrixPosition>> collections, MatrixPosition referencePoint)
		{
			int count = collections.Count;

			if (collections == null || count == 0) return null;

			List<MatrixPosition> furthest = null;
			int maxDistance = int.MinValue;

			for (int i = 0; i < count; i++)
			{
				var col = collections[i];
				int colCount = col.Count;
				int distance = 0;

				for (int j = 0; j < colCount; j++) distance += ManhattanDistance(col[j], referencePoint);

				if (distance > maxDistance)
				{
					maxDistance = distance;
					furthest = col;
				}
			}

			return furthest;
		}

		/// <typeparam name="T">The Type of the Entity.</typeparam>
		/// <param name="referencePoint">The point from which to calculate distances from.</param>
		/// <param name="collection">The collection of entities.</param>
		/// <returns>The Entity closest to referencePoint. Null if the collection is invalid.</returns>
		public static DistanceData<T> GetClosestEntityFromCollection<T>(this IReadOnlyList<T> collection, Transform referencePoint)
		where T : LinkableBehaviour
		{
			int count = collection.Count;

			if (count <= 0) return new(null, -1);

			T closestEntity = null;
			float closestSqrDist = Mathf.Infinity;

			if (count > 1)
			{
				//Inverse loop to avoid sketchy behaviour in case the collection is altered while it is running.
				for (int i = count - 1; i >= 0; i--)
				{
					var candidate = collection[i];
					var candidateTransform = candidate.CachedTransform;
					var directionToCandidate = GetVector(candidateTransform, referencePoint);
					float sqrDistFromCandidate = directionToCandidate.sqrMagnitude;

					if (sqrDistFromCandidate < closestSqrDist)
					{
						closestSqrDist = sqrDistFromCandidate;
						closestEntity = candidate;
					}
				}
			}
			else
			{
				var entity = collection[0];
				var entityTransform = entity.CachedTransform;

				closestSqrDist = GetVector(entityTransform, referencePoint).sqrMagnitude;
				closestEntity = entity;
			}

			return new(closestEntity, Mathf.Sqrt(closestSqrDist));
		}

		/// <typeparam name="T">The Type of the Entity.</typeparam>
		/// <param name="referencePoint">The point from which to calculate distances from.</param>
		/// <param name="collection">The collection of entities.</param>
		/// <returns>The Entity furthest from referencePoint. Null if the collection is invalid.</returns>
		public static DistanceData<T> GetFurthestEntityFromCollection<T>(this IReadOnlyList<T> collection, Transform referencePoint)
		where T : LinkableBehaviour
		{
			var newList = new List<T>(collection);

			referencePoint.TryGetComponent<T>(out var self);
			newList.Remove(self);

			int count = newList.Count;

			if (count <= 0) return new(null, -1);

			T furthestEntity = null;
			float furthestSqrDist = -Mathf.Infinity;

			if (count > 1)
			{
				// Inverse loop to avoid sketchy behaviour in case the collection is altered while it is running.
				for (int i = count - 1; i >= 0; i--)
				{
					var candidate = newList[i];
					var candidateTransform = candidate.CachedTransform;
					var directionToCandidate = GetVector(candidateTransform, referencePoint);
					float sqrDistFromCandidate = directionToCandidate.sqrMagnitude;

					if (sqrDistFromCandidate > furthestSqrDist)
					{
						furthestSqrDist = sqrDistFromCandidate;
						furthestEntity = candidate;
					}
				}
			}
			else
			{
				var entity = newList[0];
				var entityTransform = entity.CachedTransform;

				furthestSqrDist = GetVector(entityTransform, referencePoint).sqrMagnitude;
				furthestEntity = entity;
			}

			return new(furthestEntity, Mathf.Sqrt(furthestSqrDist));
		}


		/// <typeparam name="T">The Type of the Entity.</typeparam>
		/// <param name="referencePoint">The point from which to calculate distances from.</param>
		/// <param name="collection">The collection of entities.</param>
		/// <returns>The Entity closest to referencePoint. Null if the collection is invalid.</returns>
		public static DistanceData<T> GetClosestEntityFromCollection<T>(this IReadOnlyList<T> collection, Transform referencePoint,
		LayerMask lineOfSightObstructionMask) where T : LinkableBehaviour
		{
			bool LineOfSightObstructed(Vector3 start, Vector3 end)
			{
				return Physics.Linecast(start, end, lineOfSightObstructionMask, QueryTriggerInteraction.Ignore);
			}

			int count = collection.Count;

			if (count <= 0) return new(null, -1);

			T closestEntity = null;
			float closestSqrDist = Mathf.Infinity;

			if (count > 1)
			{
				//Inverse loop to avoid sketchy behaviour in case the collection is altered while it is running.
				for (int i = count - 1; i >= 0; i--)
				{
					var candidate = collection[i];
					var candidateTransform = candidate.CachedTransform;
					var directionToCandidate = GetVector(candidateTransform, referencePoint);
					float sqrDistFromCandidate = directionToCandidate.sqrMagnitude;
					var start = referencePoint.position + referencePoint.up;
					var end = candidateTransform.position + candidateTransform.up;

					if (sqrDistFromCandidate < closestSqrDist && LineOfSightObstructed(start, end) == false)
					{
						closestSqrDist = sqrDistFromCandidate;
						closestEntity = candidate;
					}
				}
			}
			else
			{
				var entity = collection[0];
				var entityTransform = entity.CachedTransform;
				var start = referencePoint.position + referencePoint.up;
				var end = entityTransform.position + entityTransform.up;

				if (LineOfSightObstructed(start, end) == false)
				{
					closestSqrDist = GetVector(entityTransform, referencePoint).sqrMagnitude;
					closestEntity = entity;
				}
				else
				{
					closestSqrDist = -1;
					closestEntity = null;
				}
			}

			return new(closestEntity, Mathf.Sqrt(closestSqrDist));
		}
	}
}