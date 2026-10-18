using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Health))]
    public sealed class ProjectileWeapon : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(1f)] private float projectileSpeed = 22f;
        [SerializeField] private bool arrow;
        private ProjectilePool pool;
        private Health owner;
        private float nextShot;
        private GameManager game;
        public Vector2 MuzzlePosition => muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position + Vector2.up;

        public void Initialize(ProjectilePool projectilePool, GameManager manager = null)
        {
            game = manager;
            pool = projectilePool;
            owner = GetComponent<Health>();
            ResetCooldown();
        }

        public void ResetCooldown() { nextShot = Time.time; }

        public bool TryFire(Vector2 direction, float damage, float shotsPerSecond)
        {
            if (pool == null || game == null || !game.IsPlaying || owner == null || owner.IsDead || Time.time < nextShot) return false;
            float interval = 1f / Mathf.Clamp(shotsPerSecond, 0.25f, 25f);
            if (Time.time - nextShot > interval) nextShot = Time.time;
            nextShot += interval; // Preserve cadence across uneven frame times without a catch-up burst.
            pool.Fire(MuzzlePosition, direction, damage, owner.Team, projectileSpeed, 3, false, arrow);
            return true;
        }
    }
}
