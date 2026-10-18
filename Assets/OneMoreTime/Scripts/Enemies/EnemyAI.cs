using UnityEngine;

namespace OneMoreTime
{
    public enum EnemyArchetype { Auto, WhiteWolf, BlueWolf, Archer, Mage }
    public enum EnemyState { Idle, Patrol, Chase, Anticipation, Attack, Recovery, Hurt, Dead }

    /// <summary>
    /// Idle → Patrol → Chase → Anticipation → Attack → Recovery → Chase/Idle, with Hurt and Death on top.
    /// Each archetype has its own movement and its own three-stage attack. The stages are an
    /// <see cref="IAttackClock"/> read by ArticulatedRig, so the visible pose and the damage frame match.
    /// </summary>
    public sealed class EnemyAI : EnemyActor, IAttackClock
    {
        [SerializeField] private bool ranged = true;
        [SerializeField, Min(1f)] private float detectionRange = 16f;
        [SerializeField, Min(0.5f)] private float attackRange = 7f;
        [SerializeField, Min(0.2f)] private float attackInterval = 1.1f;
        [SerializeField, Min(0.5f)] private float patrolDistance = 2f;
        [Tooltip("Auto = deducido del rig y del nombre del prefab (BlueWolf → lobo azul).")]
        [SerializeField] private EnemyArchetype archetype = EnemyArchetype.Auto;

        private struct Profile
        {
            public float speed, range, cooldown, verticalReach;
            public AttackTiming timing;
            public float lungeSpeed, leapSpeed, leapLift, backstep;
            public float hitFrom, hitTo; public Vector2 hitBox;
            public float keepAway;
        }

        private readonly AttackTimeline timeline = new AttackTimeline();
        private Profile profile;
        private EnemyArchetype kind;
        private EnemyState state = EnemyState.Idle;
        private float spawnX, direction = -1f, nextAttack, stateUntil;
        private bool preparing; // true from Anticipation to the end of Recovery (kept for diagnostics)
        private MeleeWeapon melee;
        private ArticulatedRig rig;
        private Transform biteAnchor;
        private Collider2D bodyCollider, playerCollider;
        private Vector2 lockedAim = Vector2.right;
        private float aimDegrees;
        private bool detected;
        private int attackFacing = 1;

        public EnemyState State => state;
        public EnemyArchetype Archetype => kind;
        public AttackStage Stage => timeline.Stage;
        public float StageProgress => timeline.Progress;
        public float AimAngle => aimDegrees;
        public int Variant => kind == EnemyArchetype.BlueWolf ? 1 : 0;
        private bool IsMelee => kind == EnemyArchetype.WhiteWolf || kind == EnemyArchetype.BlueWolf;

        public override void Initialize(GameManager manager, PlayerController target, ProjectilePool pool,
            int phase, LayerMask floorMask, float overrideHealth = 0)
        {
            base.Initialize(manager, target, pool, phase, floorMask, overrideHealth);
            melee = GetComponent<MeleeWeapon>(); rig = GetComponent<ArticulatedRig>();
            bodyCollider = GetComponent<Collider2D>(); playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
            spawnX = transform.position.x;
            kind = ResolveArchetype();
            profile = BuildProfile(kind);
            biteAnchor = transform.Find("Visual/Rig/Neck/Head");
            if (kind == EnemyArchetype.Archer && pool != null) rig?.ConfigureNockedArrow(pool.ArrowSprite);
            if (kind == EnemyArchetype.Mage) rig?.ConfigureChargeGlow(PixelFx.SoftDot);
            timeline.Cancel(); preparing = false; state = EnemyState.Idle; stateUntil = Time.time + 0.2f;
            // Melee enemies are allowed to engage a little sooner; ranged ones must show the full draw/channel.
            nextAttack = Time.time + (IsMelee ? 0.35f : 0.6f);
            game.StateChanged -= StateChanged;
            game.StateChanged += StateChanged;
        }

