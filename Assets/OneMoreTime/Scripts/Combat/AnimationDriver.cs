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
        private IAttackClock attackClock;
        private MeleeWeapon melee;
        private PlayerGuard guard;
        private bool samplingAttack;
        private int attackPattern;
        private float motionSpeed, motionVertical;
        private bool motionGrounded = true, motionCrouch;
        private bool hasBlockingParameter;
        private bool specialBoss;
        private int legLayer = -1;
        private bool legLayerActive;
        private float walkPhase;
        /// <summary>Clips on the prefab's Animator are the source of the visible pose.</summary>
        public bool UsesAuthoredAnimations => !specialBoss && animator != null && animator.runtimeAnimatorController != null;
        public bool RigOwnsPose => articulated != null && !UsesAuthoredAnimations;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            specialBoss = GetComponent<BossAI>() != null;
            articulated = GetComponent<ArticulatedRig>();
            // Never replace the animator's authored curves with generated poses during play.
            animator.enabled = UsesAuthoredAnimations;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            attackClock = GetComponent<IAttackClock>();
            melee = GetComponent<MeleeWeapon>();
            guard = GetComponent<PlayerGuard>();
            if (UsesAuthoredAnimations) legLayer = animator.GetLayerIndex("LowerBodyLocomotion");
            if (UsesAuthoredAnimations)
                foreach (var parameter in animator.parameters)
                    if (parameter.name == "Blocking") { hasBlockingParameter = true; break; }
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
            motionSpeed = speed; motionGrounded = grounded; motionCrouch = crouch; motionVertical = vertical;
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
            attackPattern = pattern;
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
            if (animator.enabled && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(Dead, true);
                PlayState("Die", 0);
            }
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
            samplingAttack = false;
            legLayerActive = false; walkPhase = 0;
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
            if (animator != null && animator.enabled)
            {
                if (hasBlockingParameter) animator.SetBool("Blocking", guard != null && guard.IsBlocking);
                UpdateLegLayer();
                SynchronizeCombatPose();
                articulated?.UpdateAuthoredEffects();
            }
            // Apply after animation evaluation so clips cannot leave the drawing facing away.
            ApplyFacing();
            if (hurtUntil <= 0) return;
            bool flashing = Time.time < hurtUntil;
            ApplyColors(flashing);
            if (!flashing) hurtUntil = 0;
        }

        public void SetBlocking(bool blocking)
        {
            if (!UsesAuthoredAnimations || !animator.enabled) return;
            if (hasBlockingParameter) animator.SetBool("Blocking", blocking);
            UpdateLegLayer();
            if (blocking)
            {
                samplingAttack = false;
                animator.ResetTrigger(Attack);
                PlayState("Block", 0);
            }
            else if (animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Block")) PlayState(LocomotionState(), 0);
        }

        private void UpdateLegLayer()
        {
            if (legLayer < 0) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Base Layer.Walk")) walkPhase = state.normalizedTime;
            bool active = (health == null || !health.IsDead) && motionGrounded && !motionCrouch && Mathf.Abs(motionSpeed) > 0.1f
                && ((melee != null && melee.IsAttacking) || (guard != null && guard.IsBlocking));
            if (active && !legLayerActive)
                animator.Play("LowerBodyLocomotion.Walk", legLayer, Mathf.Repeat(walkPhase, 1));
            animator.SetLayerWeight(legLayer, active ? 1 : 0);
            legLayerActive = active;
        }

        private string LocomotionState() => !motionGrounded
            ? (motionVertical < 0 && animator.HasState(0, Animator.StringToHash("Base Layer.Fall")) ? "Fall" : "Jump")
            : guard != null && guard.IsBlocking ? "Block" : motionCrouch ? "Crouch" : Mathf.Abs(motionSpeed) > 0.1f ? "Walk" : "Idle";

        /// <summary>Samples the prefab's Attack clip on the combat clock, keeping edited keys and hit windows together.</summary>
        public void SynchronizeCombatPose()
        {
            if (!UsesAuthoredAnimations || !animator.enabled || (health != null && health.IsDead)) return;
            float progress = -1;
            if (attackClock != null && attackClock.Stage != AttackStage.None)
            {
                float p = attackClock.StageProgress;
                progress = attackClock.Stage == AttackStage.Anticipation ? p * 0.6f
                    : attackClock.Stage == AttackStage.Active ? Mathf.Lerp(0.6f, 0.72f, p) : Mathf.Lerp(0.72f, 1, p);
            }
            else if (attackClock == null && melee != null && melee.IsAttacking) progress = melee.Progress;
            if (progress >= 0)
            {
                animator.ResetTrigger(Attack);
                PlayState(attackPattern == 2 ? "Special" : motionCrouch ? "CrouchAttack" : "Attack", progress);
                samplingAttack = true;
            }
            else if (samplingAttack)
            {
                samplingAttack = false;
                PlayState(LocomotionState(), 0);
            }
        }

        private void PlayState(string state, float normalizedTime)
        {
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!animator.HasState(0, hash)) return;
            animator.Play(hash, 0, Mathf.Clamp01(normalizedTime));
            animator.Update(0);
        }
    }
}
