namespace Threadlink.Templates.PlayerCharacterController
{
	using UnityEngine;

	public interface IPlayerCharacter
	{
		public Transform Transform { get; }
		public Animator Animator { get; }
		public CharacterController Controller { get; }

		public bool IsStandingOnEdge { get; set; }
		public bool IsGrounded { get; set; }
		public bool IsSprinting { get; set; }
		public bool IsMoving { get; }
	}
}