        private EnemyArchetype ResolveArchetype()
        {
            if (archetype != EnemyArchetype.Auto) return archetype;
            var rk = rig != null ? rig.Kind : RigKind.Archer;
            if (rk == RigKind.Wolf || !ranged) return name.Contains("Blue") ? EnemyArchetype.BlueWolf : EnemyArchetype.WhiteWolf;
            if (rk == RigKind.Mage) return EnemyArchetype.Mage;
            return EnemyArchetype.Archer;
        }

        private Profile BuildProfile(EnemyArchetype type)
        {
            float tempo = 1f + (difficulty - 1f) * 0.25f;      // later phases attack more often
            float read = 1f + (difficulty - 1f) * 0.12f;       // ...but anticipation stays readable
            var p = new Profile { speed = baseSpeed, range = attackRange, cooldown = attackInterval / tempo, verticalReach = 6, keepAway = 0 };
            switch (type)
            {
                case EnemyArchetype.WhiteWolf:
                    // Predator: steady pursuit, clear crouch, short lunge, bite on the jaw snap.
                    p.range = Mathf.Max(attackRange, 1.9f); p.verticalReach = 1.35f;
                    p.timing = new AttackTiming(0.42f / read, 0.22f, 0.48f);
                    p.lungeSpeed = 6.5f; p.hitFrom = 0.45f; p.hitTo = 0.85f; p.hitBox = new Vector2(0.95f, 0.85f);
                    p.cooldown = Mathf.Max(0.7f, attackInterval / tempo);
                    break;
                case EnemyArchetype.BlueWolf:
                    // Faster and twitchier: hop back, leap, very short bite window, long punishable landing.
                    p.speed = baseSpeed * 1.3f; p.range = Mathf.Max(attackRange, 2.7f); p.verticalReach = 1.6f;
                    p.timing = new AttackTiming(0.30f / read, 0.30f, 0.62f);
                    p.leapSpeed = 7.5f; p.leapLift = 5.2f; p.backstep = 2.6f;
                    p.hitFrom = 0.55f; p.hitTo = 0.75f; p.hitBox = new Vector2(0.85f, 0.8f);
                    p.cooldown = Mathf.Max(0.55f, attackInterval * 0.8f / tempo);
                    break;
                case EnemyArchetype.Archer:
                    p.timing = new AttackTiming(0.9f / read, 0.1f, 0.45f);
                    p.keepAway = 2.6f; p.cooldown = Mathf.Max(0.6f, attackInterval / tempo - 0.45f);
                    break;
                default: // Mage
                    p.timing = new AttackTiming(1.0f / read, 0.16f, 0.6f);
                    p.keepAway = 3.2f; p.cooldown = Mathf.Max(0.7f, attackInterval / tempo - 0.4f);
                    break;
            }
            p.speed *= 1 + (difficulty - 1f) * 0.3f;
            return p;
        }

        private void StateChanged(RunState runState)
        {
            if (runState == RunState.Playing) return;
            AbortAttack();
            nextAttack = Time.time + 0.3f;
        }

        private void AbortAttack()
        {
            timeline.Cancel(); preparing = false;
            visuals.LockFacing(false); visuals.SetTelegraph(false);
            if (melee != null) melee.Cancel();
            if (state == EnemyState.Anticipation || state == EnemyState.Attack || state == EnemyState.Recovery) state = EnemyState.Chase;
        }

        private float DistanceToPlayerX => Mathf.Abs(player.transform.position.x - transform.position.x);

