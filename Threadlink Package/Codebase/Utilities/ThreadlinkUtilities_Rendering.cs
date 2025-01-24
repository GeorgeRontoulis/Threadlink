namespace Threadlink.Utilities.Rendering
{
	using Core.Subsystems.Scribe;
	using System;
	using UnityEngine;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	[Serializable]
	public sealed class GraphicsCache
	{
		public int CachedMaterialsCount => CachedMaterials.Length;

		public Material this[int index] => CachedMaterials[index];

		public Material this[string name]
		{
			get
			{
				string instanceName = Scribe.ToNonAllocText(name, " (Instance)").ToString();

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

		public void CacheRenderer(Renderer renderer) => this.renderer = renderer;
		public void CacheMaterials() => CachedMaterials = renderer.materials;
		public void SetRenderingState(bool state) => renderer.enabled = state;
	}

	public static class Rendering
	{
		public static void ToUnityTexturePixelLayout(this Color32[] pixelData, int width, int height)
		{
			Color32 tempColor;
			int halfHeight = Mathf.RoundToInt(height * 0.5f);

			for (int y = 0; y < halfHeight; y++)
			{
				int yMulWidth = y * width;
				int heightMinusY1MulWidth = (height - y - 1) * width;

				for (int x = 0; x < width; x++)
				{
					int customIndex = yMulWidth + x;
					int bottomRow = heightMinusY1MulWidth + x;

					tempColor = pixelData[customIndex];
					pixelData[customIndex] = pixelData[bottomRow];
					pixelData[bottomRow] = tempColor;
				}
			}
		}

		public static Color MoveTowards(this Color source, Color target, float coefficient)
		{
			source.r = Mathf.MoveTowards(source.r, target.r, coefficient);
			source.g = Mathf.MoveTowards(source.g, target.g, coefficient);
			source.b = Mathf.MoveTowards(source.b, target.b, coefficient);
			source.a = Mathf.MoveTowards(source.a, target.a, coefficient);

			return source;
		}

		public static bool Approximately(Color a, Color b)
		{
			return Mathf.Approximately(a.r, b.r)
			&& Mathf.Approximately(a.g, b.g)
			&& Mathf.Approximately(a.b, b.b)
			&& Mathf.Approximately(a.a, b.a);
		}

		public static Color WithAlpha(this Color color, float alpha)
		{
			alpha = Mathf.Clamp01(alpha);

			color.a = alpha;
			return color;
		}
	}
}