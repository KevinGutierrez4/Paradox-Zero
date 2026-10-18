using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class AxeSwipe : MonoBehaviour
    {
        private const int Size = 128;
        private SpriteRenderer draw;
        private Texture2D texture;
        private Sprite sprite;
        private readonly Color32[] pixels = new Color32[Size * Size];
        private readonly float[] radii = new float[Size * Size], angles = new float[Size * Size];
        public void Initialize(Material material)
        {
            draw = GetComponent<SpriteRenderer>(); draw.sharedMaterial = material; draw.sortingOrder = 11; draw.enabled = false;
        }
        private void EnsureTexture()
        {
            if (texture != null) return;
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "AxeSwipe", filterMode = FilterMode.Point };
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), Vector2.one * 0.5f, 64);
            draw.sprite = sprite;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x - 63.5f, y - 63.5f) / 64f;
                int i = y * Size + x; radii[i] = p.magnitude; angles[i] = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            }
        }
        public void Pose(float progress, float crouch)
        {
            if (draw == null) draw = GetComponent<SpriteRenderer>();
            draw.enabled = progress >= 0.32f && progress <= 0.57f;
            if (!draw.enabled) return;
            EnsureTexture();
            float cut = Mathf.Clamp01((progress - 0.30f) / 0.20f), fade = 1 - Mathf.Clamp01((progress - 0.50f) / 0.07f);
            float tip = Mathf.Lerp(145, -35, cut), length = Mathf.Min(95, cut * 180);
            transform.localPosition = new Vector2(0.20f, 1.28f - crouch * 0.34f);
            for (int i = 0; i < pixels.Length; i++)
            {
                float delta = Mathf.Repeat(angles[i] - tip, 360);
                if (delta > length || length < 1) { pixels[i] = new Color32(); continue; }
                float t = 1 - delta / length, width = 0.04f + 0.11f * t;
                float edge = Mathf.Abs(radii[i] - 0.87f) / width;
                if (edge >= 1) { pixels[i] = new Color32(); continue; }
                float opacity = (1 - edge) * t * fade;
                pixels[i] = new Color(1, Mathf.Lerp(0.65f, 0.95f, 1 - edge), Mathf.Lerp(0.32f, 0.82f, 1 - edge), opacity);
            }
            texture.SetPixels32(pixels); texture.Apply();
        }
        private void OnDisable() { if (draw != null) draw.enabled = false; }
        private void OnDestroy()
        {
            if (sprite != null) { if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite); }
            if (texture != null) { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
        }
    }
}
