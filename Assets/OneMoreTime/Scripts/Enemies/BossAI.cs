using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace OneMoreTime
{
    public enum BossAttack
    {
        None,
        // Phase 1
        MordidaDeMidgard, VentiscaRasante, OlaDeEscamas,
        // Phase 2
        ErupcionDeColmillos, RespiracionDeVeneno, ContraccionDosTiempos,
        // Phase 3
        EspinasCarmesi, AlientoDelRagnarok, FracturaDelMundo, JuicioDeLaSerpiente,
        // Transition specials
        QuebrantoGlacial, EclipseCarmesi
    }

    /// <summary>Combat state machine of the boss (the boss exhaustion is NOT the roar stun of the player).</summary>
    public enum BossCombatState { Intro, Attack, Recovery, Exhausted, Recovering, Transition, Defeated }

    public enum BossTransitionStep { None, Cancel, MorphBody, MorphNeck, MorphHead, Roar, Stun, Recover, Warning, Special }

    /// <summary>
    /// Jörmungandr. The arena floor is the serpent's back: the boss never walks, flies or chases. Only the head,
    /// jaw, visible neck and the scale plates in the floor move. A silent cinematic entrance (no AudioSource or
    /// AudioClip exists anywhere), then three truly different phases with no projectiles at all. Every attack has
    /// Anticipation → Active → Recovery, a ground warning, a cooldown and a counterplay. Crossing a health
    /// threshold cancels the current attack and runs: body/neck/head morph → silent roar → stun with an A/W/D
    /// alternation QTE → recovery → warning → one transition special → normal combat.
    /// </summary>
    public sealed class BossAI : EnemyActor, IAttackClock
    {
        [Header("Presencia y cámara")]
        [SerializeField, Min(3f)] private float combatCameraSize = 5.8f;
        [SerializeField, Min(3f)] private float introCameraSize = 6.4f;
        [Tooltip("Distancia desde el final de la arena hasta la base del cuello (oculta tras el montículo).")]
        [SerializeField, Min(1.5f)] private float neckBaseFromEnd = 3.0f;
        [Tooltip("Distancia de la base del cuello a la pared que el jugador no puede atravesar.")]
        [SerializeField, Min(1f)] private float wallFromNeckBase = 2.3f;

        [Header("Entrada cinematográfica (segundos)")]
        [SerializeField, Min(0.3f)] private float awakenDuration = 1.6f;
        [SerializeField, Min(0.3f)] private float revealDuration = 2.0f;
        [SerializeField, Min(0.5f)] private float roarDuration = 2.3f;
        [SerializeField, Min(0.2f)] private float settleDuration = 0.9f;

        [Header("Fases (fracción de vida)")]
        [SerializeField, Range(0.4f, 0.95f)] private float phase2Threshold = 0.70f;
        [SerializeField, Range(0.1f, 0.6f)] private float phase3Threshold = 0.35f;

        [Header("Transformación (segundos)")]
        [SerializeField, Min(0.2f)] private float bodyMorphDuration = 1.6f;
        [SerializeField, Min(0.2f)] private float neckMorphDuration = 1.0f;
        [SerializeField, Min(0.1f)] private float headMorphDuration = 0.6f;
        [SerializeField, Min(0.3f)] private float transitionRoarDuration = 1.5f;
        [SerializeField, Min(0.3f)] private float recoveryDuration = 0.8f;
        [SerializeField, Min(0.3f)] private float warningDuration = 1.0f;
        [Tooltip("Ventana de castigo: la cabeza queda baja y expuesta tras el ataque especial de transición.")]
        [SerializeField, Min(0.5f)] private float stunWindow = 2.2f;

        [Header("Aturdimiento (QTE)")]
        [SerializeField, Min(1)] private int qteCycles = 4;
        [SerializeField, Min(2f)] private float qteTimeout = 6f;
        [SerializeField] private KeyCode phase2KeyA = KeyCode.A, phase2KeyB = KeyCode.D;
        [SerializeField] private KeyCode phase3KeyA = KeyCode.W, phase3KeyB = KeyCode.D;

        [Header("Ritmo y enfriamientos (segundos)")]
        [SerializeField, Min(0.2f)] private float restPhase1 = 1.1f;
        [SerializeField, Min(0.2f)] private float restPhase2 = 0.85f;
        [SerializeField, Min(0.2f)] private float restPhase3 = 0.65f;
        [SerializeField, Min(0f)] private float attackCooldown = 5f;
        [SerializeField, Min(4f)] private float judgementCooldown = 14f;

        [Header("Ataques")]
        [SerializeField, Min(2f)] private float biteReach = 8.5f;
        [SerializeField, Min(0.5f)] private float windHeight = 1.8f;
        [SerializeField, Min(0.5f)] private float windDuration = 2.2f;
        [SerializeField, Min(2f)] private float crestSpeed = 7f;
        [SerializeField, Min(0.5f)] private float crestHeight = 1.25f;
        [SerializeField, Min(1f)] private float breathRange = 9f;
        [SerializeField, Min(0.5f)] private float breathDuration = 2.0f;
        [SerializeField, Min(0.5f)] private float cloudDuration = 4f;
        [SerializeField, Min(0.3f)] private float fangWarning = 0.8f;
        [SerializeField, Min(0.3f)] private float fangLifetime = 0.8f;

        [Header("Agotamiento tras N ataques")]
        [Tooltip("Ataques completados seguidos antes de quedar agotado.")]
        [SerializeField, Min(1)] private int attacksBeforeExhaustion = 5;
        [Tooltip("Duración exacta del agotamiento (segundos de juego).")]
        [SerializeField, Min(0.5f)] private float exhaustionDuration = 2.5f;

        [Header("Arte")]
        [SerializeField] private Sprite iceFangSprite;
        [SerializeField] private Sprite breathPuffSprite;

        private static readonly Color IceColor = new Color(0.72f, 0.92f, 1f, 1f);
        private static readonly Color VenomColor = new Color(0.55f, 1f, 0.45f, 0.75f);
        private static readonly Color CrimsonColor = new Color(1f, 0.28f, 0.22f, 1f);
        private static readonly Color WarnIce = new Color(0.55f, 0.85f, 1f, 0.85f);
        private static readonly Color WarnVenom = new Color(0.55f, 1f, 0.45f, 0.75f);
        private static readonly Color WarnRed = new Color(1f, 0.3f, 0.22f, 0.85f);
        private static readonly Color SafeColor = new Color(0.85f, 1f, 0.85f, 0.7f);

        private JormungandrBody body2;
        private BossRoarStunController stun;
        private Collider2D playerCollider;
        private BoxCollider2D baseCollider, headCollider;
        private Coroutine routine, fightRoutine;
        private readonly List<BossHazard> hazards = new List<BossHazard>();
        private readonly Dictionary<BossAttack, float> readyAt = new Dictionary<BossAttack, float>();
        private SpriteRenderer shade;
        private Color shadeColor = new Color(0.02f, 0.03f, 0.08f, 0);
        private bool transitioning;
        private int attackEpoch;
        private bool judging;
        private BossAttack lastAttack = BossAttack.None;
        private float neckBaseX, frontX, bodyTintRed;

        public string PatternName { get; private set; } = "Despertando";
        public int Stage { get; private set; } = 1;
        public bool Enraged => Stage >= 2 || (Health != null && Health.Current <= Health.Maximum * phase2Threshold);
        public bool IntroFinished { get; private set; }
        public bool Transitioning => transitioning;
        private BossTransitionStep step;
        public BossTransitionStep TransitionStep
        {
            get => step;
            private set { step = value; if (value != BossTransitionStep.None) TransitionTrace.Add(value); }
        }
        public BossAttack CurrentAttack { get; private set; }
        public BossRoarStunController Stun => stun;
        public JormungandrBody Body => body2;
        public float FrontX => frontX;
        public float Phase2Threshold => phase2Threshold;
        public float Phase3Threshold => phase3Threshold;
        public int TransitionsCompleted { get; private set; }
        public BossCombatState CombatState { get; private set; } = BossCombatState.Intro;
        /// <summary>Attacks completed since the last rest; only increases when an attack ends normally.</summary>
        public int AttacksSinceRest { get; private set; }
        public int ExhaustionCount { get; private set; }
        public float LastExhaustionDuration { get; private set; }
        public int AttacksBeforeExhaustion => attacksBeforeExhaustion;
        public float ExhaustionDuration => exhaustionDuration;
        public int SpecialsExecuted { get; private set; }
        public readonly List<string> AttackLog = new List<string>();
        /// <summary>QA hook: when false the fight loop waits (attacks can then be run one by one with <see cref="Run"/>).</summary>
        public bool AutoAttack { get; set; } = true;
        public readonly List<BossTransitionStep> TransitionTrace = new List<BossTransitionStep>();
        // IAttackClock
        public AttackStage AttackStageNow { get; private set; }
        AttackStage IAttackClock.Stage => AttackStageNow;
        public float StageProgress { get; private set; }
        public float AimAngle => 0;
        public int Variant => (int)CurrentAttack;

        private float Tempo => Stage == 1 ? 1f : Stage == 2 ? 1.15f : 1.3f;
        private CameraFollow2D Cam => game != null ? game.CameraRig : null;
        private const int F = -1; // the serpent always faces the arena (towards lower x)
        protected override bool FadesCorpse => false;
        protected override bool DestroysCorpse => false;
        private float Ground(float x) => game.GroundHeight(x);
        private float ArenaMin => game.PhaseStart + 0.8f;
        private float BaseGround => Ground(neckBaseX);

        public override void Initialize(GameManager manager, PlayerController target, ProjectilePool pool,
            int phase, LayerMask floorMask, float overrideHealth = 0)
        {
            base.Initialize(manager, target, pool, phase, floorMask, overrideHealth);
            neckBaseX = game.PhaseEnd - neckBaseFromEnd;
            frontX = neckBaseX - wallFromNeckBase;
            // Anchored colossus: kinematic, never displaced, never touching the player physically.
            body.bodyType = RigidbodyType2D.Kinematic; body.linearVelocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            transform.localScale = Vector3.one;
            transform.position = new Vector3(neckBaseX, Ground(neckBaseX), 0);
            var oldVisual = transform.Find("Visual"); if (oldVisual != null) oldVisual.gameObject.SetActive(false);
            var oldRig = GetComponent<ArticulatedRig>(); if (oldRig != null) oldRig.enabled = false;
            baseCollider = GetComponent<BoxCollider2D>();
            baseCollider.offset = new Vector2(-1.0f, 1.25f); baseCollider.size = new Vector2(2.6f, 2.5f);
            if (headCollider == null)
            {
                var hb = new GameObject("HeadHurtbox") { layer = gameObject.layer };
                hb.transform.SetParent(transform, false);
                headCollider = hb.AddComponent<BoxCollider2D>(); headCollider.size = new Vector2(2.4f, 1.7f);
            }
            headCollider.enabled = true; baseCollider.enabled = true;
            playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
            body2 = GetComponent<JormungandrBody>();
            if (body2 == null) body2 = gameObject.AddComponent<JormungandrBody>();
            var anyRenderer = player.GetComponentInChildren<SpriteRenderer>();
            body2.Build(Ground, neckBaseX, F, game.PhaseStart, frontX, anyRenderer != null ? anyRenderer.sharedMaterial : null);
            stun = GetComponent<BossRoarStunController>();
            if (stun == null) stun = gameObject.AddComponent<BossRoarStunController>();
            stun.Initialize(game, player); stun.RequiredCycles = qteCycles; stun.Timeout = qteTimeout;
            Health.Damaged -= OnDamaged; Health.Damaged += OnDamaged;
            ClearHazards(); readyAt.Clear(); AttackLog.Clear(); TransitionTrace.Clear(); AutoAttack = true;
            Stage = 1; IntroFinished = false; transitioning = false; lastAttack = BossAttack.None; TransitionStep = BossTransitionStep.None;
            TransitionsCompleted = 0; SpecialsExecuted = 0; bodyTintRed = 0;
            AttacksSinceRest = 0; ExhaustionCount = 0; LastExhaustionDuration = 0; CombatState = BossCombatState.Intro;
            SetStage(BossAttack.None, AttackStage.None, 0);
            Health.Invulnerable = true;
            player.SetExtraMaxX(frontX - 0.3f);
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Encounter());
        }

        // ------------------------------------------------------------------ Frame
        private void Update()
        {
            if (!IsAlive || game == null) return;
            if (player != null) player.SetExtraMaxX(frontX - 0.3f);
            if (body2 != null && body2.Built && !transitioning) ProgressiveDamageVisuals();
        }

        [Header("Cámara de combate")]
        [SerializeField, Min(3f)] private float maxCombatCameraSize = 7.4f;

        /// <summary>During the fight the camera frames both the hero and the serpent's head (never loses the boss).</summary>
        private void FrameFight()
        {
            if (Cam == null || player == null || !IntroFinished || !IsAlive) return;
            var cam = Cam.GetComponent<Camera>();
            float px = player.transform.position.x, hx = Mathf.Min(body2.HeadWorld.x - 1.2f, frontX + 1.5f);
            float span = Mathf.Abs(hx - px) + 6f;
            float size = Mathf.Clamp(span / (2f * Mathf.Max(1f, cam.aspect)), combatCameraSize, maxCombatCameraSize);
            float cx = (px + hx) * 0.5f;
            float cy = Mathf.Max(Ground(px), BaseGround) + size * 0.62f;
            Cam.SetFocusOverride(new Vector2(cx, cy), size, 0.35f);
        }

        private void LateUpdate()
        {
            FrameFight();
            if (headCollider != null && body2 != null && body2.Head != null)
            {
                Vector2 center = body2.Head.TransformPoint(new Vector2(1.5f, 0.1f));
                headCollider.offset = transform.InverseTransformPoint(center);
            }
            if (shade == null || Cam == null) return;
            var cam = Cam.GetComponent<Camera>();
            float unit = PixelFx.Pixel.bounds.size.x;
            shade.transform.localPosition = new Vector3(0, 0, 10);
            shade.transform.localScale = new Vector3((cam.orthographicSize * 2 * 2.4f + 4) / unit, (cam.orthographicSize * 2 + 4) / unit, 1);
            shade.color = shadeColor;
        }

        /// <summary>Within a phase, damage gradually rewrites the serpent's back (plates switch to the next drawing).</summary>
        private void ProgressiveDamageVisuals()
        {
            float hi = Stage == 1 ? 1 : Stage == 2 ? phase2Threshold : phase3Threshold;
            float lo = Stage == 1 ? phase2Threshold : Stage == 2 ? phase3Threshold : 0;
            float d = Mathf.InverseLerp(hi, lo, Health.Current / Health.Maximum);
            if (Stage < 3)
            {
                int n = body2.PlateCount;
                float done = d * n * 0.5f; // half of the back changes before the threshold, the rest during the morph
                for (int i = 0; i < n; i++) body2.SetPlateMorph(i, Stage + (i < Mathf.FloorToInt(done) ? 1 : 0));
            }
            else
            {
                bodyTintRed = Mathf.MoveTowards(bodyTintRed, d, Time.deltaTime);
                var c = Color.Lerp(Color.white, new Color(1f, 0.68f, 0.66f), bodyTintRed);
                body2.SetPlateTint(c);
                if (CombatState != BossCombatState.Exhausted) body2.SetBodyTint(Color.Lerp(Color.white, new Color(1f, 0.82f, 0.8f), bodyTintRed));
            }
        }

        // ------------------------------------------------------------------ Helpers
        private JormungandrBody.Pose P { get => body2.pose; set => body2.pose = value; }

        private void SetStage(BossAttack attack, AttackStage stage, float progress)
        {
            CurrentAttack = attack; AttackStageNow = stage; StageProgress = progress;
        }

        private IEnumerator Wait(float seconds, Action<float> onFrame = null)
        {
            float t = 0;
            while (t < seconds && IsAlive) { t += Time.deltaTime; onFrame?.Invoke(Mathf.Clamp01(t / Mathf.Max(0.0001f, seconds))); yield return null; }
        }

        /// <summary>Eases the body pose to <paramref name="target"/> while reporting the attack stage progress.</summary>
        private IEnumerator Blend(JormungandrBody.Pose target, float duration, Action<float> onFrame = null, bool linear = false)
        {
            var from = P; float t = 0; duration = Mathf.Max(0.01f, duration);
            while (t < duration && IsAlive)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                P = Lerp(from, target, linear ? k : Mathf.SmoothStep(0, 1, k));
                StageProgress = k;
                onFrame?.Invoke(k);
                yield return null;
            }
        }

        private static JormungandrBody.Pose Lerp(JormungandrBody.Pose a, JormungandrBody.Pose b, float t)
        {
            return new JormungandrBody.Pose
            {
                head = Vector2.Lerp(a.head, b.head, t), tilt = Mathf.Lerp(a.tilt, b.tilt, t), jaw = Mathf.Lerp(a.jaw, b.jaw, t),
                open = Mathf.Lerp(a.open, b.open, t), roar = Mathf.Lerp(a.roar, b.roar, t), snarl = Mathf.Lerp(a.snarl, b.snarl, t),
                tremble = Mathf.Lerp(a.tremble, b.tremble, t), glow = Mathf.Lerp(a.glow, b.glow, t), rise = Mathf.Lerp(a.rise, b.rise, t),
                contract = Mathf.Lerp(a.contract, b.contract, t), sway = Mathf.Lerp(a.sway, b.sway, t)
            };
        }

        private JormungandrBody.Pose Rest() { var r = JormungandrBody.Rest(Stage); r.rise = 1; return r; }

        /// <summary>Art-space head joint that puts the snout over world x at a given height above the floor.</summary>
        private Vector2 HeadFor(float worldX, float heightAboveGround)
        {
            float ax = (neckBaseX - worldX) - 1.7f;
            return new Vector2(Mathf.Clamp(ax, 0.8f, biteReach), Ground(worldX) - BaseGround + heightAboveGround);
        }

        private Vector2 Mouth => body2.MouthWorld;

        private bool HitPlayer(Vector2 center, Vector2 size, float amount, Vector2 knockback)
        {
            if (player == null || playerCollider == null || player.Health.IsDead || !game.IsPlaying) return false;
            var box = new Bounds(center, new Vector3(size.x, size.y, 1));
            return box.Intersects(playerCollider.bounds) && player.Health.TakeDamage(new DamageInfo(amount, Team.Enemy, knockback));
        }

        /// <summary>Cover (solid scenery props) or terrain between the source and the player blocks breath and wind.</summary>
        public bool PlayerShieldedFrom(Vector2 source)
        {
            if (player == null) return false;
            Vector2 target = player.AimPosition;
            target.y -= 0.3f; // lower torso: hugging a cover is enough
            var hits = Physics2D.LinecastAll(source, target, groundMask | PropLayers.PropMask);
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var cover = h.collider.GetComponentInParent<ArenaCover>();
                if (cover != null) { if (cover.BlocksBreath) return true; continue; }
                if (h.collider.GetComponent<PlatformEffector2D>() != null) continue; // one-way platforms do not shield
                return true; // terrain
            }
            return false;
        }

        private bool PlayerAbove(float height)
        {
            if (playerCollider == null) return false;
            float x = player.transform.position.x;
            return playerCollider.bounds.min.y > Ground(x) + height;
        }

        private BossHazard Warn(float centerX, float width, Color color, float duration, float thickness = 0.14f)
        {
            var h = BossHazard.Marker(null, new Vector2(centerX, Ground(centerX)), width, color, duration, 14f, thickness);
            hazards.Add(h); return h;
        }

        private BossHazard Fang(float x, float height, float life, float damage, Color tint)
        {
            var h = BossHazard.IceFang(null, iceFangSprite, new Vector2(x, Ground(x)), height, life, damage, player, game, tint,
                new Vector2(Mathf.Sign(player.transform.position.x - x + 0.001f) * 3f, 2.6f));
            hazards.Add(h);
            PixelFx.Burst(new Vector2(x, Ground(x) + 0.1f), 3, tint, 3f, 0.45f, 6f, 0.07f, 31, Vector2.zero, 90, 90);
            return h;
        }

        private void ClearHazards()
        {
            foreach (var h in hazards) if (h != null) Destroy(h.gameObject);
            hazards.Clear();
        }

        private void SetShade(float alpha, Color? tint = null)
        {
            if (Cam == null) return;
            if (shade == null) shade = PixelFx.CreateRenderer("BossArenaShade", Cam.transform, PixelFx.Pixel, -60, shadeColor);
            if (tint.HasValue) { var c = tint.Value; shadeColor = new Color(c.r, c.g, c.b, alpha); }
            else shadeColor.a = alpha;
            shade.color = shadeColor;
        }

        private IEnumerator ShadeTo(float alpha, float duration, Color? tint = null)
        {
            if (Cam == null) yield break;
            SetShade(shadeColor.a);
            Color from = shadeColor, to = tint.HasValue ? new Color(tint.Value.r, tint.Value.g, tint.Value.b, alpha) : new Color(shadeColor.r, shadeColor.g, shadeColor.b, alpha);
            float t = 0;
            while (t < duration) { t += Time.deltaTime; shadeColor = Color.Lerp(from, to, Mathf.Clamp01(t / duration)); if (shade != null) shade.color = shadeColor; yield return null; }
        }

        private void IceRain(int count, Color? tint = null)
        {
            if (Cam == null) return;
            var cam = Cam.GetComponent<Camera>();
            float half = cam.orthographicSize * cam.aspect, top = cam.transform.position.y + cam.orthographicSize;
            for (int i = 0; i < count; i++)
            {
                Vector2 at = new Vector2(cam.transform.position.x + Random.Range(-half, half), top + Random.Range(0f, 0.6f));
                PixelFx.Emit(at, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-4.5f, -2.5f)), Color.Lerp(tint ?? IceColor, Color.white, Random.value * 0.5f),
                    Random.Range(1.4f, 2.2f), 3f, Random.Range(0.05f, 0.11f), 30, null, Random.Range(-200f, 200f));
            }
        }

        private void Mist(Vector2 at, Vector2 velocity, int count, Color color)
        {
            for (int i = 0; i < count; i++)
                PixelFx.Emit(at + Random.insideUnitCircle * 0.4f, velocity * Random.Range(0.6f, 1.2f) + Random.insideUnitCircle * 0.5f, color,
                    Random.Range(0.7f, 1.2f), -0.3f, Random.Range(0.5f, 0.9f), 20,
                    breathPuffSprite != null ? breathPuffSprite : PixelFx.SoftDot, Random.Range(-40f, 40f), 1.2f, true);
        }

        private void OnDamaged(Health h)
        {
            if (body2 != null) body2.Flash();
            if (!IntroFinished || transitioning || !IsAlive) return;
            if (TargetStage() > Stage) BeginTransition();
        }

        private int TargetStage()
        {
            float f = Health.Current / Health.Maximum;
            return f <= phase3Threshold ? 3 : f <= phase2Threshold ? 2 : 1;
        }

        // ------------------------------------------------------------------ Encounter
        private IEnumerator Encounter()
        {
            yield return Intro();
            if (!IsAlive) yield break;
            fightRoutine = StartCoroutine(Fight());
        }

        /// <summary>Camera framing that keeps the whole serpent (head, neck and mound) inside the view.</summary>
        private Vector2 RevealFocus(float size)
        {
            var bounds = body2.VisualBounds;
            float cx = Mathf.Min(bounds.center.x, neckBaseX - 3.2f);
            float cy = Mathf.Max(BaseGround + size * 0.55f, bounds.center.y);
            return new Vector2(cx, cy);
        }

        private IEnumerator Intro()
        {
            CombatState = BossCombatState.Intro;
            game.SetPlayerControlLocked(true);
            float groundY = BaseGround;
            var p = P; p.rise = 0; p.glow = 0; p.head = new Vector2(2.4f, 3.6f); p.sway = 0; P = p;
            // Etapa 1 — El despertar: combat is held, the camera sweeps over the floor (its back), soft tremor,
            // snow and ice, scales of the terrain light up one after another. Nothing has risen yet.
            PatternName = "El despertar";
            int n = body2.PlateCount;
            Vector2 sweepFrom = new Vector2(Mathf.Max(game.PhaseStart + 9f, frontX - 12f), groundY + 2.6f);
            Vector2 sweepTo = new Vector2(neckBaseX - 4.5f, groundY + 3.0f);
            StartCoroutine(ShadeTo(0.32f, awakenDuration));
            yield return Wait(awakenDuration * 1.3f, k =>
            {
                Cam?.SetFocusOverride(Vector2.Lerp(sweepFrom, sweepTo, Mathf.SmoothStep(0, 1, k)), introCameraSize, 0.8f);
                Cam?.Shake(0.05f + 0.05f * k, 0.12f);
                if (Random.value < 0.6f) IceRain(2);
                for (int i = 0; i < n; i++)
                {
                    // Glow travels from the far plates towards the boss: the whole floor is one creature.
                    float wave = Mathf.Clamp01(1f - Mathf.Abs(k * (n + 1) - (n - i)) * 0.6f);
                    body2.SetPlateTint(i, Color.Lerp(Color.white, new Color(0.62f, 0.95f, 1f), wave));
                    body2.SetPlateLift(i, Mathf.Sin(Time.time * 28f + i) * 0.04f * k);
                    if (wave > 0.8f && Random.value < 0.15f) PixelFx.Emit(body2.PlateTop(i), new Vector2(0, 0.8f), new Color(0.7f, 0.95f, 1f, 0.9f), 0.6f, 0, 0.08f, 31);
                }
                if (Random.value < 0.3f)
                {
                    float x = neckBaseX + Random.Range(-3f, 1f);
                    PixelFx.Emit(new Vector2(x, Ground(x)), new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 1f)), new Color(1, 1, 1, 0.45f),
                        1.1f, -0.2f, Random.Range(0.4f, 0.8f), 20, PixelFx.SoftDot, 0, 0.8f, true);
                }
            });
            for (int i = 0; i < n; i++) { body2.SetPlateLift(i, 0); body2.SetPlateTint(i, Color.white); }
            // Etapa 2 — La aparición: the camera travels to the head while the neck rises slowly out of the mound,
            // with an organic sway; the eyes ignite, the jaw opens, the head settles into a threatening pose.
            PatternName = "Jörmungandr";
            var rising = Rest(); rising.rise = 1; rising.glow = 0.6f; rising.jaw = 0.15f; rising.head = new Vector2(2.2f, 5.3f); rising.tilt = 12; rising.sway = 1f;
            yield return Blend(rising, revealDuration * 1.3f, k =>
            {
                Cam?.SetFocusOverride(Vector2.Lerp(sweepTo, RevealFocus(introCameraSize), Mathf.SmoothStep(0, 1, k)), introCameraSize, 0.6f);
                if (Random.value < 0.4f) { float x = neckBaseX + Random.Range(-2f, 1f); PixelFx.Burst(new Vector2(x, Ground(x) + 0.1f), 2, Color.white, 2.5f, 0.6f, 5f, 0.07f, 31, Vector2.zero, 120, 90); }
            });
            var threat = P; threat.glow = 1; threat.jaw = 0.75f; threat.snarl = 1; threat.tilt = -8; threat.head = new Vector2(2.9f, 4.9f); threat.sway = 0.4f;
            yield return Blend(threat, revealDuration * 0.45f, k => Cam?.SetFocusOverride(RevealFocus(introCameraSize), introCameraSize, 0.5f));
            // Etapa 3 — Rugido de presentación (silencioso: no existe ningún AudioSource ni AudioClip).
            PatternName = "...";
            var lean = P; lean.snarl = 0; lean.tilt = 22; lean.jaw = 1; lean.head = new Vector2(2.1f, 5.5f);
            yield return Blend(lean, roarDuration * 0.3f, k => Cam?.SetFocusOverride(RevealFocus(introCameraSize), introCameraSize, 0.5f));
            var climax = P; climax.roar = 1; climax.tremble = 1; climax.glow = 1; climax.sway = 0.9f;
            yield return Blend(climax, roarDuration * 0.15f);
            Cam?.Shake(0.3f, roarDuration * 0.35f);
            IceRain(26);
            Mist(Mouth, new Vector2(F * 2.2f, 1.6f), 10, new Color(0.9f, 0.97f, 1f, 0.55f));
            PixelFx.Burst(Mouth, 20, IceColor, 6f, 0.9f, 4f, 0.09f, 31, Vector2.zero, 120, 150);
            StartCoroutine(ShadeTo(0.12f, 0.08f));
            yield return Wait(roarDuration * 0.35f, k =>
            {
                Cam?.SetFocusOverride(RevealFocus(introCameraSize), introCameraSize, 0.5f);
                if (Random.value < 0.35f) Mist(Mouth, new Vector2(F * 1.5f, 1.1f), 1, new Color(0.9f, 0.97f, 1f, 0.45f));
                if (Random.value < 0.5f) PixelFx.Emit(Mouth + Random.insideUnitCircle * 0.6f, new Vector2(F * Random.Range(1f, 3f), Random.Range(-1f, 2f)), IceColor, 0.6f, 4f, 0.07f, 31);
            });
            StartCoroutine(ShadeTo(0.3f, 0.3f));
            var close = P; close.roar = 0; close.tremble = 0; close.jaw = 0.15f; close.tilt = 4; close.sway = 0.2f;
            yield return Blend(close, roarDuration * 0.2f);
            // Etapa 4 — Comienzo de la batalla: combat camera, health bar, player control, first pattern.
            PatternName = "Preparando ataque";
            Cam?.ClearFocusOverride();
            Cam?.SetCombatSize(combatCameraSize);
            StartCoroutine(ShadeTo(0.16f, settleDuration));
            yield return Blend(Rest(), settleDuration);
            Health.Invulnerable = false;
            IntroFinished = true;
            CombatState = BossCombatState.Recovery;
            game.SetPlayerControlLocked(false);
        }

        private IEnumerator Fight()
        {
            CombatState = BossCombatState.Recovery;
            yield return Wait(0.3f);
            while (IsAlive)
            {
                while (!AutoAttack && IsAlive && TargetStage() <= Stage) yield return null;
                if (TargetStage() > Stage) { BeginTransition(); yield break; }
                var next = Choose();
                CombatState = BossCombatState.Attack;
                yield return Run(next);
                // Reached only when the attack ended normally (a transition stops this coroutine first).
                AttacksSinceRest++;
                lastAttack = next;
                readyAt[next] = Time.time + (next == BossAttack.JuicioDeLaSerpiente ? judgementCooldown : attackCooldown / Tempo);
                SetStage(BossAttack.None, AttackStage.None, 0);
                CombatState = BossCombatState.Recovery;
                PatternName = "Recuperación";
                yield return Blend(Rest(), 0.35f);
                if (AttacksSinceRest >= attacksBeforeExhaustion && IsAlive && TargetStage() <= Stage) { yield return Exhaustion(); continue; }
                yield return Wait(Stage == 1 ? restPhase1 : Stage == 2 ? restPhase2 : restPhase3);
            }
        }

        /// <summary>
        /// Boss exhaustion after N completed attacks: exactly <see cref="exhaustionDuration"/> seconds of game time with
        /// a tired animation (head down, jaw half open, dim eyes, laboured breathing, relaxed scales) and a clear
        /// vulnerability cue. No attack can start; the boss takes damage normally. Different from the player's roar stun.
        /// </summary>
        private IEnumerator Exhaustion()
        {
            CombatState = BossCombatState.Exhausted;
            ExhaustionCount++;
            ClearHazards(); // every attack window has ended: nothing keeps hurting while it rests
            PatternName = "¡Agotado! · ¡Golpéalo!";
            float start = Time.time;
            var from = P;
            Vector2 low = HeadFor(frontX - 2.4f, 1.55f);
            Cam?.Shake(0.08f, 0.2f);
            while (IsAlive && Time.time - start < exhaustionDuration)
            {
                float t = Time.time - start;
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.45f));
                float breath = Mathf.Sin(t * 2.6f);
                var q = Rest();
                q.head = Vector2.Lerp(from.head, low + new Vector2(0, 0.16f * breath), k);
                q.tilt = Mathf.Lerp(from.tilt, -16f + 3f * breath, k);
                q.jaw = Mathf.Lerp(from.jaw, 0.45f + 0.12f * breath, k);
                q.glow = Mathf.Lerp(from.glow, 0.12f, k);
                q.contract = 0.12f + 0.12f * (breath * 0.5f + 0.5f);
                q.sway = 0.35f;
                P = q;
                StageProgress = t / exhaustionDuration;
                // Relaxed scales and a pale pulsing aura: the opening is obvious.
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 9f);
                for (int i = 0; i < body2.PlateCount; i++) body2.SetPlateLift(i, -0.04f * k);
                body2.SetBodyTint(Color.Lerp(Color.white, new Color(0.78f, 0.9f, 1f), 0.5f * pulse * k));
                if (Random.value < 0.35f)
                {
                    Vector2 headCenter = body2.Head.TransformPoint(new Vector2(1.2f, 0.9f));
                    PixelFx.Emit(headCenter + Random.insideUnitCircle * 0.9f, new Vector2(0, Random.Range(0.4f, 0.9f)), new Color(0.85f, 0.95f, 1f, 0.9f), 0.7f, -0.2f, 0.09f, 150);
                }
                yield return null;
            }
            LastExhaustionDuration = Time.time - start;
            body2.SetBodyTint(Color.white);
            for (int i = 0; i < body2.PlateCount; i++) body2.SetPlateLift(i, 0);
            CombatState = BossCombatState.Recovering;
            PatternName = "Se recupera";
            AttacksSinceRest = 0;
            yield return Blend(Rest(), 0.6f);
            CombatState = BossCombatState.Recovery;
        }

        private bool Ready(BossAttack a) => !readyAt.TryGetValue(a, out float t) || Time.time >= t;

        /// <summary>Selection rules: phase set, reach, cooldown, never the same attack twice in a row.</summary>
        private BossAttack Choose()
        {
            bool inReach = player.transform.position.x > frontX - biteReach;
            var options = new List<BossAttack>();
            void Add(BossAttack a, int weight) { if (Ready(a) || weight < 0) for (int i = 0; i < Mathf.Abs(weight); i++) options.Add(a); }
            if (Stage == 1)
            {
                if (inReach) Add(BossAttack.MordidaDeMidgard, 2);
                Add(BossAttack.VentiscaRasante, 2); Add(BossAttack.OlaDeEscamas, 2);
            }
            else if (Stage == 2)
            {
                Add(BossAttack.ErupcionDeColmillos, 2); Add(BossAttack.RespiracionDeVeneno, 2); Add(BossAttack.ContraccionDosTiempos, 2);
            }
            else
            {
                Add(BossAttack.EspinasCarmesi, 2); Add(BossAttack.AlientoDelRagnarok, 2); Add(BossAttack.FracturaDelMundo, 2);
                if (Ready(BossAttack.JuicioDeLaSerpiente)) Add(BossAttack.JuicioDeLaSerpiente, 2);
            }
            if (options.Count > 1) options.RemoveAll(o => o == lastAttack);
            if (options.Count == 0)
            {
                // Everything on cooldown: the phase's always-available fallback.
                options.Add(Stage == 1 ? BossAttack.OlaDeEscamas : Stage == 2 ? BossAttack.ErupcionDeColmillos : BossAttack.EspinasCarmesi);
                if (options[0] == lastAttack) options[0] = Stage == 1 ? BossAttack.VentiscaRasante : Stage == 2 ? BossAttack.ContraccionDosTiempos : BossAttack.FracturaDelMundo;
            }
            return options[Random.Range(0, options.Count)];
        }

        /// <summary>Phase each attack belongs to (transition specials report the phase they open).</summary>
        public static int PhaseOf(BossAttack a)
        {
            switch (a)
            {
                case BossAttack.MordidaDeMidgard: case BossAttack.VentiscaRasante: case BossAttack.OlaDeEscamas: return 1;
                case BossAttack.ErupcionDeColmillos: case BossAttack.RespiracionDeVeneno: case BossAttack.ContraccionDosTiempos: case BossAttack.QuebrantoGlacial: return 2;
                case BossAttack.None: return 0;
                default: return 3;
            }
        }

        public int HazardCount { get { hazards.RemoveAll(h => h == null); return hazards.Count; } }

        public IEnumerator Run(BossAttack attack)
        {
            judging = attack == BossAttack.JuicioDeLaSerpiente;
            AttackLog.Add(attack.ToString());
            switch (attack)
            {
                case BossAttack.MordidaDeMidgard: yield return Mordida(false); break;
                case BossAttack.VentiscaRasante: yield return Ventisca(); break;
                case BossAttack.OlaDeEscamas: yield return OlaDeEscamas(); break;
                case BossAttack.ErupcionDeColmillos: yield return Erupcion(); break;
                case BossAttack.RespiracionDeVeneno: yield return Aliento(false, 1f); break;
                case BossAttack.ContraccionDosTiempos: yield return Contraccion(); break;
                case BossAttack.EspinasCarmesi: yield return Espinas(); break;
                case BossAttack.AlientoDelRagnarok: yield return Aliento(true, 1f); break;
                case BossAttack.FracturaDelMundo: yield return Fractura(); break;
                case BossAttack.JuicioDeLaSerpiente: yield return Juicio(); break;
                case BossAttack.QuebrantoGlacial: yield return Quebranto(); break;
                case BossAttack.EclipseCarmesi: yield return Eclipse(); break;
            }
        }

        // ------------------------------------------------------------------ Phase transitions
        private void BeginTransition()
        {
            if (transitioning || !IsAlive) return;
            transitioning = true; attackEpoch++; judging = false;
            // Transitions take priority over exhaustion: the counter restarts and no rest is pending.
            AttacksSinceRest = 0; CombatState = BossCombatState.Transition;
            body2?.SetBodyTint(Color.white);
            Health.Invulnerable = true; // immediately: the hit that crossed the threshold is the last one until the special
            ClearHazards();
            if (fightRoutine != null) { StopCoroutine(fightRoutine); fightRoutine = null; }
            if (routine != null) { StopCoroutine(routine); routine = null; }
            routine = StartCoroutine(Transitions());
        }

        private IEnumerator Transitions()
        {
            // A single hit may cross two thresholds: transitions are queued and run one after the other.
            while (IsAlive && TargetStage() > Stage) yield return Transition(Stage + 1);
            transitioning = false; TransitionStep = BossTransitionStep.None;
            AttacksSinceRest = 0;
            if (IsAlive) fightRoutine = StartCoroutine(Fight());
        }

        private IEnumerator Transition(int next)
        {
            // 1. Cancel: the current attack and every hazard disappear at once. The boss becomes invulnerable.
            TransitionStep = BossTransitionStep.Cancel;
            Health.Invulnerable = true;
            ClearHazards();
            SetStage(BossAttack.None, AttackStage.None, 0);
            for (int i = 0; i < body2.PlateCount; i++) body2.SetPlateLift(i, 0);
            int from = Stage;
            Stage = next;
            PatternName = next == 2 ? "La serpiente se agrieta" : "La sangre del Ragnarök";
            var hold = Rest(); hold.head = new Vector2(2.2f, 4.2f); hold.contract = 0.4f; hold.glow = 1; hold.tremble = 0.4f;
            yield return Blend(hold, 0.35f);
            // 2. Body regions morph in order (from the boss outwards), then the neck from the base, then the head LAST.
            TransitionStep = BossTransitionStep.MorphBody;
            int n = body2.PlateCount;
            float per = bodyMorphDuration / Mathf.Max(1, n);
            for (int i = 0; i < n && IsAlive; i++)
            {
                if (body2.PlateMorph(i) < next)
                {
                    body2.SetPlateMorph(i, next);
                    float x = body2.PlateWorldX(i);
                    PixelFx.Burst(new Vector2(x, Ground(x) + 0.2f), 6, next == 3 ? CrimsonColor : IceColor, 3.5f, 0.6f, 6f, 0.08f, 31, Vector2.zero, 120, 90);
                    Cam?.Shake(0.06f, 0.1f);
                }
                yield return Wait(per);
            }
            TransitionStep = BossTransitionStep.MorphNeck;
            yield return Wait(neckMorphDuration, k => body2.SetNeckMorphProgress(from, k));
            body2.SetNeckMorph(next);
            TransitionStep = BossTransitionStep.MorphHead;
            yield return Wait(headMorphDuration, k => body2.SetHeadMorph(from + Mathf.SmoothStep(0, 1, k)));
            body2.SetHeadMorph(next);
            body2.Flash();
            // 3. Silent roar (no AudioSource, no AudioClip).
            TransitionStep = BossTransitionStep.Roar;
            var roar = P; roar.roar = 1; roar.tilt = 20; roar.head = new Vector2(2.0f, 5.4f); roar.tremble = 1; roar.contract = 0.2f;
            yield return Blend(roar, 0.35f);
            Cam?.Shake(next == 2 ? 0.3f : 0.4f, transitionRoarDuration * 0.7f);
            IceRain(next == 2 ? 20 : 30, next == 3 ? CrimsonColor : (Color?)null);
            Mist(Mouth, new Vector2(F * 2f, 1.3f), 8, next == 3 ? new Color(1f, 0.6f, 0.55f, 0.5f) : new Color(0.9f, 0.97f, 1f, 0.5f));
            if (next == 3) StartCoroutine(ShadeTo(0.22f, 0.6f, new Color(0.12f, 0.01f, 0.02f)));
            yield return Wait(transitionRoarDuration * 0.7f);
            var settle = P; settle.roar = 0; settle.tremble = 0; settle.jaw = 0.2f;
            yield return Blend(settle, 0.3f);
            // 4. The roar stuns the player: two keys from {A, W, D}, alternated for N full cycles. No boss damage meanwhile.
            TransitionStep = BossTransitionStep.Stun;
            bool finished = false;
            if (player != null && !player.Health.IsDead)
            {
                stun.Begin(next == 2 ? phase2KeyA : phase3KeyA, next == 2 ? phase2KeyB : phase3KeyB, ok => finished = true);
                while (IsAlive && !finished && stun.Active) yield return null;
            }
            // 5. Recovery, then a clear warning before the special.
            TransitionStep = BossTransitionStep.Recover;
            PatternName = "Recuperación";
            yield return Blend(Rest(), recoveryDuration);
            TransitionStep = BossTransitionStep.Warning;
            PatternName = next == 2 ? "¡Prepárate! · Quebranto glacial" : "¡Prepárate! · Eclipse carmesí";
            var warn = P; warn.glow = 1; warn.snarl = 1; warn.tilt = 6;
            yield return Blend(warn, warningDuration * 0.4f);
            yield return Wait(warningDuration * 0.6f, k => { if (Random.value < 0.5f) PixelFx.Emit(body2.EyeWorld, Random.insideUnitCircle * 1.5f, next == 3 ? CrimsonColor : IceColor, 0.4f, 0, 0.07f, 150); });
            // 6. The special runs once, then the head stays low (punish window) and combat resumes.
            TransitionStep = BossTransitionStep.Special;
            yield return Run(next == 2 ? BossAttack.QuebrantoGlacial : BossAttack.EclipseCarmesi);
            SpecialsExecuted++;
            SetStage(BossAttack.None, AttackStage.None, 0);
            Health.Invulnerable = false;
            PatternName = "¡Expuesto! · Golpea la cabeza";
            var low = P; low.head = HeadFor(frontX - 2.5f, 1.3f); low.tilt = -10; low.snarl = 0; low.jaw = 0.4f; low.glow = 0.4f;
            yield return Blend(low, 0.4f);
            yield return Wait(stunWindow);
            yield return Blend(Rest(), 0.5f);
            TransitionsCompleted++;
        }

        // ------------------------------------------------------------------ A. Mordida de Midgard
        private IEnumerator Mordida(bool fast)
        {
            var attack = fast ? BossAttack.JuicioDeLaSerpiente : BossAttack.MordidaDeMidgard;
            if (!fast) PatternName = "Mordida de Midgard";
            float landing = Mathf.Clamp(player.transform.position.x, frontX - biteReach, frontX - 0.6f);
            float antic = (fast ? 0.5f : 0.8f) / Tempo;
            // Anticipation: the head pulls back, the jaw parts slightly, the snarl shows; the landing zone is marked.
            SetStage(attack, AttackStage.Anticipation, 0);
            Warn(landing, 2.8f, WarnRed, antic + 0.25f);
            var pull = Rest(); pull.head = new Vector2(1.6f, 5.6f); pull.tilt = 16; pull.jaw = 0.6f; pull.snarl = 1; pull.glow = 1;
            yield return Blend(pull, antic);
            // Active: the head lunges, the jaw snaps shut on arrival. The hitbox follows the real snout.
            SetStage(attack, AttackStage.Active, 0);
            var strike = Rest(); strike.head = HeadFor(landing, 1.15f); strike.tilt = -14; strike.jaw = 1; strike.snarl = 0;
            bool shook = false, hit = false;
            yield return Blend(strike, 0.2f, k =>
            {
                if (k > 0.65f) { var q = P; q.jaw = 0; P = q; }
                if (k >= 0.5f && !hit) hit = HitPlayer(Mouth + new Vector2(0.5f, 0.1f), new Vector2(2.0f, 1.6f), Damage * 1.6f, new Vector2(-4.5f, 2.6f));
                if (k > 0.9f && !shook) { shook = true; Cam?.Shake(0.14f, 0.25f); PixelFx.Burst(new Vector2(landing, Ground(landing) + 0.1f), 10, IceColor, 3.5f, 0.6f, 6, 0.08f, 31, Vector2.zero, 140, 90); }
            }, true);
            yield return Wait(0.08f, k => { if (!hit) hit = HitPlayer(Mouth + new Vector2(0.5f, 0.1f), new Vector2(2.0f, 1.6f), Damage * 1.6f, new Vector2(-4.5f, 2.6f)); });
            // Recovery: the head rests on the floor — the counterplay window to strike it.
            SetStage(attack, AttackStage.Recovery, 0);
            yield return Wait((fast ? 0.45f : 0.95f) / Tempo, k => StageProgress = k * 0.6f);
            yield return Blend(Rest(), 0.55f / Tempo);
        }

        // ------------------------------------------------------------------ B. Ventisca rasante
        private IEnumerator Ventisca()
        {
            PatternName = "Ventisca rasante · cúbrete o sube";
            SetStage(BossAttack.VentiscaRasante, AttackStage.Anticipation, 0);
            float min = ArenaMin, max = frontX;
            // The low band of the whole arena is marked (thick, pale): high platforms and stone cover are safe.
            Warn((min + max) * 0.5f, max - min, new Color(0.7f, 0.9f, 1f, 0.35f), 1.0f / Tempo + windDuration, 0.3f);
            var inhale = Rest(); inhale.head = new Vector2(2.0f, 6.0f); inhale.tilt = 18; inhale.jaw = 0.5f; inhale.contract = 0.5f;
            yield return Blend(inhale, 1.0f / Tempo, k =>
            {
                if (Random.value < 0.6f) { Vector2 m = Mouth, from = m + Random.insideUnitCircle.normalized * 1.2f; PixelFx.Emit(from, (m - from) * 2.5f, IceColor, 0.35f, 0, 0.08f, 31); }
            });
            SetStage(BossAttack.VentiscaRasante, AttackStage.Active, 0);
            var blow = Rest(); blow.head = HeadFor(frontX - 1.5f, 1.6f); blow.tilt = -6; blow.open = 1; blow.tremble = 0.3f;
            StartCoroutine(Guard(Blend(blow, 0.2f)));
            // A pale frost band over the low part of the arena makes the danger height readable.
            var band = BossHazard.Visual(null, PixelFx.Pixel, 34, new Color(0.8f, 0.92f, 1f, 0), windDuration + 0.2f);
            hazards.Add(band);
            float unit = PixelFx.Pixel.bounds.size.x, midX = (min + max) * 0.5f;
            band.transform.position = new Vector3(midX, Ground(midX) + windHeight * 0.5f, 0);
            band.transform.localScale = new Vector3((max - min) / unit, windHeight / unit, 1);
            float t = 0, tick = 0;
            while (t < windDuration && IsAlive)
            {
                t += Time.deltaTime; tick -= Time.deltaTime; StageProgress = t / windDuration;
                if (band != null && band.Renderer != null) band.Renderer.color = new Color(0.8f, 0.92f, 1f, 0.16f + 0.06f * Mathf.Sin(t * 14f));
                for (int i = 0; i < 7; i++)
                {
                    float y = Random.Range(0.1f, windHeight);
                    float x = Random.Range(min, max);
                    PixelFx.Emit(new Vector2(x, Ground(x) + y), new Vector2(F * Random.Range(10f, 15f), Random.Range(-0.3f, 0.3f)), Color.Lerp(IceColor, Color.white, Random.value),
                        Random.Range(0.25f, 0.5f), 0, Random.Range(0.08f, 0.16f), 36);
                }
                if (Random.value < 0.6f)
                {
                    float x = Random.Range(min, max);
                    PixelFx.Emit(new Vector2(x, Ground(x) + Random.Range(0.2f, windHeight)), new Vector2(F * Random.Range(7f, 10f), 0), new Color(1, 1, 1, 0.35f),
                        0.6f, 0, Random.Range(0.35f, 0.6f), 35, PixelFx.SoftDot, 0, 0.5f, true);
                }
                if (tick <= 0)
                {
                    tick = 0.45f;
                    float px = player.transform.position.x;
                    bool shielded = PlayerShieldedFrom(new Vector2(frontX, Mathf.Max(player.AimPosition.y, Ground(px) + 0.6f)));
                    if (px < frontX + 0.2f && !PlayerAbove(windHeight) && !shielded)
                        player.Health.TakeDamage(new DamageInfo(Damage * 0.45f, Team.Enemy, new Vector2(F * 3.2f, 0.4f)));
                }
                yield return null;
            }
            SetStage(BossAttack.VentiscaRasante, AttackStage.Recovery, 0);
            yield return Blend(Rest(), 0.8f / Tempo);
        }

        // ------------------------------------------------------------------ C. Ola de escamas
        private IEnumerator OlaDeEscamas()
        {
            PatternName = "Ola de escamas · salta la cresta";
            SetStage(BossAttack.OlaDeEscamas, AttackStage.Anticipation, 0);
            float antic = 0.9f / Tempo;
            Warn((ArenaMin + frontX) * 0.5f, frontX - ArenaMin, new Color(0.6f, 0.85f, 1f, 0.3f), antic + 0.4f);
            var coil = Rest(); coil.contract = 1; coil.head = new Vector2(2.3f, 4.4f); coil.tilt = -8; coil.snarl = 1;
            yield return Blend(coil, antic, k =>
            {
                // Plates closest to the boss tremble: the wave will start there.
                for (int i = 0; i < 2; i++) body2.SetPlateLift(i, Mathf.Abs(Mathf.Sin(Time.time * 25f + i)) * 0.25f * k);
                if (Random.value < 0.2f) Cam?.Shake(0.04f, 0.1f);
            });
            SetStage(BossAttack.OlaDeEscamas, AttackStage.Active, 0);
            // The serpent's own back rises out of the floor and rolls towards the hero: jump over the crest.
            var crest = BossHazard.Crest(null, body2.WaveSprite, Ground, frontX, ArenaMin - 1f, crestSpeed * Tempo, crestHeight, Damage * 1.1f, player, game, body2.WaveTint(Stage));
            hazards.Add(crest);
            var release = P; release.contract = 0; release.snarl = 0; release.tilt = 4;
            StartCoroutine(Guard(Blend(release, 0.4f)));
            Cam?.Shake(0.15f, 0.3f);
            while (crest != null && !crest.Finished && IsAlive)
            {
                StageProgress = Mathf.InverseLerp(frontX, ArenaMin, crest.X);
                for (int i = 0; i < body2.PlateCount; i++)
                {
                    float d = Mathf.Abs(body2.PlateWorldX(i) - crest.X);
                    body2.SetPlateLift(i, Mathf.Clamp01(1 - d / 1.6f) * 0.9f);
                }
                yield return null;
            }
            for (int i = 0; i < body2.PlateCount; i++) body2.SetPlateLift(i, 0);
            SetStage(BossAttack.OlaDeEscamas, AttackStage.Recovery, 0);
            yield return Blend(Rest(), 0.6f / Tempo);
        }

        private Sprite FinSprite() => body2.FinFor(Stage);

        /// <summary>Side coroutines of an attack stop as soon as a phase transition cancels that attack.</summary>
        private IEnumerator Guard(IEnumerator inner)
        {
            int epoch = attackEpoch;
            while (epoch == attackEpoch && IsAlive && inner.MoveNext()) yield return inner.Current;
        }

        // ------------------------------------------------------------------ D. Erupción de colmillos helados
        private IEnumerator Erupcion()
        {
            PatternName = "Erupción de colmillos helados";
            SetStage(BossAttack.ErupcionDeColmillos, AttackStage.Anticipation, 0);
            var call = Rest(); call.head = new Vector2(2.2f, 5.6f); call.tilt = 14; call.jaw = 0.7f; call.glow = 1;
            yield return Blend(call, 0.5f / Tempo);
            float min = ArenaMin, max = frontX; const int sectors = 4;
            float w = (max - min) / sectors;
            // Sectors erupt one after another (near the boss first); the one that just erupted becomes the safe ground.
            SetStage(BossAttack.ErupcionDeColmillos, AttackStage.Active, 0);
            int[] order = Random.value < 0.5f ? new[] { 3, 1, 2, 0 } : new[] { 3, 2, 1, 0 };
            for (int s = 0; s < sectors && IsAlive; s++)
            {
                int k = order[s];
                float a = min + k * w, b = a + w;
                Warn((a + b) * 0.5f, w - 0.2f, WarnIce, fangWarning / Tempo);
                StageProgress = (float)s / sectors;
                yield return Wait(fangWarning / Tempo * 0.55f);
                StartCoroutine(Guard(EruptSector(a, b, fangWarning / Tempo * 0.45f)));
            }
            yield return Wait(fangWarning / Tempo + fangLifetime);
            SetStage(BossAttack.ErupcionDeColmillos, AttackStage.Recovery, 0);
            yield return Blend(Rest(), 0.6f / Tempo);
        }

        private IEnumerator EruptSector(float a, float b, float delay)
        {
            yield return Wait(delay);
            if (!IsAlive) yield break;
            Cam?.Shake(0.1f, 0.15f);
            for (float x = a + 0.55f; x < b - 0.3f; x += 1.1f) Fang(x + Random.Range(-0.15f, 0.15f), 1.5f, fangLifetime, Damage, Color.white);
        }

        // ------------------------------------------------------------------ E / H. Breath (venom / Ragnarök)
        private IEnumerator Aliento(bool ragnarok, float durationScale)
        {
            var attack = ragnarok ? BossAttack.AlientoDelRagnarok : BossAttack.RespiracionDeVeneno;
            if (!judging)
                PatternName = ragnarok ? "Aliento del Ragnarök · ponte a cubierto" : "Respiración de veneno ancestral · cúbrete o sube";
            else attack = BossAttack.JuicioDeLaSerpiente;
            SetStage(attack, AttackStage.Anticipation, 0);
            Color tint = ragnarok ? new Color(1f, 0.42f, 0.36f, 0.95f) : new Color(0.6f, 1f, 0.5f, 0.9f);
            float targetX = Mathf.Clamp(player.transform.position.x, Mathf.Max(ArenaMin, frontX - breathRange), frontX - 1f);
            float zoneW = ragnarok ? 6.5f : 5f;
            Warn(targetX, zoneW, ragnarok ? WarnRed : WarnVenom, 0.9f / Tempo + breathDuration * durationScale, 0.2f);
            var rear = Rest(); rear.head = new Vector2(1.8f, 6.0f); rear.tilt = 18; rear.jaw = 0.8f; rear.glow = 1;
            yield return Blend(rear, 0.9f / Tempo, k =>
            {
                if (Random.value < 0.5f) { Vector2 m = Mouth, from = m + Random.insideUnitCircle.normalized * 1f; PixelFx.Emit(from, (m - from) * 2.5f, tint, 0.35f, 0, 0.09f, 31); }
            });
            SetStage(attack, AttackStage.Active, 0);
            var breathe = Rest(); breathe.head = new Vector2(2.6f, 5.2f); breathe.tilt = -16; breathe.open = 1; breathe.tremble = 0.25f;
            StartCoroutine(Guard(Blend(breathe, 0.2f)));
            var cone = BossHazard.Visual(null, body2.ConeFor(Stage), 33, tint, breathDuration * durationScale + 0.3f);
            hazards.Add(cone);
            float t = 0, tick = 0, puff = 0, dur = breathDuration * durationScale;
            while (t < dur && IsAlive)
            {
                t += Time.deltaTime; tick -= Time.deltaTime; puff -= Time.deltaTime; StageProgress = t / dur;
                Vector2 m = Mouth, end = new Vector2(targetX, Ground(targetX) + 0.4f);
                Vector2 dir = end - m; float len = dir.magnitude;
                if (cone != null && cone.Renderer != null && cone.Renderer.sprite != null)
                {
                    var s = cone.Renderer.sprite.bounds.size;
                    cone.transform.position = m;
                    cone.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    float grow = Mathf.Clamp01(t / 0.25f);
                    cone.transform.localScale = new Vector3(len / s.x * grow, 1.6f / s.y * (0.85f + 0.15f * Mathf.Sin(t * 20f)), 1);
                }
                if (puff <= 0)
                {
                    puff = 0.05f;
                    PixelFx.Emit(m, (dir.normalized + Random.insideUnitCircle * 0.12f).normalized * 9f, tint, len / 9f, 0, 0.9f, 32,
                        breathPuffSprite != null ? breathPuffSprite : PixelFx.SoftDot, Random.Range(-90f, 90f), 0, true);
                }
                if (tick <= 0)
                {
                    tick = 0.35f;
                    bool inZone = Mathf.Abs(player.transform.position.x - targetX) < zoneW * 0.5f + 0.3f;
                    bool high = !ragnarok && PlayerAbove(2.0f);
                    if (inZone && !high && !PlayerShieldedFrom(m))
                        player.Health.TakeDamage(new DamageInfo(Damage * (ragnarok ? 0.6f : 0.45f), Team.Enemy, new Vector2(F * 3f, 1f)));
                }
                yield return null;
            }
            if (!ragnarok && IsAlive)
            {
                // The venom lingers on the floor: leave the zone or stay above it.
                hazards.Add(BossHazard.Cloud(null, breathPuffSprite, new Vector2(targetX, Ground(targetX)), zoneW, 1.7f, cloudDuration, Damage * 0.3f, 0.5f, player, game, VenomColor));
            }
            SetStage(attack, AttackStage.Recovery, 0);
            yield return Blend(Rest(), 0.6f / Tempo);
        }

        // ------------------------------------------------------------------ F. Contracción de dos tiempos
        private IEnumerator Contraccion()
        {
            PatternName = "Contracción de dos tiempos";
            SetStage(BossAttack.ContraccionDosTiempos, AttackStage.Anticipation, 0);
            float min = ArenaMin, max = frontX; const float strip = 3f;
            int count = Mathf.Max(2, Mathf.FloorToInt((max - min) / strip));
            float w = (max - min) / count;
            var tense = Rest(); tense.contract = 1; tense.head = new Vector2(2.2f, 4.5f); tense.snarl = 1;
            yield return Blend(tense, 0.4f / Tempo);
            for (int pulse = 0; pulse < 2 && IsAlive; pulse++)
            {
                SetStage(BossAttack.ContraccionDosTiempos, AttackStage.Anticipation, pulse * 0.5f);
                for (int i = pulse; i < count; i += 2) Warn(min + (i + 0.5f) * w, w - 0.25f, WarnIce, 0.9f / Tempo);
                yield return Wait(0.9f / Tempo, k =>
                {
                    for (int i = 0; i < body2.PlateCount; i++)
                    {
                        int s = Mathf.Clamp(Mathf.FloorToInt((body2.PlateWorldX(i) - min) / w), 0, count - 1);
                        body2.SetPlateLift(i, s % 2 == pulse ? 0.15f * k : 0);
                    }
                });
                SetStage(BossAttack.ContraccionDosTiempos, AttackStage.Active, pulse * 0.5f);
                Cam?.Shake(0.16f, 0.2f);
                var squeeze = P; squeeze.contract = pulse == 0 ? 0.3f : 1f; P = squeeze;
                for (int i = pulse; i < count; i += 2)
                {
                    float a = min + i * w;
                    for (float x = a + 0.5f; x < a + w - 0.3f; x += 1f) Fang(x, 1.3f, 0.7f, Damage, Color.white);
                }
                for (int i = 0; i < body2.PlateCount; i++)
                {
                    int s = Mathf.Clamp(Mathf.FloorToInt((body2.PlateWorldX(i) - min) / w), 0, count - 1);
                    body2.SetPlateLift(i, s % 2 == pulse ? 0.7f : 0);
                }
                yield return Wait(0.75f);
                for (int i = 0; i < body2.PlateCount; i++) body2.SetPlateLift(i, 0);
            }
            SetStage(BossAttack.ContraccionDosTiempos, AttackStage.Recovery, 0);
            yield return Blend(Rest(), 0.6f / Tempo);
        }

        // ------------------------------------------------------------------ G. Espinas carmesí en cadena
        private IEnumerator Espinas()
        {
            var attack = judging ? BossAttack.JuicioDeLaSerpiente : BossAttack.EspinasCarmesi;
            if (attack == BossAttack.EspinasCarmesi) PatternName = "Espinas carmesí en cadena";
            SetStage(attack, AttackStage.Anticipation, 0);
            var call = Rest(); call.head = new Vector2(2.4f, 5.0f); call.tilt = -10; call.snarl = 1; call.glow = 1;
            yield return Blend(call, 0.55f / Tempo);
            SetStage(attack, AttackStage.Active, 0);
            float end = Mathf.Max(ArenaMin, player.transform.position.x - 2.2f);
            float x = frontX - 0.6f; int i = 0;
            // A chain of spikes running at the player: each column is announced 0.35 s before it erupts.
            while (x > end && IsAlive)
            {
                float at = x;
                Warn(at, 0.9f, WarnRed, 0.35f);
                StartCoroutine(Guard(DelayedFang(at, 0.35f, 1.7f, 0.55f, CrimsonColor)));
                x -= 1.1f; i++;
                StageProgress = Mathf.InverseLerp(frontX, end, x);
                yield return Wait(0.11f / Tempo);
            }
            yield return Wait(0.6f);
            SetStage(attack, AttackStage.Recovery, 0);
            yield return Blend(Rest(), 0.5f / Tempo);
        }

        private IEnumerator DelayedFang(float x, float delay, float height, float life, Color tint)
        {
            yield return Wait(delay);
            if (IsAlive) Fang(x, height, life, Damage, tint);
        }

        // ------------------------------------------------------------------ I. Fractura del mundo
        private IEnumerator Fractura()
        {
            PatternName = "Fractura del mundo · salta las ondas";
            SetStage(BossAttack.FracturaDelMundo, AttackStage.Anticipation, 0);
            var slam = Rest(); slam.head = new Vector2(2.0f, 6.0f); slam.tilt = 24; slam.jaw = 0.4f; slam.contract = 0.6f;
            var spots = new List<float>();
            float min = ArenaMin + 1.5f, max = frontX - 1.2f;
            for (int i = 0; i < 3; i++) spots.Add(Mathf.Lerp(min, max, (i + 0.5f) / 3f) + Random.Range(-0.8f, 0.8f));
            foreach (float s in spots) Warn(s, 0.6f, new Color(1f, 0.45f, 0.2f, 0.95f), 1.1f / Tempo, 0.22f);
            yield return Blend(slam, 1.1f / Tempo, k =>
            {
                if (Random.value < 0.5f) { float s = spots[Random.Range(0, spots.Count)]; PixelFx.Emit(new Vector2(s + Random.Range(-0.3f, 0.3f), Ground(s) + 0.05f), new Vector2(0, Random.Range(0.6f, 1.4f)), new Color(1f, 0.5f, 0.2f), 0.4f, 0, 0.07f, 31); }
            });
            SetStage(BossAttack.FracturaDelMundo, AttackStage.Active, 0);
            var down = P; down.head = HeadFor(frontX - 1.2f, 1.6f); down.tilt = -18; down.contract = 0;
            yield return Blend(down, 0.16f, null, true);
            Cam?.Shake(0.3f, 0.4f);
            var waves = new List<BossHazard>();
            foreach (float s in spots)
            {
                Fang(s, 1.4f, 0.5f, Damage, CrimsonColor);
                foreach (int dir in new[] { -1, 1 })
                {
                    var w = BossHazard.Crest(null, body2.WaveSprite, Ground, s, Mathf.Clamp(s + dir * 4.5f, ArenaMin - 0.5f, frontX), 6f * Tempo, 0.95f, Damage * 0.9f, player, game, new Color(1f, 0.5f, 0.45f));
                    hazards.Add(w); waves.Add(w);
                }
            }
            float tt = 0;
            while (IsAlive && waves.Exists(w => w != null && !w.Finished)) { tt += Time.deltaTime; StageProgress = Mathf.Clamp01(tt / 0.9f); yield return null; }
            SetStage(BossAttack.FracturaDelMundo, AttackStage.Recovery, 0);
            yield return Wait(0.4f / Tempo);
            yield return Blend(Rest(), 0.6f / Tempo);
        }

        // ------------------------------------------------------------------ J. Juicio de la serpiente
        private IEnumerator Juicio()
        {
            PatternName = "Juicio de la serpiente";
            judging = true;
            // Three moments: bite → crimson spike chain → short Ragnarök breath. Special cooldown.
            yield return Mordida(true);
            if (!IsAlive) yield break;
            yield return Espinas();
            if (!IsAlive) yield break;
            yield return Aliento(true, 0.55f);
            judging = false;
        }

        // ------------------------------------------------------------------ Transition special: Quebranto glacial
        private IEnumerator Quebranto()
        {
            PatternName = "Quebranto glacial · ve a la zona segura";
            SetStage(BossAttack.QuebrantoGlacial, AttackStage.Anticipation, 0);
            float sector = Mathf.Min(6f, (frontX - ArenaMin) / 4f);
            float safeEnd = frontX - 3 * sector;
            // Clear safe zone (bright, steady) on the far side of the arena.
            var safe = BossHazard.Marker(null, new Vector2((ArenaMin + safeEnd) * 0.5f, Ground((ArenaMin + safeEnd) * 0.5f)), safeEnd - ArenaMin, SafeColor, 6.5f, 3f, 0.24f);
            hazards.Add(safe);
            var call = Rest(); call.head = new Vector2(1.8f, 6.0f); call.tilt = 22; call.roar = 1; call.tremble = 0.6f;
            yield return Blend(call, 0.6f);
            SetStage(BossAttack.QuebrantoGlacial, AttackStage.Active, 0);
            for (int s = 0; s < 3 && IsAlive; s++)
            {
                float b = frontX - s * sector, a = b - sector;
                Warn((a + b) * 0.5f, sector - 0.2f, WarnIce, 1.0f, 0.3f);
                yield return Wait(1.0f, k => { if (Random.value < 0.4f) { float x = Random.Range(a, b); PixelFx.Emit(new Vector2(x, Ground(x) + 0.05f), new Vector2(0, 1f), IceColor, 0.3f, 0, 0.07f, 31); } });
                Cam?.Shake(0.22f, 0.3f);
                for (float x = a + 0.45f; x < b - 0.2f; x += 0.9f) Fang(x, 2.0f, 1.1f, Damage * 1.2f, Color.white);
                StageProgress = (s + 1) / 3f;
                yield return Wait(0.5f);
            }
            yield return Wait(0.6f);
            SetStage(BossAttack.QuebrantoGlacial, AttackStage.Recovery, 0);
        }

        // ------------------------------------------------------------------ Transition special: Eclipse carmesí
        private IEnumerator Eclipse()
        {
            PatternName = "Eclipse carmesí";
            SetStage(BossAttack.EclipseCarmesi, AttackStage.Anticipation, 0);
            StartCoroutine(ShadeTo(0.38f, 0.8f, new Color(0.25f, 0.01f, 0.02f)));
            var contract = Rest(); contract.contract = 1; contract.head = new Vector2(2.0f, 4.3f); contract.glow = 1; contract.tremble = 0.5f;
            // Red wave across the scales of the back.
            int n = body2.PlateCount;
            yield return Blend(contract, 0.9f, k =>
            {
                for (int i = 0; i < n; i++)
                {
                    float wave = Mathf.Clamp01(k * (n + 2) - i);
                    body2.SetPlateTint(i, Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), wave));
                    body2.SetPlateLift(i, Mathf.Sin(wave * Mathf.PI) * 0.4f);
                }
            });
            SetStage(BossAttack.EclipseCarmesi, AttackStage.Active, 0);
            float min = ArenaMin, max = frontX; float w = (max - min) / 6f;
            for (int pass = 0; pass < 2 && IsAlive; pass++)
            {
                for (int i = pass; i < 6; i += 2) Warn(min + (i + 0.5f) * w, w - 0.2f, WarnRed, 0.8f);
                yield return Wait(0.8f);
                Cam?.Shake(0.2f, 0.25f);
                for (int i = pass; i < 6; i += 2)
                    for (float x = min + i * w + 0.45f; x < min + (i + 1) * w - 0.2f; x += 0.95f) Fang(x, 1.8f, 0.7f, Damage * 1.1f, CrimsonColor);
                StageProgress = (pass + 1) / 2f;
                yield return Wait(0.7f);
            }
            for (int i = 0; i < n; i++) body2.SetPlateLift(i, 0);
            StartCoroutine(ShadeTo(0.24f, 0.8f, new Color(0.12f, 0.01f, 0.02f)));
            SetStage(BossAttack.EclipseCarmesi, AttackStage.Recovery, 0);
        }

        // ------------------------------------------------------------------ End
        protected override void OnDeath(Health ignored)
        {
            base.OnDeath(ignored); // stops every coroutine (intro, attacks, transition)
            routine = null; fightRoutine = null; transitioning = false;
            AttacksSinceRest = 0; CombatState = BossCombatState.Defeated;
            ClearHazards();
            stun?.Close();
            if (headCollider != null) headCollider.enabled = false;
            PatternName = "Derrotado";
            SetStage(BossAttack.None, AttackStage.None, 0);
            player?.ClearExtraMaxX();
            Cam?.ClearFocusOverride();
            if (game != null && player != null && player.ControlLocked && !player.Health.IsDead) game.SetPlayerControlLocked(false);
            if (shade != null) Destroy(shade.gameObject);
            if (body2 != null) body2.StartCoroutine(body2.Defeat());
        }

        protected override void OnDestroy()
        {
            ClearHazards();
            if (stun != null) stun.Close();
            if (Health != null) Health.Damaged -= OnDamaged;
            if (shade != null) Destroy(shade.gameObject);
            if (game != null && player != null && player.ControlLocked && !IntroFinished) game.SetPlayerControlLocked(false);
            player?.ClearExtraMaxX();
            base.OnDestroy();
        }
    }
}
