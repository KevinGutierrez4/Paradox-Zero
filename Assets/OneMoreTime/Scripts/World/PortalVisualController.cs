using UnityEngine;

namespace OneMoreTime
{
    /// <summary>
    /// Builds and animates a simple Nordic rune portal in point-filtered pixel art: an oval stone ring,
    /// eight Elder Futhark runes, and a gently swirling energy core tinted per winter. No external art is
    /// required; textures are generated once per winter palette and shared.
    /// </summary>
    public sealed class PortalVisualController : MonoBehaviour
    {
        private const int W = 48, H = 76, PPU = 24;
        private static readonly Sprite[][] energyCache = new Sprite[4][];
        private static readonly Sprite[] runeCache = new Sprite[4];
        private static Sprite frameSprite;

        // 3×5 Elder Futhark glyphs (rows top→bottom): ᚠ ᚢ ᚦ ᚨ ᚱ ᚲ ᚷ ᛉ
        private static readonly string[] Glyphs =
        {
            "1.1" + "111" + "1.." + "11." + "1..",
            "11." + "1.1" + "1.1" + "1.1" + "1.1",
            "1.." + "11." + "1.1" + "11." + "1..",
            "1.." + "111" + "1.." + "111" + "1..",
            "11." + "1.1" + "11." + "1.1" + "1.1",
            "..1" + ".1." + "1.." + ".1." + "..1",
            "1.1" + ".1." + ".1." + ".1." + "1.1",
            "1.1" + "111" + ".1." + ".1." + ".1."
        };

        private SpriteRenderer frame, runes, energy;
        private Sprite[] frames;
        private int winter = 1;
        private float open, intensity, frameClock, particleClock;
        public float Open => open;

        public static Color Dark(int w) => w == 1 ? new Color(0.10f, 0.30f, 0.50f) : w == 2 ? new Color(0.22f, 0.13f, 0.46f) : new Color(0.42f, 0.07f, 0.07f);
        public static Color Mid(int w) => w == 1 ? new Color(0.35f, 0.70f, 0.95f) : w == 2 ? new Color(0.55f, 0.42f, 0.95f) : new Color(0.92f, 0.36f, 0.20f);
        public static Color Light(int w) => w == 1 ? new Color(0.82f, 0.96f, 1f) : w == 2 ? new Color(0.72f, 1f, 0.85f) : new Color(1f, 0.85f, 0.6f);

        public void Build(int winterIndex)
        {
            winter = Mathf.Clamp(winterIndex, 1, 3);
            if (frame == null)
            {
                energy = PixelFx.CreateRenderer("Energy", transform, null, -3, Color.white);
                frame = PixelFx.CreateRenderer("Frame", transform, FrameSprite(), -2, Color.white);
                runes = PixelFx.CreateRenderer("Runes", transform, null, -1, Color.white);
            }
            frames = EnergySprites(winter);
            energy.sprite = frames[0];
            runes.sprite = RuneSprite(winter);
            SetOpen(0); SetIntensity(0);
        }

        public void SetOpen(float value)
        {
            open = Mathf.Clamp01(value);
            float e = Mathf.SmoothStep(0, 1, open);
            // The ring unfolds from a thin slit standing on the ground (pivot at the base).
            transform.localScale = new Vector3(Mathf.Lerp(0.55f, 1f, e), Mathf.Max(0.04f, e), 1);
            gameObject.SetActive(open > 0.001f);
        }

        public void SetIntensity(float value) { intensity = Mathf.Clamp01(value); }

