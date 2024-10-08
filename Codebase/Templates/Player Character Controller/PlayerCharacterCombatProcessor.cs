namespace Threadlink.Templates.PlayerCharacterController
{
	using System.Collections.Generic;
	using Utilities.Events;
	using UnityEngine;
	using System;
	using Sirenix.OdinInspector;
	using Utilities.Mathematics;
	using System.Linq;

#if UNITY_EDITOR
	using UnityEditor.Animations;
	using UnityEditor;
	using Threadlink.Utilities.Editor.Attributes;
#endif

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/Combat")]
	internal sealed class PlayerCharacterCombatProcessor : PlayerCharacterProcessor
	{
		[Serializable]
		internal sealed class AttackAnimationData
		{
			[Serializable]
			internal sealed class Event
			{
				internal AnimationEvent AsAnimationEvent => new()
				{
					time = timeInSeconds,
					intParameter = integerInput,
					floatParameter = floatInput,
					stringParameter = stringInput,
					functionName = methodInput,
					objectReferenceParameter = scriptableEventInput
				};

				[HideInInspector][SerializeField] private float targetClipDuration = 0f;

				[OnValueChanged("UpdateTime")]
				[Range(0f, 1f)][SerializeField] private float normalizedTime = 0.5f;
				[Sirenix.OdinInspector.ReadOnly][SerializeField] private float timeInSeconds = 0f;

				[Space(10)]

				[SerializeField] private int integerInput = 0;
				[SerializeField] private float floatInput = 0f;
				[SerializeField] private string stringInput = string.Empty;
				[SerializeField] private string methodInput = "React";
				[SerializeField] private ScriptableEvent scriptableEventInput = null;

				internal Event(float targetClipDuration)
				{
					UpdateTimeOnAnimClipChange(targetClipDuration);
				}

				internal void UpdateTimeOnAnimClipChange(float newDuration)
				{
					targetClipDuration = newDuration;
					UpdateTime();
				}

				private void UpdateTime()
				{
					timeInSeconds = Mathematics.Denormalize(normalizedTime, 0f, targetClipDuration);
				}
			}

#if UNITY_EDITOR && ODIN_INSPECTOR
#pragma warning disable IDE0051
			private IEnumerable<ValueDropdownItem> GetAnimatorControllerClipNames()
			{
				var result = new List<ValueDropdownItem>();

				var selectedObject = Selection.activeObject;

				if (selectedObject == null) return result;

				var owner = selectedObject as PlayerCharacterCombatProcessor;

				if (owner == null || owner.animatorController == null) return result;

				var clips = owner.animatorController.animationClips;
				int length = clips.Length;

				for (int i = 0; i < length; i++)
				{
					var animClip = clips[i];
					string name = animClip.name;

					result.Add(new(name, animClip));
				}

				return result;
			}
#pragma warning restore IDE0051
			[ValueDropdown("GetAnimatorControllerClipNames", SortDropdownItems = true)]
			[OnValueChanged("UpdateEvents")]
#endif
			[SerializeField] internal AnimationClip targetClip;

			[Space(10)]

#if UNITY_EDITOR
			[MinMaxRange(0f, 1f, decimals: 2)]
#endif
			[SerializeField] internal Vector2 normalizedComboTime = new(0, 1);

			[Space(10)]

			[ListDrawerSettings(CustomAddFunction = "AddEvent", AlwaysAddDefaultValue = false, AddCopiesLastElement = true)]
			[SerializeField] internal Event[] animationEvents = new Event[0];

#if UNITY_EDITOR
#pragma warning disable IDE0051
			private void UpdateEvents()
			{
				int length = animationEvents.Length;

				for (int i = 0; i < length; i++) animationEvents[i].UpdateTimeOnAnimClipChange(targetClip.length);
			}

			private void AddEvent()
			{
				if (targetClip == null) return;

				var list = animationEvents.ToList();

				list.Add(new(targetClip.length));

				animationEvents = list.ToArray();
			}
#pragma warning restore IDE0051
#endif
		}

#if UNITY_EDITOR
		[Space(20)]

		[SerializeField] private AnimatorController animatorController = null;
#endif

		[Space(10)]

		[SerializeField] private AttackAnimationData[] attackAnimations = new AttackAnimationData[0];

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			void FindRuntimeClipByName(string clipName, out AnimationClip result)
			{
				var clips = owner.Owner.Animator.runtimeAnimatorController.animationClips;
				int length = clips.Length;

				for (int i = 0; i < length; i++)
				{
					var candidate = clips[i];

					if (candidate.name.Equals(clipName))
					{
						result = candidate;
						return;
					}
				}

				result = null;
			}

			int attacksLength = attackAnimations.Length;

			for (int i = 0; i < attacksLength; i++)
			{
				var attack = attackAnimations[i];
				var events = attack.animationEvents;

				FindRuntimeClipByName(attack.targetClip.name, out var clip);

				int eventsLength = events.Length;
				for (int j = 0; j < eventsLength; j++) clip.AddEvent(events[i].AsAnimationEvent);
			}

			base.Initialize(owner);
		}

		protected override VoidOutput Run(VoidInput _)
		{
			return default;
		}
	}
}
