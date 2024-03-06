namespace Threadlink.Systems.Anima
{
	using Threadlink.Core;
	using Threadlink.Utilities.Events;
	using UnityEngine;
	using UnityEngine.Animations;
	using UnityEngine.Playables;

	[RequireComponent(typeof(Animator))]
	public sealed class AnimaRig : LinkableBehaviour
	{
		public PlayableGraph Graph { get; private set; }
		public AnimationMixerPlayable SourcePlayable { get; private set; }

		private void OnDestroy()
		{
			if (Graph.IsValid()) Graph.Destroy();
		}

		public override void Discard()
		{
			if (Graph.IsValid()) Graph.Destroy();

			base.Discard();
		}

		public override void Boot()
		{
			Graph = PlayableGraph.Create(name);
			Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

			SourcePlayable = AnimationMixerPlayable.Create(Graph, 0);
			SourcePlayable.Play();

			var output = AnimationPlayableOutput.Create(Graph, "Animation", GetComponent<Animator>());
			output.SetSourcePlayable(SourcePlayable);
		}

		public override void Initialize()
		{
			Graph.Play();
			Iris.SubscribeToUpdate(Evaluate);
		}

		private VoidOutput Evaluate(VoidInput input)
		{
			Graph.Evaluate(Chronos.DeltaTime);
			return default;
		}
	}
}