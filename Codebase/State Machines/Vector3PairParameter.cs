//Project: Aeon's Legacy: Ascension
//Lead Programmer: George Rontoulis

namespace Threadlink.StateMachines
{
	using UnityEngine;

	public struct Vector3Pair
	{
		public Vector3 A { get; private set; }
		public Vector3 B { get; private set; }

		public Vector3Pair(Vector3 forward, Vector3 right)
		{
			A = forward;
			B = right;
		}
	}

	[CreateAssetMenu(menuName = "Threadlink/State Machines/Parameters/Vector3Pair")]
	public sealed class Vector3PairParameter : AbstractParameter<Vector3Pair> { }
}