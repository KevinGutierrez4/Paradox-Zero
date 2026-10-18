using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace OneMoreTime
{
    [DisallowMultipleComponent]
    public sealed class ProjectilePool : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField, Min(1)] private int prewarm = 64;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private Sprite magicSprite;
        private ObjectPool<Projectile> pool;
        private readonly HashSet<Projectile> active = new HashSet<Projectile>();
        private readonly List<Projectile> scratch = new List<Projectile>();
        private GameManager game;
        public bool IsConfigured => projectilePrefab != null;

        public void Initialize(GameManager manager)
        {
            game = manager;
            if (pool != null || projectilePrefab == null) return;
            pool = new ObjectPool<Projectile>(
                () =>
                {
                    var bullet = Instantiate(projectilePrefab, transform);
                    bullet.gameObject.SetActive(false);
                    return bullet;
                },
                bullet => bullet.gameObject.SetActive(true),
                bullet => bullet.gameObject.SetActive(false),
                bullet => { if (bullet != null) Destroy(bullet.gameObject); },
                true, Mathf.Max(1, prewarm), 2048);
            // Hold all acquired objects until the end; otherwise prewarm would create only one.
            scratch.Clear();
            for (int i = 0; i < prewarm; i++) scratch.Add(pool.Get());
            foreach (var bullet in scratch) pool.Release(bullet);
            scratch.Clear();
        }

        public void Fire(Vector2 origin, Vector2 direction, float damage, Team team,
            float speed = 22f, float lifetime = 3f, bool groundWave = false, bool arrow = false)
        {
            if (pool == null || game == null || !game.IsPlaying || direction.sqrMagnitude < 0.001f) return;
            var bullet = pool.Get();
            active.Add(bullet);
            bullet.Launch(this, game, origin, direction.normalized, damage, team, speed, lifetime, collisionMask, groundWave);
            bullet.SetAppearance(arrow ? arrowSprite : magicSprite, arrow);
        }

        public void Release(Projectile bullet)
        {
            if (bullet == null || !active.Remove(bullet)) return;
            pool.Release(bullet);
        }

        public void ClearActive()
        {
            scratch.Clear();
            scratch.AddRange(active);
            foreach (var bullet in scratch) bullet.Despawn();
            scratch.Clear();
        }

        private void OnDestroy()
        {
            // Scene teardown destroys child objects; the pool owns no static references.
            active.Clear();
            pool?.Clear();
        }
    }
}
