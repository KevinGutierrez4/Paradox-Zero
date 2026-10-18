using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(PlayerStats))]
    [RequireComponent(typeof(Health), typeof(AnimationDriver), typeof(MeleeWeapon))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform groundCheck;
        [SerializeField] private LayerMask groundMask;
        [SerializeField, Min(0.01f)] private float groundRadius = 0.12f;
        [SerializeField, Min(1f)] private float jumpVelocity = 13f;
        [SerializeField, Range(0, 0.3f)] private float coyoteTime = 0.1f;
        [SerializeField, Range(0, 0.3f)] private float jumpBuffer = 0.12f;
        [SerializeField, Range(0.2f, 0.9f)] private float crouchHeight = 0.72f;
        [SerializeField] private string jumpButton = "Jump";
        [SerializeField] private string fireButton = "Fire1";
        [Header("Retroceso al recibir daño")]
        [SerializeField, Min(0)] private float knockbackScale = 1.35f;
        [SerializeField, Min(0)] private float knockbackMinHorizontal = 3.2f;
        [SerializeField, Min(0)] private float knockbackMaxHorizontal = 7f;
        [SerializeField, Min(0)] private float knockbackMaxLift = 3.2f;
        [SerializeField, Range(0.05f, 0.6f)] private float knockbackControlLoss = 0.2f;
        [SerializeField, Range(0, 1)] private float guardKnockbackMultiplier = 0.45f;
        private Vector2 knockVelocity;
        private float knockTimer;
        private bool knockLiftPending;
        private float extraMaxX = float.PositiveInfinity;
        [Header("Esquiva")]
        [SerializeField, Min(1f)] private float dodgeSpeed = 13f;
        [SerializeField, Range(0.08f, 0.5f)] private float dodgeDuration = 0.2f;
        [SerializeField, Range(0.05f, 0.6f)] private float dodgeInvulnerability = 0.24f;
        [SerializeField, Range(0.1f, 2f)] private float dodgeCooldown = 0.65f;
        private float dodgeTimer, dodgeCooldownTimer, dodgeInvulTimer;
        private int dodgeDirection = 1;
        private bool dodgeRequested, dropRequested;
        private Collider2D groundCollider, droppedThrough;
        private float dropTimer;
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private AnimationDriver animationDriver;
        private MeleeWeapon weapon;
        private PlayerGuard guard;
        private GameManager game;
        private Vector2 standingSize, standingOffset;
        private float horizontal, coyoteRemaining, bufferedJump;
        private float minX, maxX;
        private bool crouchRequested, crouching, grounded, fireMustRelease, blockMustRelease;
        public PlayerGuard Guard => guard;
        /// <summary>Scripted sequences (boss presentation, portal travel) take input away without pausing physics.</summary>
        public bool ControlLocked { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool Grounded => grounded;
        /// <summary>True while a received hit is pushing the player (input partially suppressed).</summary>
        public bool IsKnockedBack => knockTimer > 0;
        public bool IsDodging => dodgeTimer > 0;
        public int DodgeCount { get; private set; }
        public int DropCount { get; private set; }
        public int JumpCount { get; private set; }
        /// <summary>Collider the player is standing on (terrain, platform or prop), null in the air.</summary>
        public Collider2D GroundCollider => grounded ? groundCollider : null;
        public static KeyCode DodgeKey => KeyCode.L;
        public Vector2 LastKnockback { get; private set; }
        public PlayerStats Stats { get; private set; }
        public PlayerLifeSteal LifeSteal { get; private set; }
        public Health Health { get; private set; }
        internal float DeathFloorY => game != null ? game.GroundHeight(transform.position.x) : transform.position.y;
        public Vector2 AimPosition => (Vector2)transform.position + hitbox.offset;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>(); hitbox = GetComponent<BoxCollider2D>();
            Stats = GetComponent<PlayerStats>(); Health = GetComponent<Health>();
            animationDriver = GetComponent<AnimationDriver>(); weapon = GetComponent<MeleeWeapon>();
            guard = GetComponent<PlayerGuard>();
            standingSize = hitbox.size; standingOffset = hitbox.offset;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Health.HitReceived += OnHitReceived;
            groundMask |= PropLayers.PropMask; // stand on solid cover too
            LifeSteal = GetComponent<PlayerLifeSteal>();
            if (LifeSteal == null) LifeSteal = gameObject.AddComponent<PlayerLifeSteal>();
        }

        private void OnDestroy() { if (Health != null) Health.HitReceived -= OnHitReceived; }

        /// <summary>Horizontal knockback away from the source; brief loss of control, then progressive recovery.</summary>
        private void OnHitReceived(DamageInfo hit)
        {
            if (hit.Source == Team.Player || Health.IsDead) return;
            Vector2 k = hit.Knockback;
            if (k.sqrMagnitude < 0.0001f) return;
            float guardScale = guard != null && guard.IsBlocking ? guardKnockbackMultiplier : 1f;
            float dir = Mathf.Sign(k.x == 0 ? -animationDriver.Facing : k.x);
            float h = Mathf.Clamp(Mathf.Abs(k.x) * knockbackScale, knockbackMinHorizontal, knockbackMaxHorizontal) * guardScale;
            float lift = Mathf.Clamp(k.y * knockbackScale, 0, knockbackMaxLift) * guardScale;
            knockVelocity = new Vector2(dir * h, lift);
            LastKnockback = knockVelocity;
            knockTimer = knockbackControlLoss;
            knockLiftPending = lift > 0.01f;
            weapon.Cancel();
            PixelFx.Burst((Vector2)transform.position + hitbox.offset, 10, new Color(1f, 0.35f, 0.25f), 3.2f, 0.32f, -6f, 0.07f, 45, new Vector2(dir * 1.5f, 0.5f));
        }

        /// <summary>Extra right-hand limit (boss front). The player cannot walk into the boss body.</summary>
        public void SetExtraMaxX(float x) { extraMaxX = x; }
        public void ClearExtraMaxX() { extraMaxX = float.PositiveInfinity; }
        private float MaxX => Mathf.Min(maxX, extraMaxX);

        public void Initialize(GameManager manager, ProjectilePool pool)
        { game = manager; weapon.Initialize(manager); guard?.Initialize(manager); }

        public void ResetForRun(StatBlock initial)
        {
            Stats.ResetStats(initial);
            weapon.Cancel();
            body.simulated = true;
            hitbox.enabled = true;
            body.linearVelocity = Vector2.zero;
            SetCrouch(false);
            coyoteRemaining = bufferedJump = 0;
            knockTimer = 0; knockVelocity = Vector2.zero; extraMaxX = float.PositiveInfinity;
            EndDodge(); dodgeCooldownTimer = 0; RestoreDrop();
            LifeSteal?.ResetRun();
            ControlLocked = false;
            animationDriver.ResetPose();
            SuppressUntilRelease();
        }

        public void SetPhaseBounds(float startX, float endX, float floorY)
        {
            minX = startX; maxX = endX; extraMaxX = float.PositiveInfinity; knockTimer = 0;
            body.position = new Vector2(startX + 2f, floorY + 0.04f);
            body.linearVelocity = Vector2.zero;
            coyoteRemaining = bufferedJump = 0;
        }

        public void SetControlLocked(bool locked)
        {
            ControlLocked = locked;
            horizontal = 0; bufferedJump = 0; InteractPressed = false;
            if (locked) { weapon.Cancel(); guard?.SetBlocking(false); EndDodge(); }
            else fireMustRelease = blockMustRelease = true; // held buttons must be released before acting again
        }

        public void SuppressUntilRelease()
        { fireMustRelease = blockMustRelease = true; horizontal = 0; bufferedJump = 0; weapon.Cancel(); guard?.SetBlocking(false); }

        private void Update()
        {
            InteractPressed = false;
            if (game == null || !game.IsPlaying || Health.IsDead || ControlLocked)
            { horizontal = 0; bufferedJump = 0; crouchRequested = false; guard?.SetBlocking(false); return; }
#if ENABLE_LEGACY_INPUT_MANAGER
            InteractPressed = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.E);
            horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            crouchRequested = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            if (Input.GetButtonDown(jumpButton))
            {
                // S + Space on a one-way platform drops through it instead of jumping.
                if (crouchRequested && grounded && groundCollider != null && groundCollider.GetComponent<PlatformEffector2D>() != null) dropRequested = true;
                else bufferedJump = jumpBuffer;
            }
            if (Input.GetKeyDown(DodgeKey) || Input.GetKeyDown(KeyCode.LeftShift)) dodgeRequested = true;
            bool fireHeld = Input.GetKey(KeyCode.J) || Input.GetButton(fireButton);
            bool blockHeld = Input.GetKey(KeyCode.K) || Input.GetMouseButton(1);
            if (!blockHeld) blockMustRelease = false;
            guard?.SetBlocking(!blockMustRelease && blockHeld);
            if (!fireHeld) fireMustRelease = false;
            if (horizontal != 0) animationDriver.Face(horizontal);
            if (!fireMustRelease && fireHeld) weapon.RequestAttack(Stats.Current.damage, Stats.Current.fireRate, crouching);
#endif
            if (transform.position.y < game.FloorY - 12f) Health.Kill();
        }

        private void FixedUpdate()
        {
            if (game == null || !game.IsPlaying || Health.IsDead) return;
            Vector2 probe = groundCheck != null ? (Vector2)groundCheck.position
                : (Vector2)transform.position + Vector2.up * 0.04f;
            var groundHit = Physics2D.Raycast(probe + Vector2.up * 0.14f, Vector2.down, 0.34f, groundMask);
            if (groundHit.collider != null && groundHit.collider == droppedThrough) groundHit = default;
            grounded = body.linearVelocity.y <= 1.5f && groundHit.collider != null;
            groundCollider = groundHit.collider;
            if (dropTimer > 0) { dropTimer -= Time.fixedDeltaTime; if (dropTimer <= 0) RestoreDrop(); }
            if (dropRequested)
            {
                dropRequested = false;
                if (grounded && groundCollider != null && groundCollider.GetComponent<PlatformEffector2D>() != null)
                {
                    droppedThrough = groundCollider; dropTimer = 0.4f; DropCount++;
                    Physics2D.IgnoreCollision(hitbox, droppedThrough, true);
                    grounded = false; groundCollider = null;
                    body.position += Vector2.down * 0.08f;
                }
            }
            dodgeCooldownTimer -= Time.fixedDeltaTime;
            if (dodgeRequested)
            {
                dodgeRequested = false;
                if (dodgeCooldownTimer <= 0 && !ControlLocked)
                {
                    dodgeDirection = horizontal != 0 ? (int)Mathf.Sign(horizontal) : animationDriver.Facing;
                    dodgeTimer = dodgeDuration; dodgeInvulTimer = dodgeInvulnerability; dodgeCooldownTimer = dodgeCooldown;
                    Health.DodgeInvulnerable = true; DodgeCount++;
                    knockTimer = 0; knockLiftPending = false; // the dash cancels any remaining knockback
                    weapon.Cancel(); guard?.SetBlocking(false);
                    animationDriver.Face(dodgeDirection);
                }
            }
            if (dodgeInvulTimer > 0) { dodgeInvulTimer -= Time.fixedDeltaTime; if (dodgeInvulTimer <= 0) Health.DodgeInvulnerable = false; }
            coyoteRemaining = grounded ? coyoteTime : coyoteRemaining - Time.fixedDeltaTime;
            bufferedJump -= Time.fixedDeltaTime;
            if (crouchRequested && grounded) SetCrouch(true);
            else if (crouching && HasStandingClearance()) SetCrouch(false);
            var velocity = body.linearVelocity;
            velocity.x = horizontal * Stats.Current.moveSpeed * (crouching ? 0.45f : 1f) * (guard != null ? guard.MoveMultiplier : 1);
            if (grounded && bufferedJump <= 0 && !knockLiftPending)
            {
                // Follow the tangent; a small downward bias keeps feet in contact on descents.
                Vector2 tangent = new Vector2(groundHit.normal.y, -groundHit.normal.x);
                velocity.y = velocity.x * tangent.y / Mathf.Max(0.25f, tangent.x) - 1f;
            }
            if (dodgeTimer > 0)
            {
                // Short horizontal dash with invulnerability frames; walls stop it like any movement.
                velocity.x = dodgeDirection * dodgeSpeed;
                dodgeTimer -= Time.fixedDeltaTime;
                if (Random.value < 0.7f) PixelFx.Emit((Vector2)transform.position + Vector2.up * Random.Range(0.3f, 1.6f), new Vector2(-dodgeDirection * 2f, 0), new Color(0.75f, 0.9f, 1f, 0.6f), 0.25f, 0, 0.09f, 9);
            }
            if (bufferedJump > 0 && coyoteRemaining > 0 && !crouching)
            {
                JumpCount++;
                velocity.y = jumpVelocity;
                bufferedJump = coyoteRemaining = 0;
                grounded = false;
            }
            if (knockTimer > 0 && dodgeTimer <= 0)
            {
                // Mostly horizontal push; input regains authority progressively. Walls stop it (dynamic body vs ground).
                float k = knockTimer / knockbackControlLoss;
                velocity.x = Mathf.Lerp(velocity.x * 0.35f, knockVelocity.x, k);
                if (knockLiftPending) { velocity.y = Mathf.Max(velocity.y, knockVelocity.y); knockLiftPending = false; grounded = false; }
                knockTimer -= Time.fixedDeltaTime;
            }
            float right = MaxX;
            if ((body.position.x <= minX && velocity.x < 0) || (body.position.x >= right && velocity.x > 0)) velocity.x = 0;
            body.position = new Vector2(Mathf.Clamp(body.position.x, minX, Mathf.Max(minX, right)), body.position.y);
            body.linearVelocity = velocity;
            animationDriver.SetMotion(velocity.x, grounded, crouching, velocity.y);
        }

        private void EndDodge()
        {
            dodgeTimer = 0; dodgeInvulTimer = 0; dodgeRequested = false;
            if (Health != null) Health.DodgeInvulnerable = false;
        }

        private void RestoreDrop()
        {
            if (droppedThrough != null && hitbox != null) Physics2D.IgnoreCollision(hitbox, droppedThrough, false);
            droppedThrough = null; dropTimer = 0;
        }

        private bool HasStandingClearance()
        {
            float top = standingOffset.y + standingSize.y * 0.5f;
            float currentTop = hitbox.offset.y + hitbox.size.y * 0.5f;
            var center = (Vector2)transform.position + new Vector2(standingOffset.x, (top + currentTop) * 0.5f);
            return Physics2D.OverlapBox(center, new Vector2(standingSize.x * 0.9f, Mathf.Max(0.02f, top - currentTop - 0.03f)), 0, groundMask) == null;
        }

        private void SetCrouch(bool value)
        {
            crouching = value;
            hitbox.size = new Vector2(standingSize.x, standingSize.y * (value ? crouchHeight : 1f));
            hitbox.offset = standingOffset - Vector2.up * (standingSize.y - hitbox.size.y) * 0.5f;
        }

        public void PlayDeath()
        {
            EndDodge();
            weapon.Cancel();
            guard?.SetBlocking(false);
            body.linearVelocity = Vector2.zero;
            animationDriver.PlayDeath();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
    }
}
