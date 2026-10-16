using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>Flat fan on the ground: melee swing area and melee attack telegraphs.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ArcView : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RendererColorId = Shader.PropertyToID("_RendererColor");

        MeshFilter filter;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;
        float hideAt = float.MaxValue;

        void Awake()
        {
            filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
        }

        public void Set(Vector2 origin, Vector2 dir, float radius, float arcDegrees, Color color, int sortingOrder, int sortingLayer = 0)
        {
            filter.sharedMesh = ArcMesh.Get(arcDegrees);
            transform.position = new Vector3(origin.x, origin.y, 0);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            transform.localScale = new Vector3(radius, radius, 1);
            block.SetColor(ColorId, color);
            block.SetColor(RendererColorId, color);
            block.SetTexture("_MainTex", Texture2D.whiteTexture);
            meshRenderer.SetPropertyBlock(block);
            meshRenderer.sortingOrder = sortingOrder;
            meshRenderer.sortingLayerID = sortingLayer;
            meshRenderer.enabled = true;
        }

        /// <summary>Shows the arc for a short real-time duration (cosmetic).</summary>
        public void Flash(Vector2 origin, Vector2 dir, float radius, float arcDegrees, Color color, float seconds, int sortingLayer = 0)
        {
            Set(origin, dir, radius, arcDegrees, color, -5, sortingLayer);
            hideAt = Time.time + seconds;
        }

        public void Hide()
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
            hideAt = float.MaxValue;
        }

        void Update()
        {
            if (Time.time >= hideAt) Hide();
        }
    }
}
