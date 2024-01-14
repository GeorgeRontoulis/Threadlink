namespace Threadlink.Systems.Initium
{
	using System.Collections;
	using Threadlink.Core;
	using UnityEngine;

	public sealed class InitializableCollection : LinkableEntity
	{
		[SerializeField] private LinkableObject[] objects = new LinkableObject[0];

		public override void Boot() { }
		public override void Initialize() { }

		internal IEnumerator BootObjects()
		{
			yield return Initium.BootLinkableObjects(objects);
		}

		internal IEnumerator InitializeObjects()
		{
			yield return Initium.InitializeLinkableObjects(objects);
		}

		public override void Discard()
		{
			objects = null;
			base.Discard();
		}
	}
}