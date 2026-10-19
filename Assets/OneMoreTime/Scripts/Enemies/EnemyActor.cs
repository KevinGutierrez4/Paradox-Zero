using System;
using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
    [RequireComponent(typeof(AnimationDriver))]
    public abstract class EnemyActor : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float baseHealth = 32f;
        [SerializeField, Min(0.1f)] protected float baseSpeed = 2.2f;
        [SerializeField, Min(1f)] protected float baseDamage = 8f;
        [SerializeField, Min(0.1f)] private float deathDuration = 0.5f;
        [Tooltip("La pose de muerte dura ~0,5 s; el cuerpo permanece este tiempo mínimo y se desvanece al final.")]
        [SerializeField, Min(0.3f)] private float minimumCorpseTime = 1.25f;
        [SerializeField, Min(0.05f)] private float corpseFade = 0.4f;
        [Tooltip("0 = retroceso completo, 1 = inamovible (jefes).")]
        [SerializeField, Range(0, 1)] private float knockbackResistance;
        [SerializeField, Range(0.05f, 0.5f)] private float knockbackDuration = 0.12f;
        private float knockbackUntil;
        protected GameManager game;
        protected PlayerController player;
        protected ProjectilePool projectiles;
        protected Rigidbody2D body;
        protected AnimationDriver visuals;
        protected ProjectileWeapon weapon;
        protected LayerMask groundMask;
        protected float difficulty;
        public Health Health { get; private set; }
        public bool IsAlive => Health != null && !Health.IsDead;
        public event Action<EnemyActor> Defeated;
        public event Action<EnemyActor> Removed;

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>(); Health = GetComponent<Health>();
            visuals = GetComponent<AnimationDriver>(); weapon = GetComponent<ProjectileWeapon>();
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Health.Died += OnDeath;
            Health.HitReceived += OnHitReceived;
        }

        public virtual void Initialize(GameManager manager, PlayerController target, ProjectilePool pool,
            int phase, LayerMask floorMask, float overrideHealth = 0)
        {
            game = manager; player = target; projectiles = pool; groundMask = floorMask;
            difficulty = 1f + 0.18f * Mathf.Max(0, phase - 1);
            Health.ResetHealth(overrideHealth > 0 ? overrideHealth : baseHealth * difficulty, Team.Enemy);
            knockbackUntil = 0;
            if (weapon != null) weapon.Initialize(pool, manager);
            var melee = GetComponent<MeleeWeapon>();
            if (melee != null) melee.Initialize(manager);
        }

        protected bool CanAct => game != null && game.IsPlaying && IsAlive && player != null && !player.Health.IsDead;
        protected float Damage => baseDamage * (1f + (difficulty - 1f) * 0.6f);
        protected bool IsRecoiling => Time.time < knockbackUntil;

        protected virtual void OnHitReceived(DamageInfo hit)
        {
            if (hit.Source == Team.Player && IsAlive)
            {
                var c = GetComponent<BoxCollider2D>();
                Vector2 at = c != null ? (Vector2)c.bounds.center : (Vector2)transform.position;
                PixelFx.Burst(at, 8, new Color(1f, 0.92f, 0.75f), 3f, 0.25f, -5f, 0.06f, 45, new Vector2(Mathf.Sign(hit.Knockback.x) * 1.2f, 0.4f));
            }
            // Anchored bosses resist displacement; ordinary enemies keep physical wall collisions.
            if (!CanAct || hit.Source != Team.Player || hit.Knockback.sqrMagnitude < 0.001f
                || knockbackResistance >= 1 || (body.constraints & RigidbodyConstraints2D.FreezePositionX) != 0) return;
            Vector2 velocity = hit.Knockback * (1 - knockbackResistance);
            body.linearVelocity = new Vector2(velocity.x, Mathf.Max(body.linearVelocity.y, velocity.y));
            knockbackUntil = Time.time + knockbackDuration;
            var melee = GetComponent<MeleeWeapon>();
            if (melee != null) melee.Cancel();
            visuals.SetTelegraph(false);
        }

        protected virtual void OnDeath(Health ignored)
        {
            StopAllCoroutines();
            knockbackUntil = 0;
            var melee = GetComponent<MeleeWeapon>();
            if (melee != null) melee.Cancel();
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            GetComponent<BoxCollider2D>().enabled = false;
            visuals.SetTelegraph(false);
            visuals.PlayDeath();
            Defeated?.Invoke(this);
            // Keep the body until the fall has landed, then fade it (unscaled, so it also completes at timeScale 0).
            float total = Mathf.Max(deathDuration, minimumCorpseTime);
            if (FadesCorpse) visuals.FadeOut(total - corpseFade, corpseFade);
            if (DestroysCorpse) Destroy(gameObject, total);
        }

        /// <summary>Bosses keep their defeat pose on screen; ordinary enemies fade and are removed.</summary>
        protected virtual bool FadesCorpse => true;
        protected virtual bool DestroysCorpse => true;

        protected virtual void OnDestroy()
        {
            if (Health != null) { Health.Died -= OnDeath; Health.HitReceived -= OnHitReceived; }
            Removed?.Invoke(this);
        }
    }
}
