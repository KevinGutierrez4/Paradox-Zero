using UnityEngine;

namespace OneMoreTime
{
    public enum CrumbleState { Idle, Cracking, Breaking, Destroyed, Respawning }

    /// <summary>
    /// Fixed platform of Jörmungandr scales that gives way after the player stands on it.
    /// Cycle: Idle → Cracking (stand timer, cracks grow, last seconds tremble and flicker) → Breaking (the five
    /// pieces of the slab fall apart, the collider is already off) → Destroyed (invisible, no collision) →
    /// Respawning (pieces come back; the collider only returns once the player is out of the way) → Idle.
    /// Stepping off before it breaks resets the timer. One state machine, so there is never more than one timer.
    /// Never moves: only its pieces animate, the root and the collider stay where they were placed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrumblingPlatform : MonoBehaviour
    {
        [Header("Tiempos (segundos)")]
        [Tooltip("Tiempo total de permanencia del jugador antes de romperse.")]
        [SerializeField, Min(0.2f)] private float timeToBreak = 2f;
        [Tooltip("Advertencia visual (temblor y parpadeo) durante los últimos segundos antes de romperse.")]
        [SerializeField, Min(0f)] private float warningTime = 0.6f;
        [Tooltip("Tiempo que permanece destruida, contado desde el momento de la rotura.")]
        [SerializeField, Min(0.5f)] private float respawnTime = 3f;
        [Tooltip("Duración de la animación de rotura (fragmentos cayendo).")]
        [SerializeField, Min(0.05f)] private float breakAnimation = 0.45f;
        [Tooltip("Duración de la animación de reaparición.")]
        [SerializeField, Min(0.05f)] private float respawnAnimation = 0.4f;
        [Header("Comportamiento")]
        [SerializeField] private bool startActive = true;
        [SerializeField] private bool animatedDestruction = true;
        [Header("Arte (sprites existentes del proyecto)")]
        [SerializeField] private SpriteRenderer slab;
        [SerializeField] private SpriteRenderer crack;
        [SerializeField] private Sprite[] crackStages = new Sprite[0];
        [SerializeField] private SpriteRenderer[] shards = new SpriteRenderer[0];

        private Collider2D surface;
        private PlayerController player;
        private Collider2D playerBody;
        private float standTimer, stateTimer, destroyedAt;
        private Vector3[] shardHome = new Vector3[0];
        private Vector2[] shardVelocity = new Vector2[0];
        private float[] shardSpin = new float[0];

        public CrumbleState State { get; private set; } = CrumbleState.Idle;
        public float TimeToBreak => timeToBreak;
        public float WarningTime => warningTime;
        public float RespawnTime => respawnTime;
        public float StandTimer => standTimer;
        public bool Warning => State == CrumbleState.Cracking && standTimer >= timeToBreak - warningTime;
        public bool Solid => surface != null && surface.enabled;
        public bool Visible => slab != null && slab.enabled && slab.color.a > 0.05f;
        public Collider2D Surface => surface;
        public float TopY => surface.bounds.max.y;
        /// <summary>Time.time of the last break (QA: respawn is measured from it).</summary>
        public float DestroyedAt => destroyedAt;
        public int BreakCount { get; private set; }
        public int RespawnCount { get; private set; }
        /// <summary>Seconds the respawn waited because the player occupied the space.</summary>
        public float LastRespawnWait { get; private set; }

        public void ConfigureArt(SpriteRenderer slabRenderer, SpriteRenderer crackRenderer, Sprite[] stages, SpriteRenderer[] pieces)
        { slab = slabRenderer; crack = crackRenderer; crackStages = stages ?? new Sprite[0]; shards = pieces ?? new SpriteRenderer[0]; }

        public void ConfigureTiming(float breakSeconds, float warningSeconds, float respawnSeconds)
        {
            timeToBreak = Mathf.Max(0.2f, breakSeconds); warningTime = Mathf.Clamp(warningSeconds, 0, timeToBreak); respawnTime = Mathf.Max(0.5f, respawnSeconds);
        }

        private void Awake()
        {
            surface = GetComponent<Collider2D>();
            shardHome = new Vector3[shards.Length]; shardVelocity = new Vector2[shards.Length]; shardSpin = new float[shards.Length];
            for (int i = 0; i < shards.Length; i++) if (shards[i] != null) shardHome[i] = shards[i].transform.localPosition;
        }

        private void Start()
        {
            var game = FindAnyObjectByType<GameManager>();
            player = game != null ? game.Player : FindAnyObjectByType<PlayerController>();
            playerBody = player != null ? player.GetComponent<Collider2D>() : null;
            if (startActive) EnterIdle(); else { BreakNow(false); destroyedAt = Time.time; }
        }

        private bool PlayerOnTop => player != null && surface != null && surface.enabled && player.GroundCollider == surface && !player.Health.IsDead;

        /// <summary>The player overlaps the space the slab occupies (respawning there would trap or push him).</summary>
        public bool PlayerBlocksRespawn()
        {
            if (playerBody == null || surface == null) return false;
            // The slab's own volume (a head grazing its underside does not count) plus a thin band above it.
            var b = surface.bounds;
            var region = new Bounds(); region.SetMinMax(new Vector3(b.min.x - 0.05f, b.min.y + 0.06f, -1), new Vector3(b.max.x + 0.05f, b.max.y + 0.1f, 1));
            return region.Intersects(playerBody.bounds);
        }

        private void FixedUpdate()
        {
            // Standing time is measured on the physics step, the same clock the ground check uses.
            if (State == CrumbleState.Idle && PlayerOnTop) { State = CrumbleState.Cracking; standTimer = 0; }
            if (State != CrumbleState.Cracking) return;
            if (!PlayerOnTop) { EnterIdle(); return; } // stepped off before it broke: the count starts again next time
            standTimer += Time.fixedDeltaTime;
            if (standTimer >= timeToBreak) BreakNow(animatedDestruction);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            stateTimer += dt;
            switch (State)
            {
                case CrumbleState.Cracking: AnimateCracking(); break;
                case CrumbleState.Breaking:
                    AnimateShards(dt, true);
                    if (stateTimer >= breakAnimation) EnterDestroyed();
                    break;
                case CrumbleState.Destroyed:
                    // The reassembly starts early so the slab is whole exactly when the 3 s are over.
                    if (Time.time - destroyedAt >= respawnTime - respawnAnimation) EnterRespawning();
                    break;
                case CrumbleState.Respawning: AnimateRespawn(); break;
            }
        }

        private void EnterIdle()
        {
            State = CrumbleState.Idle; standTimer = 0; stateTimer = 0;
            if (surface != null) surface.enabled = true;
            SetWhole(true, 1f);
            if (crack != null) crack.enabled = false;
            if (slab != null) slab.transform.localPosition = Vector3.zero;
        }

        private void BreakNow(bool animate)
        {
            State = CrumbleState.Breaking; stateTimer = 0; standTimer = 0; destroyedAt = Time.time; BreakCount++;
            // Collision goes first: the player falls with gravity the very next physics step, never on air.
            if (surface != null) surface.enabled = false;
            SetWhole(false, 1f);
            if (crack != null) crack.enabled = false;
            Vector2 center = surface != null ? (Vector2)surface.bounds.center : (Vector2)transform.position;
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] == null) continue;
                shards[i].enabled = animate; shards[i].color = Color.white;
                shards[i].transform.localPosition = shardHome[i]; shards[i].transform.localRotation = Quaternion.identity;
                float side = Mathf.Sign(shards[i].transform.position.x - center.x + 0.001f);
                shardVelocity[i] = new Vector2(side * Random.Range(0.6f, 1.6f), Random.Range(0.6f, 1.8f));
                shardSpin[i] = side * Random.Range(90f, 260f);
            }
            if (!animate) { EnterDestroyed(); return; }
            for (int i = 0; i < 14; i++)
                PixelFx.Emit(center + new Vector2(Random.Range(-1.1f, 1.1f), Random.Range(-0.1f, 0.3f)), new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.5f, 2.6f)),
                    i % 3 == 0 ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.72f, 0.88f, 1f, 0.95f), Random.Range(0.4f, 0.7f), 7f, Random.Range(0.05f, 0.1f), 31);
        }

        private void EnterDestroyed()
        {
            State = CrumbleState.Destroyed; stateTimer = 0;
            foreach (var s in shards) if (s != null) s.enabled = false;
            SetWhole(false, 0);
        }

        private void EnterRespawning()
        {
            State = CrumbleState.Respawning; stateTimer = 0; LastRespawnWait = 0;
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] == null) continue;
                shards[i].enabled = true; shards[i].transform.localRotation = Quaternion.Euler(0, 0, shardSpin[i] * 0.15f);
            }
        }

        private void AnimateCracking()
        {
            if (crack == null || crackStages.Length == 0) return;
            float k = Mathf.Clamp01(standTimer / timeToBreak);
            crack.enabled = true;
            crack.sprite = crackStages[Mathf.Min(crackStages.Length - 1, Mathf.FloorToInt(k * crackStages.Length))];
            if (Warning)
            {
                // Warning: tremble, flicker and loose ice chips under the slab.
                float w = Mathf.InverseLerp(timeToBreak - warningTime, timeToBreak, standTimer);
                slab.transform.localPosition = new Vector3(Mathf.Sin(stateTimer * 75f) * 0.035f * (0.5f + w), 0, 0);
                bool dim = Mathf.Repeat(stateTimer * (10f + 10f * w), 1f) < 0.5f;
                slab.color = dim ? new Color(0.78f, 0.88f, 1f) : Color.white;
                crack.color = new Color(1, 1, 1, dim ? 0.75f : 1f);
                if (Random.value < 0.35f && surface != null)
                    PixelFx.Emit(new Vector2(Random.Range(surface.bounds.min.x, surface.bounds.max.x), surface.bounds.min.y - 0.15f), new Vector2(Random.Range(-0.3f, 0.3f), -0.8f),
                        new Color(0.85f, 0.95f, 1f), 0.5f, 6f, 0.06f, 31);
            }
            else { slab.transform.localPosition = Vector3.zero; slab.color = Color.white; crack.color = Color.white; }
        }

        private void AnimateShards(float dt, bool falling)
        {
            float fade = 1 - Mathf.Clamp01(stateTimer / breakAnimation);
            for (int i = 0; i < shards.Length; i++)
            {
                var s = shards[i]; if (s == null) continue;
                shardVelocity[i] += Vector2.down * 14f * dt;
                s.transform.localPosition += (Vector3)(shardVelocity[i] * dt);
                s.transform.Rotate(0, 0, shardSpin[i] * dt);
                s.color = new Color(1, 1, 1, fade);
            }
        }

        private void AnimateRespawn()
        {
            // The pieces drift back into place and fade in; then the whole slab returns.
            float k = Mathf.Clamp01(1 - (respawnTime - (Time.time - destroyedAt)) / respawnAnimation);
            float e = Mathf.SmoothStep(0, 1, k);
            for (int i = 0; i < shards.Length; i++)
            {
                var s = shards[i]; if (s == null) continue;
                s.transform.localPosition = shardHome[i] + new Vector3(0, -0.35f * (1 - e), 0);
                s.transform.localRotation = Quaternion.Euler(0, 0, shardSpin[i] * 0.15f * (1 - e));
                s.color = new Color(1, 1, 1, 0.25f + 0.75f * e);
            }
            if (k < 1) return;
            if (PlayerBlocksRespawn())
            {
                // Someone is in the way: stay a ghost, without collision, until the space is free.
                LastRespawnWait += Time.deltaTime;
                foreach (var s in shards) if (s != null) s.color = new Color(1, 1, 1, 0.35f + 0.15f * Mathf.Sin(stateTimer * 12f));
                return;
            }
            for (int i = 0; i < shards.Length; i++)
                if (shards[i] != null) { shards[i].enabled = false; shards[i].transform.localPosition = shardHome[i]; shards[i].transform.localRotation = Quaternion.identity; }
            RespawnCount++;
            EnterIdle();
        }

        private void SetWhole(bool visible, float alpha)
        {
            if (slab == null) return;
            slab.enabled = visible; slab.color = new Color(1, 1, 1, alpha);
        }
    }
}
