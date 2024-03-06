namespace Threadlink.Templates.PlayerCharacterController
{
	using Threadlink.StateMachines;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/State Machine")]
	public class PlayerCharacterStateMachine :
	AbstractStateMachine<IPlayerCharacter, PlayerCharacterState, PlayerCharacterProcessor>
	{
		protected override void InitializeStatesAndProcessors()
		{
			int length = processors.Length;

			for (int i = 0; i < length; i++) processors[i].Initialize(this);

			length = states.Length;

			for (int i = 0; i < length; i++) states[i].Initialize(this);
		}
	}
}