        // Clock and stage transitions run per frame so the pose is smooth; physics steering runs in FixedUpdate.
        private void Update()
        {
            if (!CanAct || state == EnemyState.Dead) return;
            if (state == EnemyState.Hurt && Time.time >= stateUntil) state = detected ? EnemyState.Chase : EnemyState.Patrol;
            if (!timeline.Running) return;
            if (timeline.Stage == AttackStage.Anticipation) TrackAim(timeline.Progress);
            var entered = timeline.Tick(Time.deltaTime);
            if (entered == AttackStage.Active) BeginActive();
            else if (entered == AttackStage.Recovery) { state = EnemyState.Recovery; visuals.LockFacing(IsMelee); }
            if (!timeline.Running) FinishAttack();
            else if (timeline.Stage == AttackStage.Anticipation) visuals.SetTelegraph(timeline.Progress > (ranged ? 0.8f : 0.55f));
            if (kind == EnemyArchetype.Mage && timeline.Stage == AttackStage.Anticipation && timeline.Progress > 0.3f && Random.value < Time.deltaTime * 14f)
            {
                // Sparks drawn in towards the staff head make the channel readable without hiding the player.
                Vector2 at = weapon != null ? weapon.MuzzlePosition : (Vector2)transform.position + Vector2.up * 2;
                Vector2 from = at + Random.insideUnitCircle.normalized * 0.7f;
                PixelFx.Emit(from, (at - from) * 2.2f, new Color(0.55f, 1f, 0.6f, 0.9f), 0.35f, 0, 0.07f, 3);
            }
        }

        private void TrackAim(float progress)
        {
            if (!ranged || weapon == null) return;
            // Aim follows the target while drawing, then locks for the final 20 % (the "aim" beat).
            if (progress > 0.8f && kind == EnemyArchetype.Archer) return;
            if (progress > 0.85f && kind == EnemyArchetype.Mage) return;
            Vector2 aim = player.AimPosition - weapon.MuzzlePosition;
            if (aim.sqrMagnitude < 0.01f) return;
            lockedAim = aim.normalized;
            Vector2 local = new Vector2(lockedAim.x * attackFacing, lockedAim.y);
            // Elevated archers aim steeply down at targets near the base of their post.
            aimDegrees = Mathf.Clamp(Mathf.Atan2(local.y, Mathf.Max(0.05f, local.x)) * Mathf.Rad2Deg, Post != null ? -62 : -35, 35);
        }

        private void StartAttack()
        {
            attackFacing = visuals.Facing;
            preparing = true; state = EnemyState.Anticipation;
            aimDegrees = 0; lockedAim = new Vector2(attackFacing, 0);
            TrackAim(0);
            timeline.Begin(profile.timing);
            visuals.LockFacing(true);
            if (melee != null) melee.BeginExternalSwing();
        }

        private void BeginActive()
        {
            state = EnemyState.Attack;
            visuals.SetTelegraph(false);
            if (kind == EnemyArchetype.BlueWolf)
            {
                body.linearVelocity = new Vector2(attackFacing * profile.leapSpeed, profile.leapLift);
            }
            else if (ranged && weapon != null)
            {
                // Release frame: the projectile leaves the string / staff head exactly now.
                Vector2 origin = weapon.MuzzlePosition;
                weapon.FireNow(origin, lockedAim, Damage);
                if (kind == EnemyArchetype.Mage) PixelFx.Burst(origin, 8, new Color(0.6f, 1f, 0.65f, 0.9f), 2.2f, 0.3f, 0, 0.08f, 3);
            }
        }

        private void FinishAttack()
        {
            preparing = false; state = detected ? EnemyState.Chase : EnemyState.Patrol;
            visuals.LockFacing(false); visuals.SetTelegraph(false);
            nextAttack = Time.time + profile.cooldown;
        }

        /// <summary>Elevated post this archer defends (never leaves it, never falls off).</summary>
        public ArcherPost Post { get; private set; }

        public void AnchorTo(ArcherPost post)
        {
            Post = post;
            if (post != null) post.Occupant = this;
        }

