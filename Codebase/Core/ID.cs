namespace Threadlink.Core
{
	using UnityEngine;
	using Utilities.Collections;

	[CreateAssetMenu(menuName = "Threadlink/Link ID")]
	public sealed class ID : ScriptableObject, IIdentifiable
	{
		public string LinkID => name;
	}
}