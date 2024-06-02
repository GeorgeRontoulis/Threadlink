namespace Threadlink.Templates.PlayerCharacterController
{
	using Threadlink.Utilities.Events;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/Edge")]
	internal sealed class PlayerCharacterEdgeProcessor : PlayerCharacterProcessor
	{
		private Collider[] Edges { get; set; }
		private RaycastHit[] Grounds { get; set; }
		private IPlayerCharacter Character { get; set; }
		private Vector3 Scalar { get; set; }

		[SerializeField] private float edgeCheckRadious = 0.15f;
		[SerializeField] private float edgeHeightThreshold = 5f;
		[Range(0.1f, 1f)][SerializeField] private float edgeCheckOffset = 1f;
		[SerializeField] private LayerMask groundMask = 0;
		[SerializeField] private QueryTriggerInteraction interaction = QueryTriggerInteraction.Ignore;

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			Scalar = new(1, 0, 1);
			Character = owner.Owner;
			Edges = new Collider[1];
			Grounds = new RaycastHit[1];

			base.Initialize(owner);
		}

		protected override VoidOutput Run(VoidInput _)
		{
			var transform = Character.Transform;
			var checkOrigin = transform.position + Vector3.Scale(transform.forward, Scalar) * edgeCheckOffset;

			bool isHigh = Physics.RaycastNonAlloc(checkOrigin, -Vector3.up, Grounds, edgeHeightThreshold, groundMask, interaction) <= 0;
			bool isEdge = Physics.OverlapSphereNonAlloc(checkOrigin, edgeCheckRadious, Edges, groundMask, interaction) <= 0;

			Character.IsStandingOnEdge = isEdge && isHigh;

			return default;
		}
	}
}