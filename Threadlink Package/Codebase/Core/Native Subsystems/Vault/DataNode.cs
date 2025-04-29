namespace Threadlink.Core.Subsystems.Vault
{
	using System;
	using UnityEngine;

	[Serializable]
	public class DataNode : IDiscardable
	{
		public virtual DataNodeIDs ID => iD;

		[SerializeField] protected DataNodeIDs iD = 0;

		public virtual void Discard() { }
	}

	[Serializable]
	public class DataNode<T> : DataNode
	{
		public virtual T Value
		{
			get => value;
			set
			{
				this.value = value;
				OnValueChanged?.Invoke(value);
			}
		}

		public event Action<T> OnValueChanged = null;

		[SerializeField] protected T value = default;

		public override void Discard()
		{
			value = default;
			OnValueChanged?.Invoke(default);
			OnValueChanged = null;
		}
	}

	#region Native Node Definitions:
	[Serializable] public sealed class Integer : DataNode<int> { }
	[Serializable] public sealed class Float : DataNode<float> { }
	[Serializable] public sealed class Boolean : DataNode<bool> { }
	[Serializable] public sealed class Double : DataNode<double> { }
	[Serializable] public sealed class Long : DataNode<long> { }
	[Serializable] public sealed class Label : DataNode<string> { }
	[Serializable] public sealed class Vector2D : DataNode<Vector2> { }
	[Serializable] public sealed class Vector3D : DataNode<Vector3> { }
	#endregion
}