        private bool ClearLine(Vector2 from, Vector2 to)
        {
            // One-way platforms do not block a shot; terrain and solid cover do.
            foreach (var h in Physics2D.LinecastAll(from, to, groundMask | PropLayers.PropMask))
            {
                if (h.collider == null || h.collider.GetComponent<PlatformEffector2D>() != null) continue;
                var cover = h.collider.GetComponentInParent<ArenaCover>();
                if (cover != null && !cover.BlocksProjectiles) continue;
                return false;
            }
            return true;
        }

        private void FixedUpdate()
        {
            if (!CanAct) return;
            if (IsRecoiling)
            {
                // Do not let steering overwrite the impact velocity during the short recoil.
                visuals.SetMotion(0, false, false, body.linearVelocity.y);
                return;
            }
            Vector2 delta = player.AimPosition - ((Vector2)transform.position + Vector2.up * 0.7f);
            float distanceX = Mathf.Abs(delta.x);
            // Hysteresis: acquire at detectionRange, lose at 125 % so the chase never flickers.
            detected = distanceX <= (detected ? detectionRange * 1.25f : detectionRange) && Mathf.Abs(delta.y) < 5f;
            var floor = Physics2D.Raycast((Vector2)transform.position + new Vector2(direction * 0.5f, 0.7f), Vector2.down, 1.5f, groundMask);
            bool onGround = Physics2D.Raycast((Vector2)transform.position + Vector2.up * 0.3f, Vector2.down, 0.5f, groundMask).collider != null;
            float speed = 0;

            if (timeline.Running) speed = AttackMotion(onGround);
            else if (state == EnemyState.Hurt) speed = 0;
            else
            {
                if (detected)
                {
                    // Turn only with a clear horizontal offset so overlapping targets do not cause flip-flopping.
                    if (Mathf.Abs(delta.x) > 0.3f) direction = delta.x >= 0 ? 1 : -1;
                    state = EnemyState.Chase;
                }
                else if (state == EnemyState.Chase) { state = EnemyState.Patrol; }
                visuals.Face(direction);
                bool inRange = detected && distanceX <= profile.range && Mathf.Abs(delta.y) < profile.verticalReach;
                bool clearShot = !ranged || (weapon != null && ClearLine(weapon.MuzzlePosition, player.AimPosition));
                if (state == EnemyState.Chase)
                {
                    float stopAt = ranged ? profile.range * 0.8f : profile.range * 0.85f;
                    if (!inRange || !clearShot) speed = profile.speed * direction;
                    else if (ranged && distanceX < profile.keepAway) speed = -profile.speed * 0.55f * direction; // keep a firing distance
                    else if (!ranged && distanceX > stopAt) speed = profile.speed * 0.6f * direction;
                    if (inRange && clearShot && onGround && Time.time >= nextAttack) { StartAttack(); speed = 0; }
                }
                else speed = Patrol();
            }

            // Floor and arena limits apply to every movement, including lunges and leaps.
            floor = Physics2D.Raycast((Vector2)transform.position + new Vector2(Mathf.Sign(speed == 0 ? direction : speed) * 0.5f, 0.7f), Vector2.down, 1.5f, groundMask);
            if (floor.collider == null && onGround) { speed = 0; if (!detected && !timeline.Running) direction *= -1; }
            if (Post != null)
            {
                // Anchored on the post: stay between its edges whatever the steering asks.
                if ((transform.position.x <= Post.MinX && speed < 0) || (transform.position.x >= Post.MaxX && speed > 0)) speed = 0;
            }
            if (transform.position.x <= game.PhaseStart + 0.5f && speed < 0) speed = 0;
            if (transform.position.x >= game.PhaseEnd - 0.5f && speed > 0) speed = 0;
            float vy = body.linearVelocity.y;
            if (floor.collider != null && onGround && Mathf.Abs(floor.point.y - transform.position.y) < 0.3f && vy < 1.5f)
                vy = -speed * floor.normal.x / Mathf.Max(0.25f, floor.normal.y) - 1f;
            body.linearVelocity = new Vector2(speed, vy);
            visuals.SetMotion(timeline.Running ? 0 : speed, true, false, vy);
            // Facing while moving away (keep-away) must still look at the target.
            if (detected && !timeline.Running && Mathf.Abs(delta.x) > 0.3f) visuals.Face(delta.x);

            if (timeline.Running && timeline.Stage == AttackStage.Active && IsMelee) ResolveBite();
        }

