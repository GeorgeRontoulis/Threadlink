namespace Threadlink.Systems.Nexus
{
	using System.Collections;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Nexus/Standard Scene Entry")]
	internal sealed class StandardSceneEntry : SceneEntry
	{
		public override IEnumerator PostLoadingCoroutine() { yield break; }
	}
}