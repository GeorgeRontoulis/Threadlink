namespace Threadlink.Utilities.Rendering
{
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif
	using System;
	using UnityEngine;
	using Utilities.Text;

	[Serializable]
	public sealed class GraphicsCache
	{
		public int CachedMaterialsCount => CachedMaterials.Length;

		public Material this[int index] => CachedMaterials[index];

		public Material this[string name]
		{
			get
			{
				string instanceName = TLZString.Construct(name, " (Instance)");

				int length = CachedMaterials.Length;
				for (int i = 0; i < length; i++)
				{
					if (CachedMaterials[i].name.Equals(instanceName)) return CachedMaterials[i];
				}

				return null;
			}
		}

		public Material[] SharedMaterials => renderer.sharedMaterials;
		public Renderer Renderer => renderer;
		public bool IsValid => renderer != null && CachedMaterials != null && CachedMaterials.Length > 0;

#if ODIN_INSPECTOR
		[ShowInInspector]
		[ReadOnly]
#endif
		private Material[] CachedMaterials { get; set; }

		[SerializeField] private Renderer renderer = null;

		public void CacheMaterials() { CachedMaterials = renderer.materials; }
		public void CacheRenderer(Renderer renderer) { this.renderer = renderer; }

		public void Discard()
		{
			int length = CachedMaterials.Length;

			for (int i = 0; i < length; i++)
			{
				ref var material = ref CachedMaterials[i];

				UnityEngine.Object.Destroy(material);
				material = null;
			}

			CachedMaterials = null;
			renderer = null;
		}

		public void SetRenderingState(bool state) { renderer.enabled = state; }
	}

	public static class Rendering
	{
		public static void ToUnityTexturePixelLayout(this Color32[] pixelData, int width, int height)
		{
			Color32 tempColor;
			int halfHeight = Mathf.RoundToInt(height * 0.5f);

			for (int y = 0; y < halfHeight; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int customIndex = y * width + x;
					int bottomRow = (height - y - 1) * width + x;

					tempColor = pixelData[customIndex];
					pixelData[customIndex] = pixelData[bottomRow];
					pixelData[bottomRow] = tempColor;
				}
			}
		}
	}
}