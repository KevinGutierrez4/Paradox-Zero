using System.Collections;
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
        private bool telegraph;
        private float fade = 1f;
        private Coroutine fadeRoutine;
        public int Facing { get; private set; } = 1;
        /// <summary>1 = fully visible, 0 = faded out after death.</summary>
        public float Fade => fade;
        /// <summary>True when ArticulatedRig is the only system writing the joints.</summary>
        public bool RigOwnsPose => articulated != null;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            articulated = GetComponent<ArticulatedRig>();
            // Single owner per joint: the articulated rig poses every rigged character in LateUpdate
            // with the real combat clock. The baked clips/controllers stay in the project as editor
            // previews, but a running Animator would write the same transforms every frame first.
            if (articulated != null) animator.enabled = false;
            health = GetComponent<Health>();
            if (health != null) health.Damaged += OnDamaged;
            if (visual == null) visual = transform.Find("Visual");
            if (visual != null) visualScaleX = Mathf.Abs(visual.localScale.x);
            CacheRenderers();
        }

        /// <summary>Re-reads renderers after runtime parts (arrow, charge glow, boss heads) are added.</summary>
        public void CacheRenderers()
        {
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

        /// <summary>Fades every sprite after the death pose has finished (unscaled, so it also ends at timeScale 0).</summary>
        public void FadeOut(float delay, float duration)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(delay, duration));
        }

        private IEnumerator FadeRoutine(float delay, float duration)
        {
            float start = Time.unscaledTime + delay;
            while (Time.unscaledTime < start) yield return null;
            while (fade > 0)
            {
                fade = Mathf.Max(0, fade - Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
                ApplyColors(false);
                yield return null;
            }
        }

        /// <summary>Scripted visibility (portal travel). 1 = visible.</summary>
        public void SetFade(float value)
        {
            if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
            fade = Mathf.Clamp01(value);
            ApplyColors(false);
        }

        public void SetTelegraph(bool enabled)
        {
            telegraph = enabled;
            ApplyColors(false);
        }

        public void ResetPose()
        {
            facingLocked = false;
            pendingFacing = 0; hurtUntil = 0; fade = 1; telegraph = false;
            if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
            articulated?.ResetPose();
            if (animator != null && animator.enabled)
            {
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.Rebind();
                animator.Update(0);
            }
            ApplyColors(false);
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

        private void ApplyColors(bool flashing)
        {
            if (renderers == null) return;
            Color tint = flashing ? new Color(1, 0.55f, 0.45f) : telegraph ? new Color(1f, 0.8f, 0.7f) : Color.white;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = colors[i];
                // Parts named RT_* get their alpha from ArticulatedRig every frame (already multiplied by Fade):
                // only their colour is tinted here, so a hit flash can never pop a hidden drawing back in.
                float alpha = renderers[i].gameObject.name.StartsWith("RT_") ? renderers[i].color.a : c.a * fade;
                renderers[i].color = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, alpha);
            }
        }

        private void LateUpdate()
        {
            // Apply after animation evaluation so clips cannot leave the drawing facing away.
            ApplyFacing();
            if (hurtUntil <= 0) return;
            bool flashing = Time.time < hurtUntil;
            ApplyColors(flashing);
            if (!flashing) hurtUntil = 0;
        }
    }
}