        private float idleUntil;
        private float Patrol()
        {
            if (state == EnemyState.Idle)
            {
                if (Time.time < idleUntil) return 0;
                state = EnemyState.Patrol;
            }
            if (Mathf.Abs(transform.position.x - spawnX) > patrolDistance && Mathf.Sign(transform.position.x - spawnX) == Mathf.Sign(direction))
            {
                // Reached the end of the territory: pause, then turn around.
                direction *= -1; state = EnemyState.Idle; idleUntil = Time.time + Random.Range(0.5f, 1.1f);
                visuals.Face(direction);
                return 0;
            }
            visuals.Face(direction);
            return profile.speed * 0.45f * direction;
        }

        private float AttackMotion(bool onGround)
        {
            var stage = timeline.Stage; float p = timeline.Progress;
            bool touching = bodyCollider != null && playerCollider != null && bodyCollider.Distance(playerCollider).distance < 0.06f;
            switch (kind)
            {
                case EnemyArchetype.WhiteWolf:
                    // Short committed lunge in the first half of the active window; never pushes through the player.
                    if (stage == AttackStage.Active && p < 0.55f && !touching) return attackFacing * profile.lungeSpeed;
                    // After the bite it recoils a step, leaving a clear window for the player's counter-attack.
                    if (stage == AttackStage.Recovery) return -attackFacing * 1.6f * (1 - Mathf.Clamp01(p / 0.6f));
                    return 0;
                case EnemyArchetype.BlueWolf:
                    if (stage == AttackStage.Anticipation) return -attackFacing * profile.backstep * (1 - p);
                    if (stage == AttackStage.Active) return touching ? 0 : body.linearVelocity.x;
                    if (!onGround) return body.linearVelocity.x * 0.5f;
                    return -attackFacing * 1.8f * (1 - Mathf.Clamp01(p / 0.5f)); // hop back after landing
                default:
                    return 0;
            }
        }

        private void ResolveBite()
        {
            float p = timeline.Progress;
            if (melee == null || p < profile.hitFrom || p > profile.hitTo) return;
            // The bite box follows the real head/jaw position of the posed rig.
            Vector2 center = biteAnchor != null ? (Vector2)biteAnchor.position + new Vector2(attackFacing * 0.25f, -0.12f)
                : (Vector2)transform.position + new Vector2(attackFacing * 1.15f, 0.95f);
            melee.StrikeAt(center, profile.hitBox, Damage, attackFacing);
        }

        protected override void OnHitReceived(DamageInfo hit)
        {
            base.OnHitReceived(hit);
            if (!IsAlive) return;
            // The active window is committed; anything else is interrupted by a hit reaction.
            if (timeline.Stage == AttackStage.Active) return;
            AbortAttack();
            state = EnemyState.Hurt; stateUntil = Time.time + 0.24f;
            nextAttack = Mathf.Max(nextAttack, Time.time + 0.45f);
        }

        protected override void OnDeath(Health ignored)
        {
            timeline.Cancel(); preparing = false; state = EnemyState.Dead;
            base.OnDeath(ignored);
        }

        protected override void OnDestroy()
        {
            if (game != null) game.StateChanged -= StateChanged;
            base.OnDestroy();
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !IsMelee) return;
            Gizmos.color = timeline.Stage == AttackStage.Active ? Color.red : Color.yellow;
            Vector2 center = biteAnchor != null ? (Vector2)biteAnchor.position + new Vector2(attackFacing * 0.25f, -0.12f) : (Vector2)transform.position;
            Gizmos.DrawWireCube(center, profile.hitBox);
        }
    }
}
