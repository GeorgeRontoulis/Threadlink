namespace Threadlink.Core
{
	using MassTransit;
	using UnityEngine;
	using Utilities.Collections;

	[CreateAssetMenu(menuName = "Threadlink/Link ID")]
	public class ID : ScriptableObject, IIdentifiable
	{
		public virtual string LinkID => name;
		public virtual NewId InstanceID
		{
			get => NewId.Empty;
			set => _ = NewId.Empty;
		}
	}
}