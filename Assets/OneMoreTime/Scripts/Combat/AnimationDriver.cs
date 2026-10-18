using UnityEngine;

namespace OneMoreTime
{
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class AnimationDriver : MonoBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int Crouch = Animator.StringToHash("Crouch");
        private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Dead = Animator.StringToHash("Dead");
        private static readonly int Pattern = Animator.StringToHash("Pattern");
        [SerializeField] private Transform visual;
        private Animator animator;
        private SpriteRenderer[] renderers;
        private Color[] colors;
        private float visualScaleX = 1f;
        private bool facingLocked;
        private float pendingFacing;
        private ArticulatedRig articulated;
        private Health health;
        private float hurtUntil;
        public int Facing { get; private set; } = 1;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            articulated = GetComponent<ArticulatedRig>();
            // The articulated shield rig owns its transforms; Animator clips remain preview assets.
            if (articulated != null && articulated.HasShieldRig) animator.enabled = false;
            health = GetComponent<Health>();
            if (health != null) health.Damaged += OnDamaged;
            if (visual == null) visual = transform.Find("Visual");
            if (visual != null) visualScaleX = Mathf.Abs(visual.localScale.x);
            renderers = visual != null ? visual.GetComponentsInChildren<SpriteRenderer>(true) : GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[renderers.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = renderers[i].color;
        }

        public void Face(float horizontal)
        {
            if (Mathf.Abs(horizontal) < 0.001f) return;
            if (facingLocked) { pendingFacing = horizontal; return; }
            Facing = horizontal > 0 ? 1 : -1;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            if (visual != null)
            {
                var scale = visual.localScale;
                // Facing is a world direction. An already mirrored parent must not reverse it.
                float parentX = visual.parent != null ? visual.parent.TransformVector(Vector3.right).x : 1;
                scale.x = visualScaleX * Facing * (parentX < 0 ? -1 : 1);
                visual.localScale = scale;
            }
        }

        public void SetMotion(float speed, bool grounded, bool crouch, float vertical)
        {
            if (Mathf.Abs(speed) > 0.1f) Face(speed);
            articulated?.Motion(speed, grounded, crouch, vertical);
            if (!animator.enabled || animator.runtimeAnimatorController == null) return;
            animator.SetFloat(Speed, Mathf.Abs(speed));
            animator.SetBool(Grounded, grounded);
            animator.SetBool(Crouch, crouch);
            animator.SetFloat(VerticalSpeed, vertical);
        }

        public void PlayAttack(int pattern = 0)
        {
            articulated?.Attack(pattern == 2 ? 0.85f : 0.8f);
            if (!animator.enabled || animator.runtimeAnimatorController == null) return;
            animator.SetInteger(Pattern, pattern);
            animator.SetTrigger(Attack);
        }

        public void PlayDeath()
        {
            articulated?.Die();
            // A defeated player's animation can finish behind the result UI at timeScale = 0.
            if (GetComponent<PlayerController>() != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (animator.enabled && animator.runtimeAnimatorController != null) animator.SetBool(Dead, true);
        }

        public void SetTelegraph(bool enabled)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = enabled ? new Color(1f, 0.45f, 0.3f) : colors[i];
        }

        public void ResetPose()
        {
            facingLocked = false;
            pendingFacing = 0; hurtUntil = 0;
            articulated?.ResetPose();
            if (animator != null && animator.enabled)
            {
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.Rebind();
                animator.Update(0);
            }
            SetTelegraph(false);
            Face(1);
        }

        public void LockFacing(bool locked)
        {
            facingLocked = locked;
            if (!locked && pendingFacing != 0) { float next = pendingFacing; pendingFacing = 0; Face(next); }
        }
        public void PlayHurt() { articulated?.Hurt(); hurtUntil = Time.time + 0.12f; }
        private void OnDamaged(Health ignored) { PlayHurt(); }
        private void OnDestroy() { if (health != null) health.Damaged -= OnDamaged; }
        private void LateUpdate()
        {
            // Apply after animation evaluation so clips cannot leave the drawing facing away.
            ApplyFacing();
            if (hurtUntil <= 0) return;
            bool flashing = Time.time < hurtUntil;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = flashing ? new Color(1, 0.55f, 0.45f) : colors[i];
            if (!flashing) hurtUntil = 0;
        }
    }
}
