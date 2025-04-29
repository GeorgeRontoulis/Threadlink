namespace Threadlink.Core.Subsystems.Vault
{
	using Core;
	using System.Collections.Generic;
	using UnityEngine;

	/// <summary>
	/// Threadlink's powerful and designer-friendly dynamic data container.
	/// Your designers should use this to lay out their data.
	/// It includes API for actually handling that data at runtime.
	/// Currently used in the <see cref="StateMachines"/> API to implement parameters.
	/// </summary>
	[CreateAssetMenu(menuName = "Threadlink/Vault/Create New")]
	public class Vault : LinkableAsset
	{
#if UNITY_EDITOR && ODIN_INSPECTOR
		[Sirenix.OdinInspector.DrawWithUnity]
#endif
		[SerializeReference, SerializeReferenceButton] private List<DataNode> dataNodes = new();

		public virtual bool TryAddNew<T>(DataNodeIDs nodeID, out T newNode) where T : DataNode, new()
		{
			newNode = null;

			int count = dataNodes.Count;
			for (int i = 0; i < count; i++) if (dataNodes[i].ID == nodeID) return false;

			dataNodes.Add(newNode = new T());

			return true;
		}

		public virtual bool Remove(DataNodeIDs nodeID)
		{
			int count = dataNodes.Count;
			for (int i = 0; i < count; i++)
			{
				if (dataNodes[i].ID == nodeID)
				{
					dataNodes.RemoveAt(i);
					return true;
				}
			}

			return false;
		}

		public virtual bool Has(DataNodeIDs nodeID)
		{
			int count = dataNodes.Count;
			for (int i = 0; i < count; i++) if (dataNodes[i].ID == nodeID) return true;

			return false;
		}

		public virtual bool TryGetDataOfType<T>(DataNodeIDs nodeID, out DataNode<T> result)
		{
			int count = dataNodes.Count;
			for (int i = 0; i < count; i++)
			{
				var candidate = dataNodes[i];

				if (candidate.ID == nodeID && candidate is DataNode<T> convertedCandidate)
				{
					result = convertedCandidate;
					return true;
				}
			}

			result = null;
			return false;
		}

		public virtual bool TryGet<T>(DataNodeIDs nodeID, out T value)
		{
			bool retrieved = TryGetDataOfType<T>(nodeID, out var retrievedNode);

			value = retrieved ? retrievedNode.Value : default;

			return retrieved;
		}

		public virtual bool TrySet<T>(DataNodeIDs nodeID, T value)
		{
			bool retrieved = TryGetDataOfType<T>(nodeID, out var retrievedNode);

			if (retrieved) retrievedNode.Value = value;

			return retrieved;
		}
	}
}
