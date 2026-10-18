using System;
using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Health))]
    [RequireComponent(typeof(AnimationDriver), typeof(ProjectileWeapon))]
    public abstract class EnemyActor : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float baseHealth = 32f;
        [SerializeField, Min(0.1f)] protected float baseSpeed = 2.2f;
        [SerializeField, Min(1f)] protected float baseDamage = 8f;
        [SerializeField, Min(0.1f)] private float deathDuration = 0.5f;
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
        }

        public virtual void Initialize(GameManager manager, PlayerController target, ProjectilePool pool,
            int phase, LayerMask floorMask, float overrideHealth = 0)
        {
            game = manager; player = target; projectiles = pool; groundMask = floorMask;
            difficulty = 1f + 0.18f * Mathf.Max(0, phase - 1);
            Health.ResetHealth(overrideHealth > 0 ? overrideHealth : baseHealth * difficulty, Team.Enemy);
            weapon.Initialize(pool);
        }

        protected bool CanAct => game != null && game.IsPlaying && IsAlive && player != null && !player.Health.IsDead;
        protected float Damage => baseDamage * (1f + (difficulty - 1f) * 0.6f);

        protected virtual void OnDeath(Health ignored)
        {
            StopAllCoroutines();
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            GetComponent<BoxCollider2D>().enabled = false;
            visuals.SetTelegraph(false);
            visuals.PlayDeath();
            Defeated?.Invoke(this);
            Destroy(gameObject, deathDuration);
        }

        protected virtual void OnDestroy()
        {
            if (Health != null) Health.Died -= OnDeath;
            Removed?.Invoke(this);
        }
    }
}
