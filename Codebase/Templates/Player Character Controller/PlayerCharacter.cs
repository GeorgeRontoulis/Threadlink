namespace Threadlink.Templates.PlayerCharacterController
{
	using UnityEngine;
	using System;

	public interface IPlayerCharacter
	{
		[Flags]
		public enum StateFlags
		{
			IsNeutral = 0,
			IsMoving = 1 << 0,
			IsSprinting = 1 << 1,
			IsGrounded = 1 << 2,
			IsStandingOverEdge = 1 << 3,
			IsControllingWeaponState = 1 << 4,
			IsWeaponStateControlAvailable = 1 << 5,
		}

		public Transform SelfTransform { get; }
		public Animator Animator { get; }
		public CharacterController Controller { get; }

		public StateFlags CurrentStateFlags { get; set; }
	}
}