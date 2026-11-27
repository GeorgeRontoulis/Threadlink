namespace Threadforge.Threadlink.Starter
{
    using UnityEngine;

    /// <summary>Colors a renderer with the active render pipeline's default material, so sample scenes carry no
    /// pipeline-specific material assets. Replace with authored materials in a real project.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public sealed class StarterTint : MonoBehaviour
    {
        private static Material template;

        [SerializeField]
        [Tooltip("Color applied to this renderer.")]
        private Color color = Color.white;

        private Material instance;

        /// <summary>The applied color.</summary>
        public Color Color
        {
            get => color;
            set
            {
                color = value;
                Apply();
            }
        }

        private void OnEnable()
        {
            if (template == null)
            {
                // A primitive receives the current pipeline's default material, which every build includes.
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                template = probe.GetComponent<Renderer>().sharedMaterial;
                if (Application.isPlaying)
                    Destroy(probe);
                else
                    DestroyImmediate(probe);
            }
            if (instance == null && template != null)
                instance = new Material(template) { name = "Starter Tint", hideFlags = HideFlags.DontSave };
            Apply();
        }

        private void Apply()
        {
            if (instance == null)
                return;
            instance.SetColor("_BaseColor", color);
            instance.SetColor("_Color", color);
            GetComponent<Renderer>().sharedMaterial = instance;
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
                Apply();
        }

        private void OnDestroy()
        {
            if (instance == null)
                return;
            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
        }
    }
}
