namespace Threadlink.Utilities.Geometry
{
	using System.Collections.Generic;
	using Threadlink.Core;
	using Threadlink.Utilities.Collections;
	using UnityEngine;

	public static class Geometry
	{
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

		public static (Vector2 viewportPosition, Vector2 canvasPosition) WorldToCanvasPoint(this RectTransform canvasRect, Vector3 worldPoint)
		{
			Vector2 viewportPos = Camera.main.WorldToViewportPoint(worldPoint, Camera.MonoOrStereoscopicEye.Mono);

			float deltaX = canvasRect.sizeDelta.x;
			float deltaY = canvasRect.sizeDelta.y;

			float x = (viewportPos.x * deltaX) - (deltaX * 0.5f);
			float y = (viewportPos.y * deltaY) - (deltaY * 0.5f);

			Vector2 canvasPos = new Vector2(x, y);

			return (viewportPos, canvasPos);
		}

		public static bool ConeCheck(Vector3 center, Vector3 pointA, Vector3 pointB, Vector3 direction)
		{
			// Calculate vectors from center to points A and B
			Vector3 vectorAC = pointA - center;
			Vector3 vectorBC = pointB - center;

			// Calculate the angle between vector AC and vector BC
			float angleABC = Vector3.SignedAngle(vectorAC.normalized, vectorBC.normalized, Vector3.up);

			// Calculate the angle between vector AC and the direction
			float angleACDir = Vector3.SignedAngle(vectorAC.normalized, direction.normalized, Vector3.up);

			// Check if the direction angle is within the cone angle
			return angleACDir > 0 && angleACDir <= angleABC;
		}

		public static bool Approximately(Vector3 a, Vector3 b)
		{
			bool Approx(float a, float b) { return Mathf.Approximately(a, b); }

			return Approx(a.x, b.x) && Approx(a.y, b.y) && Approx(a.z, b.z);
		}

		public static Vector2 ToUVCoordinates(this Vector2Int input, Vector2Int dimensions)
		{
			return new Vector2(input.x / (float)dimensions.x, input.y / (float)dimensions.y);
		}

		public static Vector2 ToScreenPosition(this Vector2 input, Vector2 dimensions)
		{
			return new Vector2(input.x * dimensions.x, input.y * dimensions.y);
		}

		public static List<Vector3> CalculateBisectors(Vector3[] originalPath, float offsetAmount)
		{
			List<Vector3> bisectorPath = new List<Vector3>();

			int pathCount = originalPath.Length;

			for (int i = 0; i < pathCount; i++)
			{
				Vector3 currentPoint = originalPath[i];
				Vector3 nextPoint = originalPath[(i + 1) % pathCount];
				Vector3 prevPoint = originalPath[(i - 1 + pathCount) % pathCount];

				Vector3 bisector = CalculateBisector(prevPoint, currentPoint, nextPoint).normalized * offsetAmount;
				Vector3 bisectedPoint = currentPoint + bisector;

				bisectorPath.Add(bisectedPoint);
			}

			return bisectorPath;
		}

		public static Vector3 CalculateBisector(Vector3 pointA, Vector3 pointB, Vector3 pointC)
		{
			Vector3 vectorAB = (pointB - pointA).normalized;
			Vector3 vectorBC = (pointC - pointB).normalized;

			Vector3 bisector = vectorAB + vectorBC;
			return bisector.normalized;
		}

		public static Vector3 GetPerpendicularVector(Vector3 vectorB, Vector3 midPointB)
		{
			// Calculate a vector perpendicular to B
			Vector3 vectorA = new Vector3(-vectorB.y, vectorB.x, vectorB.z);

			// Ensure that vectorA starts from the middle point of B
			return midPointB + vectorA;
		}

		public static MatrixPosition AverageCenter(this List<MatrixPosition> points)
		{
			int count = points.Count;

			if (points == null || count == 0) return new MatrixPosition(0, 0);

			int totalX = 0;
			int totalZ = 0;

			for (int i = 0; i < count; i++)
			{
				totalX += points[i].C;
				totalZ += points[i].R;
			}

			int averageX = (int)(totalX / (float)count);
			int averageZ = (int)(totalZ / (float)count);

			return new MatrixPosition(averageZ, averageX);
		}

