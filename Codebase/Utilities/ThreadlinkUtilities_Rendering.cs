namespace Threadlink.Utilities.Rendering
{
	using System;
	using Threadlink.Utilities.Editor.Attributes;
	using UnityEngine;
	using String = Text.String;

	[Serializable]
	public sealed class GraphicsCache
	{
		public Material[] SharedMaterials { get => renderer.sharedMaterials; }
		public Renderer renderer = null;
		[ReadOnly] public Material[] materials = new Material[0];

		public void CacheMaterials() { materials = renderer.materials; }
		public void DiscardRenderer() { renderer = null; }

		public void DiscardCachedMaterials()
		{
			int length = materials.Length;

			for (int i = 0; i < length; i++)
			{
				UnityEngine.Object.Destroy(materials[i]);
				materials[i] = null;
			}

			materials = null;
		}

		public void SetRenderingState(bool state) { renderer.enabled = state; }

		public Material FindCachedMaterial(int index) { return materials[index]; }

		public Material FindCachedMaterial(string name)
		{
			string instanceName = String.Construct(name, " (Instance)");

			int length = materials.Length;
			for (int i = 0; i < length; i++)
			{
				if (materials[i].name.Equals(instanceName)) return materials[i];
			}

			return null;
		}
	}
}