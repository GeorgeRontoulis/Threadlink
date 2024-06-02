namespace Threadlink.Utilities.Rendering
{
	using Sirenix.OdinInspector;
	using System;
	using UnityEngine;
	using String = Text.String;

	[Serializable]
	public sealed class GraphicsCache
	{
		public Material[] SharedMaterials => renderer.sharedMaterials;
		public Renderer Renderer => renderer;
		public bool IsValid => renderer != null && CachedMaterials != null && CachedMaterials.Length > 0;

		[ShowInInspector][ReadOnly] public Material[] CachedMaterials { get; set; }

		[SerializeField] private Renderer renderer = null;

		public void CacheMaterials() { CachedMaterials = renderer.materials; }
		public void CacheRenderer(Renderer renderer) { this.renderer = renderer; }

		public void Discard()
		{
			int length = CachedMaterials.Length;

			for (int i = 0; i < length; i++)
			{
				UnityEngine.Object.Destroy(CachedMaterials[i]);
				CachedMaterials[i] = null;
			}

			CachedMaterials = null;
			renderer = null;
		}

		public void SetRenderingState(bool state) { renderer.enabled = state; }

		public Material FindCachedMaterial(int index) { return CachedMaterials[index]; }

		public Material FindCachedMaterial(string name)
		{
			string instanceName = String.Construct(name, " (Instance)");

			int length = CachedMaterials.Length;
			for (int i = 0; i < length; i++)
			{
				if (CachedMaterials[i].name.Equals(instanceName)) return CachedMaterials[i];
			}

			return null;
		}
	}
}