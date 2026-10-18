using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>One clock owns windup, hit window, recovery and the visible swing.</summary>
    [RequireComponent(typeof(Health), typeof(AnimationDriver))]
    [DisallowMultipleComponent]
    public sealed class MeleeWeapon : MonoBehaviour
    {
        [SerializeField] private Vector2 center = new Vector2(0.85f, 1.05f);
        [SerializeField] private Vector2 size = new Vector2(1.55f, 1.6f);
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField, Range(0.05f, 0.6f)] private float windup = 0.3f;
        [SerializeField, Range(0.05f, 0.4f)] private float activeWindow = 0.2f;
        [SerializeField, Range(0f, 0.3f)] private float inputBuffer = 0.14f;
        [SerializeField, Min(0)] private float knockbackSpeed = 3f;
        [SerializeField, Min(0)] private float knockbackLift = 0.65f;
        private readonly Collider2D[] overlaps = new Collider2D[128];
        private readonly HashSet<Health> hitThisSwing = new HashSet<Health>();
        private Health owner;
        private AnimationDriver driver;
        private GameManager game;
        private float elapsed, duration, damage, queuedUntil = -1f, queuedDamage, queuedRate;
        private int facing = 1, swingIndex;
        private bool crouching;
        public bool IsAttacking { get; private set; }
        public float Progress => IsAttacking ? Mathf.Clamp01(elapsed / duration) : -1f;
        public int SwingIndex => swingIndex;
        public int SwingFacing => facing;

        private void Awake() { owner = GetComponent<Health>(); driver = GetComponent<AnimationDriver>(); }
        public void Initialize(GameManager manager)
        {
            if (game != null) game.StateChanged -= StateChanged;
            game = manager;
            if (game != null) game.StateChanged += StateChanged;
            Cancel();
        }
        private void StateChanged(RunState state) { if (state != RunState.Playing) Cancel(); }
        private void OnDestroy() { if (game != null) game.StateChanged -= StateChanged; }

        public bool RequestAttack(float hitDamage, float attacksPerSecond, bool crouch = false)
        {
            if (GetComponent<PlayerGuard>()?.IsBlocking == true) return false;
            if (game == null || !game.IsPlaying || owner.IsDead) return false;
            if (IsAttacking)
            {
                // A held button refreshes the buffer, a tap only survives the last 140 ms.
                queuedUntil = Time.time + inputBuffer; queuedDamage = hitDamage; queuedRate = attacksPerSecond;
                return false;
            }
            Begin(hitDamage, attacksPerSecond, crouch);
            return true;
        }

        private void Begin(float hitDamage, float rate, bool crouch)
        {
            damage = Mathf.Max(0, hitDamage); duration = 1f / Mathf.Clamp(rate, 0.5f, 4f);
            elapsed = 0; crouching = crouch; facing = driver.Facing;
            IsAttacking = true; swingIndex = (swingIndex + 1) % 2; hitThisSwing.Clear();
            queuedUntil = -1; driver.LockFacing(true); driver.PlayAttack();
        }

        private void FixedUpdate()
        {
            if (!IsAttacking) return;
            if (owner.IsDead || game == null) { Cancel(); return; }
            if (!game.IsPlaying) return;
            float previous = elapsed / duration;
            elapsed += Time.fixedDeltaTime;
            float current = elapsed / duration;
            // Include a crossed window even at high cadence or a long physics step.
            if (current >= windup && previous <= windup + activeWindow) ResolveHits();
            if (current > windup + activeWindow) driver.LockFacing(false);
            if (elapsed < duration) return;
            IsAttacking = false; driver.LockFacing(false);
            if (Time.time <= queuedUntil) Begin(queuedDamage, queuedRate, crouching);
        }

        private Vector2 HitCenter => (Vector2)transform.position + new Vector2(center.x * facing, center.y * (crouching ? 0.57f : 1f));

        private void ResolveHits()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = targetMask, useTriggers = false };
            int count = Physics2D.OverlapBox(HitCenter, size, 0, filter, overlaps);
            Vector2 origin = (Vector2)transform.position + Vector2.up * (crouching ? 0.55f : 1.1f);
            for (int i = 0; i < count; i++)
            {
                var target = overlaps[i].GetComponentInParent<Health>();
                if (target == null || target == owner || target.Team == owner.Team || target.IsDead || hitThisSwing.Contains(target)) continue;
                Vector2 contact = overlaps[i].ClosestPoint(origin);
                if (Physics2D.Linecast(origin, contact, obstacleMask).collider != null) continue;
                hitThisSwing.Add(target);
                target.TakeDamage(new DamageInfo(damage, owner.Team, new Vector2(facing * knockbackSpeed, knockbackLift)));
            }
        }

        public void Cancel()
        {
            IsAttacking = false; queuedUntil = -1; elapsed = 0; hitThisSwing.Clear();
            if (driver != null) driver.LockFacing(false);
        }
        private void OnDisable() { Cancel(); }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsAttacking && Progress >= windup && Progress <= windup + activeWindow ? Color.red : Color.yellow;
            int sign = Application.isPlaying ? facing : 1;
            Gizmos.DrawWireCube((Vector2)transform.position + new Vector2(center.x * sign, center.y * (crouching ? 0.57f : 1f)), size);
        }
    }
}
