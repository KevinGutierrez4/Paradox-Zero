using UnityEngine;

namespace OneMoreTime
{
    /// <summary>Reusable burst attached to the physics root, so phase teleports carry the effect.</summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeFeedback : MonoBehaviour
    {
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[12];
        private SpriteRenderer ring;
        private Sprite sparkSprite, ringSprite;
        private Texture2D sparkTexture, ringTexture;
        private float elapsed;
        private bool pact;
        public bool IsActive { get; private set; }
        public bool IsPact => pact;
        private void Awake()
        {
            var source = GetComponentInChildren<SpriteRenderer>();
            Material material = source != null ? source.sharedMaterial : null;
            sparkTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[64];
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                pixels[y * 8 + x] = Mathf.Abs(x - 3.5f) + Mathf.Abs(y - 3.5f) < 4 ? new Color32(255, 255, 255, 255) : new Color32();
            sparkTexture.SetPixels32(pixels); sparkTexture.Apply();
            sparkSprite = Sprite.Create(sparkTexture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f, 8);
            ringTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            pixels = new Color32[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                pixels[y * 32 + x] = radius > 12.5f && radius < 15 ? new Color32(255, 255, 255, 255) : new Color32();
            }
            ringTexture.SetPixels32(pixels); ringTexture.Apply();
            ringSprite = Sprite.Create(ringTexture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 16);
            ring = MakeRenderer("UpgradeRing", ringSprite, material);
            for (int i = 0; i < sparks.Length; i++) sparks[i] = MakeRenderer("UpgradeSpark_" + i, sparkSprite, material);
            Cancel();
        }
        private SpriteRenderer MakeRenderer(string name, Sprite sprite, Material material)
        {
            var child = new GameObject(name).transform; child.SetParent(transform, false);
            var renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; if (material != null) renderer.sharedMaterial = material;
            renderer.sortingOrder = 90; return renderer;
        }
        public void Play(UpgradeDefinition upgrade)
        {
            pact = upgrade.HasTradeoff; elapsed = 0; IsActive = true;
            ring.enabled = true; foreach (var spark in sparks) spark.enabled = true;
            Pose(0);
        }
        private void Update()
        {
            if (!IsActive) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 1.05f); Pose(t);
            if (t >= 1) Cancel();
        }
        private void Pose(float t)
        {
            Color positive = pact ? new Color(0.75f, 0.45f, 1) : new Color(0.25f, 1, 0.65f);
            ring.transform.localPosition = Vector3.up;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.38f, 0.95f, t);
            ring.color = new Color(positive.r, positive.g, positive.b, (1 - t) * 0.85f);
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / sparks.Length + t * 1.1f;
                float radius = Mathf.Lerp(0.18f, 0.9f, Mathf.Sqrt(t));
                var spark = sparks[i];
                spark.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 1 + Mathf.Sin(angle) * radius * 0.75f + t * 0.55f, 0);
                spark.transform.localScale = Vector3.one * Mathf.Lerp(0.11f, 0.025f, t);
                Color tint = pact && i % 3 == 0 ? new Color(1, 0.3f, 0.25f) : positive;
                tint.a = 1 - t; spark.color = tint;
            }
        }
        public void Cancel()
        {
            IsActive = false;
            if (ring != null) ring.enabled = false;
            foreach (var spark in sparks) if (spark != null) spark.enabled = false;
        }
        private void OnDisable() { Cancel(); }
        private void OnDestroy()
        {
            if (sparkSprite != null) Destroy(sparkSprite); if (ringSprite != null) Destroy(ringSprite);
            if (sparkTexture != null) Destroy(sparkTexture); if (ringTexture != null) Destroy(ringTexture);
        }
    }
}