        private void Update()
        {
            if (frames == null || open <= 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            frameClock += dt * (8f + 10f * intensity);
            energy.sprite = frames[(int)frameClock % frames.Length];
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            var c = Color.Lerp(Color.white, Light(winter), intensity * 0.6f);
            energy.color = new Color(c.r, c.g, c.b, Mathf.SmoothStep(0, 1, (open - 0.3f) / 0.7f) * (0.85f + 0.15f * intensity));
            runes.color = new Color(1, 1, 1, Mathf.Clamp01(open * 1.4f - 0.2f) * Mathf.Lerp(pulse, 1f, intensity));
            particleClock -= dt;
            if (particleClock <= 0 && open > 0.6f)
            {
                // Discreet motes drifting up out of the core.
                particleClock = Mathf.Lerp(0.18f, 0.05f, intensity);
                Vector2 at = (Vector2)transform.position + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.4f, 2.6f));
                PixelFx.Emit(at, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 1.1f)), Color.Lerp(Mid(winter), Light(winter), Random.value),
                    Random.Range(0.6f, 1.1f), -0.2f, 0.06f, -1);
            }
        }

        private static float Ellipse(float x, float y)
        {
            float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
            float a = W * 0.5f - 0.5f, b = H * 0.5f - 0.5f;
            return Mathf.Sqrt((x - cx) * (x - cx) / (a * a) + (y - cy) * (y - cy) / (b * b));
        }

        private static Sprite Make(Color32[] pixels, string name)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            t.SetPixels32(pixels); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), PPU);
        }

        private static Sprite FrameSprite()
        {
            if (frameSprite != null) return frameSprite;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = Ellipse(x, y);
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (d <= 1f && d >= 0.80f)
                    {
                        bool outline = d > 0.955f || d < 0.835f;
                        // Light from the upper left, stone blocks every few pixels.
                        float light = Mathf.Clamp01(0.5f + ((W * 0.5f - x) / W + (y - H * 0.5f) / H) * 0.8f);
                        int block = (Mathf.FloorToInt(Mathf.Atan2(y - H * 0.5f, x - W * 0.5f) * 5.1f) & 1);
                        Color stone = Color.Lerp(new Color(0.20f, 0.22f, 0.27f), new Color(0.47f, 0.50f, 0.56f), light) * (block == 0 ? 1f : 0.88f);
                        c = outline ? new Color32(14, 16, 24, 255) : (Color32)new Color(stone.r, stone.g, stone.b, 1f);
                    }
                    px[y * W + x] = c;
                }
            frameSprite = Make(px, "PortalFrame");
            return frameSprite;
        }

        private static Sprite RuneSprite(int w)
        {
            if (runeCache[w] != null) return runeCache[w];
            var px = new Color32[W * H];
            Color32 glow = (Color32)Light(w), edge = (Color32)Mid(w);
            for (int g = 0; g < 8; g++)
            {
                float angle = (g / 8f) * Mathf.PI * 2 + Mathf.PI / 8;
                float r = 0.895f;
                int cx = Mathf.RoundToInt((W - 1) * 0.5f + Mathf.Cos(angle) * r * (W * 0.5f - 0.5f));
                int cy = Mathf.RoundToInt((H - 1) * 0.5f + Mathf.Sin(angle) * r * (H * 0.5f - 0.5f));
                string glyph = Glyphs[g];
                for (int gy = 0; gy < 5; gy++)
                    for (int gx = 0; gx < 3; gx++)
                    {
                        if (glyph[gy * 3 + gx] != '1') continue;
                        int x = cx - 1 + gx, y = cy + 2 - gy;
                        if (x < 0 || y < 0 || x >= W || y >= H) continue;
                        px[y * W + x] = gy == 0 || gy == 4 ? edge : glow;
                    }
            }
            runeCache[w] = Make(px, "PortalRunes" + w);
            return runeCache[w];
        }

        private static Sprite[] EnergySprites(int w)
        {
            if (energyCache[w] != null) return energyCache[w];
            var result = new Sprite[4];
            Color dark = Dark(w), mid = Mid(w), light = Light(w);
            for (int f = 0; f < 4; f++)
            {
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float d = Ellipse(x, y);
                        if (d >= 0.84f) continue;
                        float angle = Mathf.Atan2((y - H * 0.5f) / H, (x - W * 0.5f) / W);
                        float swirl = Mathf.Sin(angle * 3f + d * 11f - f * Mathf.PI * 0.5f);
                        // Three stepped bands plus a bright centre: reads as energy without dithering noise.
                        Color c = swirl > 0.55f ? light : swirl > -0.1f ? mid : dark;
                        if (d < 0.22f) c = Color.Lerp(c, light, 0.7f);
                        float alpha = d > 0.78f ? 0.65f : 0.92f;
                        if (((x * 7 + y * 13 + f * 5) % 61) == 0) { c = Color.white; alpha = 1; }
                        px[y * W + x] = (Color32)new Color(c.r, c.g, c.b, alpha);
                    }
                result[f] = Make(px, "PortalEnergy" + w + "_" + f);
            }
            energyCache[w] = result;
            return result;
        }
    }
}
