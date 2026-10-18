using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>
    /// Tiny pixel-art effect toolkit (ice shards, mist, sparks, warning strips). Every effect is a pooled
    /// SpriteRenderer using the project's own unlit sprite material, so nothing depends on particle
    /// shaders and the look stays point-filtered like the rest of the art. No audio is ever produced.
    /// </summary>
    public static class PixelFx
    {
        private static Material material;
        private static Sprite pixel, softDot, hardDot;
        private static Transform root;
        private static readonly Stack<PixelParticle> pool = new Stack<PixelParticle>();
        private static readonly HashSet<PixelParticle> live = new HashSet<PixelParticle>();

        public static Material Material => material;
        public static void Initialize(Material spriteMaterial) { if (spriteMaterial != null) material = spriteMaterial; }

        /// <summary>4×4 white square at 32 PPU (0.125 world units).</summary>
        public static Sprite Pixel { get { if (pixel == null) pixel = MakeSquare(); return pixel; } }
        /// <summary>16×16 soft radial glow at 16 PPU (1 world unit).</summary>
        public static Sprite SoftDot { get { if (softDot == null) softDot = MakeDot(true); return softDot; } }
        /// <summary>16×16 hard-edged pixel disc at 16 PPU (1 world unit).</summary>
        public static Sprite HardDot { get { if (hardDot == null) hardDot = MakeDot(false); return hardDot; } }

        private static Transform Root
        {
            get
            {
                if (root == null)
                {
                    pool.Clear(); live.Clear();
                    root = new GameObject("PixelFx").transform;
                }
                return root;
            }
        }

        public static SpriteRenderer CreateRenderer(string name, Transform parent, Sprite sprite, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite; r.sortingOrder = order; r.color = color;
            if (material != null) r.sharedMaterial = material;
            return r;
        }

        public static PixelParticle Emit(Vector2 position, Vector2 velocity, Color color, float life, float gravity, float size,
            int order = 40, Sprite sprite = null, float spin = 0, float drag = 0, bool growth = false)
        {
            PixelParticle p = null;
            while (pool.Count > 0 && p == null) p = pool.Pop();
            if (p == null)
            {
                var r = CreateRenderer("FxParticle", Root, Pixel, order, color);
                p = r.gameObject.AddComponent<PixelParticle>();
            }
            p.gameObject.SetActive(true);
            live.Add(p);
            p.Launch(position, velocity, color, life, gravity, size, order, sprite != null ? sprite : Pixel, spin, drag, growth);
            return p;
        }

        public static void Burst(Vector2 position, int count, Color color, float speed, float life, float gravity, float size,
            int order = 40, Vector2 bias = default, float spreadDegrees = 360, float baseAngle = 90, Sprite sprite = null)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + Random.Range(-spreadDegrees * 0.5f, spreadDegrees * 0.5f)) * Mathf.Deg2Rad;
                Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.45f, 1f) + bias;
                Color c = Color.Lerp(color, Color.white, Random.Range(0f, 0.35f)); c.a = color.a;
                Emit(position + Random.insideUnitCircle * size, v, c, life * Random.Range(0.7f, 1.2f), gravity,
                    size * Random.Range(0.7f, 1.3f), order, sprite, Random.Range(-180f, 180f));
            }
        }

        internal static void Recycle(PixelParticle p)
        {
            if (p == null) return;
            live.Remove(p);
            p.gameObject.SetActive(false);
            pool.Push(p);
        }

        /// <summary>Removes every live effect (run reset, phase change, boss defeat).</summary>
        public static void ClearAll()
        {
            if (root == null) { pool.Clear(); live.Clear(); return; }
            var copy = new List<PixelParticle>(live);
            foreach (var p in copy) Recycle(p);
        }

        private static Sprite MakeSquare()
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FxPixel" };
            var c = new Color32[16]; for (int i = 0; i < 16; i++) c[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(c); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 32);
        }

        private static Sprite MakeDot(bool soft)
        {
            const int n = 16;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = soft ? "FxSoftDot" : "FxHardDot" };
            var c = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - 7.5f, y - 7.5f).magnitude / 7.5f;
                    // Quantised falloff keeps the glow in stepped pixel-art bands.
                    float a = soft ? Mathf.Floor(Mathf.Clamp01(1 - d) * 4f) / 4f : d <= 1 ? 1 : 0;
                    c[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(c); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * 0.5f, n);
        }
    }
}
