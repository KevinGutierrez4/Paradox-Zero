using UnityEngine;

namespace OneMoreTime
{
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
        public int Facing { get; private set; } = 1;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (visual == null) visual = transform.Find("Visual");
            if (visual != null) visualScaleX = Mathf.Abs(visual.localScale.x);
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[renderers.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = renderers[i].color;
        }

        public void Face(float horizontal)
        {
            if (Mathf.Abs(horizontal) < 0.001f) return;
            Facing = horizontal > 0 ? 1 : -1;
            if (visual != null)
            {
                var scale = visual.localScale;
                scale.x = visualScaleX * Facing;
                visual.localScale = scale;
            }
        }

        public void SetMotion(float speed, bool grounded, bool crouch, float vertical)
        {
            if (animator.runtimeAnimatorController == null) return;
            animator.SetFloat(Speed, Mathf.Abs(speed));
            animator.SetBool(Grounded, grounded);
            animator.SetBool(Crouch, crouch);
            animator.SetFloat(VerticalSpeed, vertical);
        }

        public void PlayAttack(int pattern = 0)
        {
            if (animator.runtimeAnimatorController == null) return;
            animator.SetInteger(Pattern, pattern);
            animator.SetTrigger(Attack);
        }

        public void PlayDeath()
        {
            // A defeated player's animation can finish behind the result UI at timeScale = 0.
            if (GetComponent<PlayerController>() != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (animator.runtimeAnimatorController != null) animator.SetBool(Dead, true);
        }

        public void SetTelegraph(bool enabled)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = enabled ? new Color(1f, 0.45f, 0.3f) : colors[i];
        }

        public void ResetPose()
        {
            if (animator == null) return;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.Rebind();
            animator.Update(0);
            SetTelegraph(false);
            Face(1);
        }
    }
}