		public static int IndexOfFurthestPointFrom(this List<MatrixPosition> collection, MatrixPosition referencePoint)
		{
			if (collection == null || collection.Count == 0) return -1; // Return -1 to indicate an invalid index

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


		public static List<MatrixPosition> GetFurthestFrom(this List<List<MatrixPosition>> collections, MatrixPosition referencePoint)
		{
			int count = collections.Count;

			if (collections == null || count == 0) return null;

			List<MatrixPosition> furthest = null;
			int maxDistance = int.MinValue;

			for (int i = 0; i < count; i++)
			{
				List<MatrixPosition> col = collections[i];
				int colCount = col.Count;
				int distance = 0;

				for (int j = 0; j < colCount; j++)
				{
					distance += ManhattanDistance(col[j], referencePoint);
				}

				if (distance > maxDistance)
				{
					maxDistance = distance;
					furthest = col;
				}
			}

			return furthest;
		}

		public static int ManhattanDistance(MatrixPosition point1, MatrixPosition point2)
		{
			return Mathf.Abs(point1.C - point2.C) + Mathf.Abs(point1.R - point2.R);
		}

		/// <typeparam name="T">The Type of the Entity.</typeparam>
		/// <param name="referencePoint">The point from which to calculate distances from.</param>
		/// <param name="collection">The collection of entities.</param>
		/// <returns>The Entity closest to referencePoint. Null if the collection is invalid.</returns>
		internal static DistanceData<T> GetClosestEntityFromCollection<T>(Transform referencePoint, T[] collection)
		where T : LinkableEntity
		{
			Vector3 GetVector(Transform end, Transform start) { return end.position - start.position; }

			int count = collection.Length;

			if (count <= 0) return new DistanceData<T>(null, -1);

			T closestEntity = null;
			float closestSqrDist = Mathf.Infinity;

			if (count > 1)
			{
				//Inverse loop to avoid sketchy behaviour in case the collection is altered while it is running.
				for (int i = count - 1; i >= 0; i--)
				{
					T candidate = collection[i];
					Transform candidateTransform = candidate.SelfTransform;
					Vector3 directionToCandidate = GetVector(candidateTransform, referencePoint);
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
				T entity = collection[0];
				Transform entityTransform = entity.SelfTransform;

				closestSqrDist = GetVector(entityTransform, referencePoint).sqrMagnitude;
				closestEntity = entity;
			}

			return new DistanceData<T>(closestEntity, Mathf.Sqrt(closestSqrDist));
		}

		/// <typeparam name="T">The Type of the Entity.</typeparam>
		/// <param name="referencePoint">The point from which to calculate distances from.</param>
		/// <param name="collection">The collection of entities.</param>
		/// <returns>The Entity closest to referencePoint. Null if the collection is invalid.</returns>
		internal static DistanceData<T> GetClosestEntityFromCollection<T>(Transform referencePoint, List<T> collection)
		where T : LinkableEntity
		{
			List<T> newList = new List<T>();
			newList.AddRange(collection);
			newList.Remove(referencePoint.GetComponent<T>());
			Vector3 GetVector(Transform end, Transform start) { return end.position - start.position; }

			int count = newList.Count;

			if (count <= 0) return new DistanceData<T>(null, -1);

			T closestEntity = null;
			float closestSqrDist = Mathf.Infinity;

			if (count > 1)
			{
				//Inverse loop to avoid sketchy behaviour in case the collection is altered while it is running.
				for (int i = count - 1; i >= 0; i--)
				{
					T candidate = newList[i];
					Transform candidateTransform = candidate.SelfTransform;
					Vector3 directionToCandidate = GetVector(candidateTransform, referencePoint);
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
				T entity = newList[0];
				Transform entityTransform = entity.SelfTransform;

				closestSqrDist = GetVector(entityTransform, referencePoint).sqrMagnitude;
				closestEntity = entity;
			}

			return new DistanceData<T>(closestEntity, Mathf.Sqrt(closestSqrDist));
		}

		/// <typeparam name="T">The Type of the Entity.</typeparam>
		/// <param name="referencePoint">The point from which to calculate distances from.</param>
		/// <param name="collection">The collection of entities.</param>
		/// <returns>The Entity closest to referencePoint. Null if the collection is invalid.</returns>
		internal static DistanceData<T> GetClosestEntityFromCollection<T>(Transform referencePoint, List<T> collection,
		LayerMask lineOfSightObstructionMask) where T : LinkableEntity
		{
			Vector3 GetVector(Transform end, Transform start) { return end.position - start.position; }

			bool LineOfSightObstructed(Vector3 start, Vector3 end)
			{
				return Physics.Linecast(start, end, lineOfSightObstructionMask, QueryTriggerInteraction.Ignore);
			}

			int count = collection.Count;

			if (count <= 0) return new DistanceData<T>(null, -1);

			T closestEntity = null;
			float closestSqrDist = Mathf.Infinity;

			if (count > 1)
			{
				//Inverse loop to avoid sketchy behaviour in case the collection is altered while it is running.
				for (int i = count - 1; i >= 0; i--)
				{
					T candidate = collection[i];
					Transform candidateTransform = candidate.SelfTransform;
					Vector3 directionToCandidate = GetVector(candidateTransform, referencePoint);
					float sqrDistFromCandidate = directionToCandidate.sqrMagnitude;
					Vector3 start = referencePoint.position + referencePoint.up;
					Vector3 end = candidateTransform.position + candidateTransform.up;

					if (sqrDistFromCandidate < closestSqrDist && LineOfSightObstructed(start, end) == false)
					{
						closestSqrDist = sqrDistFromCandidate;
						closestEntity = candidate;
					}
				}
			}
			else
			{
				T entity = collection[0];
				Transform entityTransform = entity.SelfTransform;
				Vector3 start = referencePoint.position + referencePoint.up;
				Vector3 end = entityTransform.position + entityTransform.up;

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

			return new DistanceData<T>(closestEntity, Mathf.Sqrt(closestSqrDist));
		}
	}
}