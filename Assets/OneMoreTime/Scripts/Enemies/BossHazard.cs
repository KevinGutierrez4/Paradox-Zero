using UnityEngine;

namespace OneMoreTime
{
    public enum BossHazardKind { Marker, IceFang, Crest, Cloud, Visual }

    /// <summary>
    /// Short-lived boss arena element. None of them is a projectile:
    /// <list type="bullet">
    /// <item>Marker: pulsing ground warning (no damage).</item>
    /// <item>IceFang: a spike that erupts from the floor, holds and retracts; damages only while mostly out.</item>
    /// <item>Crest: a ridge of scale plates rising out of the serpent's back and rolling along the floor (jumpable).</item>
    /// <item>Cloud: a lingering low cloud (venom) that damages in ticks; standing higher than it is safe.</item>
    /// <item>Visual: a purely decorative element owned by the boss (breath cone, glow strip).</item>
    /// </list>
    /// Uses gameplay time; every damage check intersects the player's real collider.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossHazard : MonoBehaviour
    {
        private BossHazardKind kind;
        private SpriteRenderer draw;
        private Color color;
        private float age, lifetime, damage, height, width, baseScale, pulse, speed, endX, tick, nextTick;
        private Vector2 ground;
        private PlayerController player;
        private Collider2D playerCollider;
        private GameManager game;
        private System.Func<float, float> groundAt;
        private Vector2 push;
        private bool hitOnce;

        public bool Finished { get; private set; }
        public BossHazardKind Kind => kind;
        public float Age => age;
        public float Lifetime => lifetime;
        public SpriteRenderer Renderer => draw;
        /// <summary>Horizontal centre of a travelling crest.</summary>
        public float X => ground.x;
        /// <summary>True while the element is able to hurt (for QA/tests).</summary>
        public bool Dangerous { get; private set; }

        private void Setup(BossHazardKind k, SpriteRenderer r, PlayerController target, GameManager manager)
        {
            kind = k; draw = r; player = target; game = manager;
            playerCollider = target != null ? target.GetComponent<Collider2D>() : null;
        }

        public static BossHazard Marker(Transform parent, Vector2 groundCenter, float markerWidth, Color tint, float duration, float pulseSpeed = 14f, float thickness = 0.14f)
        {
            var r = PixelFx.CreateRenderer("BossWarning", parent, PixelFx.Pixel, -1, tint);
            var h = r.gameObject.AddComponent<BossHazard>();
            h.Setup(BossHazardKind.Marker, r, null, null);
            h.color = tint; h.lifetime = duration; h.pulse = pulseSpeed;
            h.ground = groundCenter; h.width = markerWidth;
            float unit = PixelFx.Pixel.bounds.size.x;
            r.transform.position = groundCenter + Vector2.up * (thickness * 0.5f);
            r.transform.localScale = new Vector3(markerWidth / unit, thickness / unit, 1);
            return h;
        }

        public static BossHazard IceFang(Transform parent, Sprite sprite, Vector2 groundPoint, float fangHeight, float duration,
            float hitDamage, PlayerController target, GameManager manager, Color? tint = null, Vector2 knockback = default)
        {
            var r = PixelFx.CreateRenderer("IceFang", parent, sprite != null ? sprite : PixelFx.Pixel, 2, tint ?? Color.white);
            var h = r.gameObject.AddComponent<BossHazard>();
            h.Setup(BossHazardKind.IceFang, r, target, manager);
            h.color = tint ?? Color.white; h.lifetime = duration;
            h.ground = groundPoint; h.height = fangHeight; h.damage = hitDamage; h.push = knockback;
            h.baseScale = fangHeight / Mathf.Max(0.01f, r.sprite.bounds.size.y);
            h.width = r.sprite.bounds.size.x * h.baseScale;
            r.flipX = Random.value < 0.5f;
            h.ApplyFang(0);
            return h;
        }

        /// <summary>A crest of scale plates travelling from <paramref name="fromX"/> to <paramref name="toX"/> along the floor.</summary>
        public static BossHazard Crest(Transform parent, Sprite sprite, System.Func<float, float> groundFunction, float fromX, float toX, float crestSpeed,
            float crestHeight, float hitDamage, PlayerController target, GameManager manager, Color tint)
        {
            var r = PixelFx.CreateRenderer("ScaleCrest", parent, sprite != null ? sprite : PixelFx.Pixel, 3, tint);
            var h = r.gameObject.AddComponent<BossHazard>();
            h.Setup(BossHazardKind.Crest, r, target, manager);
            h.color = tint; h.groundAt = groundFunction; h.ground = new Vector2(fromX, groundFunction(fromX)); h.endX = toX;
            h.speed = Mathf.Abs(crestSpeed) * Mathf.Sign(toX - fromX); h.height = crestHeight; h.damage = hitDamage;
            h.lifetime = Mathf.Abs(toX - fromX) / Mathf.Max(0.1f, Mathf.Abs(crestSpeed)) + 0.3f;
            h.baseScale = crestHeight / Mathf.Max(0.01f, r.sprite.bounds.size.y);
            h.width = Mathf.Max(0.7f, r.sprite.bounds.size.x * h.baseScale * 0.75f);
            h.push = new Vector2(Mathf.Sign(h.speed) * 4f, 2.2f);
            if (h.speed < 0) r.flipX = true;
            h.ApplyCrest();
            return h;
        }

        /// <summary>Low lingering cloud. <paramref name="tickSeconds"/> between damage ticks.</summary>
        public static BossHazard Cloud(Transform parent, Sprite puff, Vector2 groundCenter, float cloudWidth, float cloudHeight, float duration,
            float tickDamage, float tickSeconds, PlayerController target, GameManager manager, Color tint)
        {
            var r = PixelFx.CreateRenderer("LingeringCloud", parent, puff != null ? puff : PixelFx.SoftDot, 30, new Color(tint.r, tint.g, tint.b, 0));
            var h = r.gameObject.AddComponent<BossHazard>();
            h.Setup(BossHazardKind.Cloud, r, target, manager);
            h.color = tint; h.ground = groundCenter; h.width = cloudWidth; h.height = cloudHeight; h.lifetime = duration;
            h.damage = tickDamage; h.tick = Mathf.Max(0.1f, tickSeconds); h.nextTick = 0.35f;
            var size = r.sprite.bounds.size;
            r.transform.position = groundCenter + Vector2.up * cloudHeight * 0.45f;
            r.transform.localScale = new Vector3(cloudWidth * 1.15f / size.x, cloudHeight * 1.3f / size.y, 1);
            return h;
        }

        /// <summary>Decorative element whose transform the boss drives (cleaned up with every other hazard).</summary>
        public static BossHazard Visual(Transform parent, Sprite sprite, int order, Color tint, float duration)
        {
            var r = PixelFx.CreateRenderer("BossVisual", parent, sprite != null ? sprite : PixelFx.Pixel, order, tint);
            var h = r.gameObject.AddComponent<BossHazard>();
            h.Setup(BossHazardKind.Visual, r, null, null);
            h.color = tint; h.lifetime = duration;
            return h;
        }

        private float Rise()
        {
            // 0.12 s eruption, hold, 0.22 s retraction.
            float up = Mathf.SmoothStep(0, 1, age / 0.12f);
            float down = Mathf.SmoothStep(0, 1, (lifetime - age) / 0.22f);
            return Mathf.Min(up, down);
        }

        private void ApplyFang(float s)
        {
            // Grows from the ground line (bottom stays planted) instead of popping in.
            transform.localScale = new Vector3(baseScale, baseScale * Mathf.Max(0.02f, s), 1);
            transform.position = ground + Vector2.up * (height * s * 0.5f - 0.05f);
        }

        private void ApplyCrest()
        {
            float up = Mathf.SmoothStep(0, 1, age / 0.18f);
            float down = Mathf.SmoothStep(0, 1, (lifetime - age) / 0.25f);
            float s = Mathf.Min(up, down);
            transform.localScale = new Vector3(baseScale, baseScale * Mathf.Max(0.02f, s), 1);
            // Bottom of the drawing on the floor whatever its pivot.
            float bottom = draw.sprite != null ? draw.sprite.bounds.min.y * transform.localScale.y : -height * s * 0.5f;
            transform.position = new Vector3(ground.x, ground.y - bottom - 0.1f, 0);
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age * 18f) * 3f);
        }

        private bool PlayerHit(Bounds box, Vector2 knockback)
        {
            if (game == null || !game.IsPlaying || player == null || playerCollider == null || player.Health.IsDead) return false;
            return box.Intersects(playerCollider.bounds) && player.Health.TakeDamage(new DamageInfo(damage, Team.Enemy, knockback));
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0 || Finished) return;
            age += dt;
            switch (kind)
            {
                case BossHazardKind.Marker:
                {
                    var c = color; c.a = color.a * (0.55f + 0.45f * Mathf.Sin(age * pulse));
                    draw.color = c;
                    break;
                }
                case BossHazardKind.IceFang:
                {
                    float s = Rise();
                    ApplyFang(s);
                    Dangerous = s > 0.6f;
                    if (Dangerous)
                    {
                        Vector2 k = push.sqrMagnitude > 0.01f ? push
                            : new Vector2(Mathf.Sign((player != null ? player.transform.position.x : ground.x) - ground.x + 0.001f) * 3f, 2.4f);
                        PlayerHit(new Bounds(ground + Vector2.up * height * s * 0.5f, new Vector3(width * 0.7f, height * s * 0.9f, 1)), k);
                    }
                    break;
                }
                case BossHazardKind.Crest:
                {
                    float x = ground.x + speed * dt;
                    if ((speed < 0 && x <= endX) || (speed > 0 && x >= endX)) { x = endX; if (lifetime - age > 0.25f) lifetime = age + 0.25f; }
                    ground = new Vector2(x, groundAt(x));
                    ApplyCrest();
                    Dangerous = age > 0.15f && lifetime - age > 0.15f;
                    if (Dangerous && !hitOnce)
                        hitOnce = PlayerHit(new Bounds(ground + Vector2.up * height * 0.42f, new Vector3(width, height * 0.8f, 1)), push);
                    if (Random.value < 0.35f)
                        PixelFx.Emit(ground + new Vector2(Random.Range(-0.3f, 0.3f), 0.1f), new Vector2(-speed * 0.1f + Random.Range(-0.6f, 0.6f), Random.Range(1f, 2.4f)),
                            Color.Lerp(color, Color.white, 0.5f), 0.4f, 6f, 0.07f, 31);
                    break;
                }
                case BossHazardKind.Cloud:
                {
                    float fade = Mathf.Min(Mathf.Clamp01(age / 0.4f), Mathf.Clamp01((lifetime - age) / 0.6f));
                    var c = color; c.a = color.a * fade * (0.8f + 0.2f * Mathf.Sin(age * 3f)); draw.color = c;
                    Dangerous = fade > 0.5f;
                    nextTick -= dt;
                    if (Dangerous && nextTick <= 0)
                    {
                        nextTick = tick;
                        PlayerHit(new Bounds(ground + Vector2.up * height * 0.5f, new Vector3(width, height, 1)), Vector2.zero);
                    }
                    if (Random.value < 0.3f * fade)
                        PixelFx.Emit(ground + new Vector2(Random.Range(-width * 0.5f, width * 0.5f), Random.Range(0.1f, height)),
                            new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.5f)), color, 0.9f, -0.2f, Random.Range(0.3f, 0.6f), 31, PixelFx.SoftDot, 0, 0.8f, true);
                    break;
                }
            }
            if (age >= lifetime) { Finished = true; Destroy(gameObject); }
        }

        /// <summary>Bounds of the area a cloud covers (for tests and AI).</summary>
        public Bounds Area => new Bounds(ground + Vector2.up * height * 0.5f, new Vector3(width, height, 1));
    }
}
