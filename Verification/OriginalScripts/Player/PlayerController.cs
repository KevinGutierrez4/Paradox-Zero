using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(PlayerStats))]
    [RequireComponent(typeof(Health), typeof(AnimationDriver), typeof(ProjectileWeapon))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform groundCheck;
        [SerializeField] private LayerMask groundMask;
        [SerializeField, Min(0.01f)] private float groundRadius = 0.12f;
        [SerializeField, Min(1f)] private float jumpVelocity = 13f;
        [SerializeField, Range(0, 0.3f)] private float coyoteTime = 0.1f;
        [SerializeField, Range(0, 0.3f)] private float jumpBuffer = 0.12f;
        [SerializeField, Range(0.2f, 0.9f)] private float crouchHeight = 0.55f;
        [SerializeField] private string jumpButton = "Jump";
        [SerializeField] private string fireButton = "Fire1";
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private AnimationDriver animationDriver;
        private ProjectileWeapon weapon;
        private GameManager game;
        private Vector2 standingSize, standingOffset;
        private float horizontal, coyoteRemaining, bufferedJump;
        private float minX, maxX;
        private bool crouchRequested, crouching, grounded, fireMustRelease;
        public PlayerStats Stats { get; private set; }
        public Health Health { get; private set; }
        public Vector2 AimPosition => (Vector2)transform.position + hitbox.offset;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>(); hitbox = GetComponent<BoxCollider2D>();
            Stats = GetComponent<PlayerStats>(); Health = GetComponent<Health>();
            animationDriver = GetComponent<AnimationDriver>(); weapon = GetComponent<ProjectileWeapon>();
            standingSize = hitbox.size; standingOffset = hitbox.offset;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        public void Initialize(GameManager manager, ProjectilePool pool)
        { game = manager; weapon.Initialize(pool); }

        public void ResetForRun(StatBlock initial)
        {
            Stats.ResetStats(initial);
            weapon.ResetCooldown();
            body.simulated = true;
            hitbox.enabled = true;
            body.linearVelocity = Vector2.zero;
            SetCrouch(false);
            coyoteRemaining = bufferedJump = 0;
            animationDriver.ResetPose();
            SuppressUntilRelease();
        }

        public void SetPhaseBounds(float startX, float endX, float floorY)
        {
            minX = startX; maxX = endX;
            body.position = new Vector2(startX + 2f, floorY + 0.04f);
            body.linearVelocity = Vector2.zero;
            coyoteRemaining = bufferedJump = 0;
        }

        public void SuppressUntilRelease()
        { fireMustRelease = true; horizontal = 0; bufferedJump = 0; }

        private void Update()
        {
            if (game == null || !game.IsPlaying || Health.IsDead)
            { horizontal = 0; bufferedJump = 0; return; }
#if ENABLE_LEGACY_INPUT_MANAGER
            horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            crouchRequested = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            if (Input.GetButtonDown(jumpButton)) bufferedJump = jumpBuffer;
            bool fireHeld = Input.GetKey(KeyCode.J) || Input.GetButton(fireButton);
            if (!fireHeld) fireMustRelease = false;
            if (horizontal != 0) animationDriver.Face(horizontal);
            if (!fireMustRelease && fireHeld && weapon.TryFire(Vector2.right * animationDriver.Facing,
                Stats.Current.damage, Stats.Current.fireRate)) animationDriver.PlayAttack();
#endif
            if (transform.position.y < game.FloorY - 12f) Health.Kill();
        }

        private void FixedUpdate()
        {
            if (game == null || !game.IsPlaying || Health.IsDead) return;
            Vector2 probe = groundCheck != null ? (Vector2)groundCheck.position
                : (Vector2)transform.position + Vector2.up * 0.04f;
            grounded = body.linearVelocity.y <= 0.1f && Physics2D.OverlapCircle(probe, groundRadius, groundMask) != null;
            coyoteRemaining = grounded ? coyoteTime : coyoteRemaining - Time.fixedDeltaTime;
            bufferedJump -= Time.fixedDeltaTime;
            if (crouchRequested && grounded) SetCrouch(true);
            else if (crouching && HasStandingClearance()) SetCrouch(false);
            var velocity = body.linearVelocity;
            velocity.x = horizontal * Stats.Current.moveSpeed * (crouching ? 0.45f : 1f);
            if (bufferedJump > 0 && coyoteRemaining > 0 && !crouching)
            {
                velocity.y = jumpVelocity;
                bufferedJump = coyoteRemaining = 0;
                grounded = false;
            }
            if ((body.position.x <= minX && velocity.x < 0) || (body.position.x >= maxX && velocity.x > 0)) velocity.x = 0;
            body.position = new Vector2(Mathf.Clamp(body.position.x, minX, maxX), body.position.y);
            body.linearVelocity = velocity;
            animationDriver.SetMotion(velocity.x, grounded, crouching, velocity.y);
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
