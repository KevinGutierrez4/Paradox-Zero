using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float castRadius = 0.07f;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        private Rigidbody2D body;
        private SpriteRenderer sprite;
        private ProjectilePool owner;
        private GameManager game;
        private ContactFilter2D filter;
        private Team team;
        private float damage, speed, remaining;
        private Vector2 direction;
        private bool flying;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            sprite = GetComponent<SpriteRenderer>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0;
            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        public void Launch(ProjectilePool pool, GameManager manager, Vector2 origin,
            Vector2 heading, float hitDamage, Team source, float velocity, float lifetime, LayerMask mask)
        {
            owner = pool; game = manager; direction = heading; team = source;
            damage = Mathf.Max(0f, hitDamage); speed = Mathf.Max(0.1f, velocity);
            remaining = Mathf.Max(0.1f, lifetime); flying = true;
            body.position = origin;
            transform.position = origin;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
            body.linearVelocity = Vector2.zero;
            sprite.color = source == Team.Player ? new Color(0.3f, 1f, 1f) : new Color(1f, 0.4f, 0.25f);
            filter = new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = false };
        }

        private void Update()
        {
            if (!flying || game == null || !game.IsPlaying) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0) Despawn();
        }

        private void FixedUpdate()
        {
            if (!flying || game == null || !game.IsPlaying) return;
            float distance = speed * Time.fixedDeltaTime;
            // Swept collision prevents fast bullets from passing through thin colliders.
            int count = Physics2D.CircleCast(body.position, castRadius, direction, filter, hits, distance);
            Collider2D closest = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform == transform) continue;
                var target = collider.GetComponentInParent<Health>();
                if (target != null && (target.Team == team || target.IsDead)) continue;
                if (hits[i].distance < nearest) { nearest = hits[i].distance; closest = collider; }
            }
            if (closest != null) { Hit(closest); return; }
            body.MovePosition(body.position + direction * distance);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!flying || game == null || !game.IsPlaying || other.isTrigger) return;
            if ((filter.layerMask.value & (1 << other.gameObject.layer)) == 0) return;
            Hit(other);
        }

        private void Hit(Collider2D collider)
        {
            var target = collider.GetComponentInParent<Health>();
            if (target != null)
            {
                if (target.Team == team || target.IsDead) return;
                target.TakeDamage(new DamageInfo(damage, team));
            }
            Despawn();
        }

        public void Despawn()
        {
            if (!flying) return;
            flying = false;
            owner.Release(this);
        }
    }
}
