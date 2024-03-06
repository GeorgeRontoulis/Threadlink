namespace Threadlink.Systems.Anima
{
	using Threadlink.Core;
	using Threadlink.Utilities.Events;
	using Threadlink.Utilities.Mathematics;
	using UnityEngine;
	using UnityEngine.Animations;
	using UnityEngine.Playables;

	public sealed class AnimaHandle
	{
		public float MasterInput { get; set; }
		public int InputCount { get; private set; }
		public float[] Times { get; private set; }
		public float[] NormalizedTimes { get; private set; }

		public int ActiveInput
		{
			get
			{
				if (HandledMixer.IsValid())
				{
					float maxWeight = -1;
					int result = 0;

					for (int i = 0; i < InputCount; i++)
					{
						float weight = HandledMixer.GetInputWeight(i);

						if (weight > maxWeight)
						{
							maxWeight = weight;
							result = i;
						}
					}

					return result;
				}
				else return -1;
			}
		}

		public float ActiveInputNormalizedTime => NormalizedTimes[ActiveInput];

		private AnimationMixerPlayable HandledMixer { get; set; }

		public AnimaHandle(AnimationMixerPlayable mixer, int activePort = 0, bool updateAutomatically = true)
		{
			MasterInput = 0;

			if (mixer.IsValid())
			{
				HandledMixer = mixer;

				InputCount = HandledMixer.GetInputCount();
				Times = new float[InputCount];
				NormalizedTimes = new float[InputCount];

				for (int i = 0; i < InputCount; i++) HandledMixer.SetInputWeight(i, 0);

				if (activePort >= 0 && activePort < HandledMixer.GetInputCount())
					HandledMixer.SetInputWeight(activePort, 1);

				if (updateAutomatically) Iris.SubscribeToUpdate(Update);
			}
			else
			{
				NormalizedTimes = null;
				Times = null;
				InputCount = 0;
				HandledMixer = AnimationMixerPlayable.Null;
			}
		}

		public void Release(PlayableGraph owner)
		{
			if (owner.IsValid())
			{
				Iris.UnsubscribeFromUpdate(Update);
				if (HandledMixer.IsValid()) owner.DestroyPlayable(HandledMixer);
			}
		}

		public VoidOutput Update(VoidInput input)
		{
			if (HandledMixer.IsValid())
			{
				for (int i = 0; i < InputCount; i++)
				{
					float destinationWeight = CalculateWeight(i);
					HandledMixer.SetInputWeight(i, destinationWeight);
				}

				CalculateNormalizedTimes();
			}

			return default;
		}

		private void CalculateNormalizedTimes()
		{
			for (int i = 0; i < InputCount; i++)
			{
				Playable input = HandledMixer.GetInput(i);

				if (input.IsPlayableOfType<AnimationClipPlayable>() == false) continue;

				var playable = (AnimationClipPlayable)HandledMixer.GetInput(i);
				float clipDuration = playable.GetAnimationClip().length;

				if (Times[i] >= clipDuration) Times[i] = 0f;

				Times[i] += Chronos.DeltaTime;
				NormalizedTimes[i] = Mathematics.NormalizeBetween(Times[i], 0, clipDuration);
			}
		}

		private float CalculateWeight(int inputPort)
		{
			float distance = Mathf.Abs(Mathf.Clamp(MasterInput, 0, InputCount - 1) - inputPort);
			float weight = Mathf.Clamp01(1f - distance);
			return weight;
		}
	}

	public sealed class Anima : LinkableBehaviourSingleton<Anima>
	{
		public static float BlendingCoefficient => Instance.blendingCoefficient;

		[SerializeField] private float blendingCoefficient = 8;

		public override void Boot()
		{
			Instance = this;
		}

		public override void Initialize()
		{
		}

		public static AnimaHandle InjectPlayablesInto(AnimaRig rig, int activePort, bool updateAutomatically = true, params AnimaInjectable[] injectables)
		{
			var targetGraph = rig.Graph;
			int length = injectables.Length;

			if (targetGraph.IsValid() || injectables == null || length <= 0)
			{
				#region Conversion of the Injectables to Playables:
				var handleMixer = AnimationMixerPlayable.Create(targetGraph, length);

				for (int i = 0; i < length; i++)
					targetGraph.Connect(injectables[i].ToPlayable(rig), 0, handleMixer, i);

				handleMixer.Play();
				#endregion

				#region Connection of the generated Playables to the main Source Playable of this Anima Rig:
				rig.SourcePlayable.AddInput(handleMixer, 0);
				#endregion

				return new AnimaHandle(handleMixer, activePort, updateAutomatically);
			}
			else return new AnimaHandle(AnimationMixerPlayable.Null);
		}
	}
}