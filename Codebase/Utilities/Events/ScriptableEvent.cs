namespace Threadlink.Utilities.Events
{
	using System;
	using UnityEngine;

	/// <summary>
	/// Zero-parameter event object meant for use inside the Editor
	/// in conjuction with Unity Events (see <see cref="ScriptableEventListener"/>), 
	/// mainly for rapid prototyping. Use in moderation, since multiple event 
	/// assets of this type get increasingly hard to keep track of as your project grows.
	/// </summary>
	[CreateAssetMenu(menuName = "Threadlink/Scriptable Events/Unity-Events-Compatible Event")]
	public sealed class ScriptableEvent : ScriptableObject
	{
		private Action Action { get; set; }

		private void OnEnable()
		{
			Action = null;
		}

		public void AddListener(Action action) { Action += action; }
		public void RemoveListener(Action action) { Action -= action; }
		public void Invoke() { Action?.Invoke(); }
	}
}
