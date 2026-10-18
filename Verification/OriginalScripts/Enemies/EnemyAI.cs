using UnityEngine;

namespace OneMoreTime
{
    public sealed class EnemyAI : EnemyActor
    {
        [SerializeField] private bool ranged = true;
        [SerializeField, Min(1f)] private float detectionRange = 16f;
        [SerializeField, Min(0.5f)] private float attackRange = 7f;
        [SerializeField, Min(0.2f)] private float attackInterval = 1.1f;
        [SerializeField, Min(0.5f)] private float patrolDistance = 2f;
        private float spawnX, direction = -1f, nextMelee;

        public override void Initialize(GameManager manager, PlayerController target, ProjectilePool pool,
            int phase, LayerMask floorMask, float overrideHealth = 0)
        {
            base.Initialize(manager, target, pool, phase, floorMask, overrideHealth);
            spawnX = transform.position.x;
            nextMelee = Time.time + 0.6f;
        }

        private void FixedUpdate()
        {
            if (!CanAct) return;
            Vector2 delta = player.AimPosition - (Vector2)transform.position;
            float distanceX = Mathf.Abs(delta.x);
            bool detected = distanceX <= detectionRange;
            if (detected) direction = delta.x >= 0 ? 1 : -1;
            else if (Mathf.Abs(transform.position.x - spawnX) > patrolDistance) direction = transform.position.x > spawnX ? -1 : 1;
            visuals.Face(direction);
            bool inRange = detected && distanceX <= attackRange;
            bool clearShot = !ranged || Physics2D.Linecast(weapon.MuzzlePosition, player.AimPosition, groundMask).collider == null;
            float speed = !inRange || !clearShot ? baseSpeed * (1 + (difficulty - 1f) * 0.3f) * direction : 0;
            // Sample ahead instead of walking off platform edges.
            Vector2 footProbe = (Vector2)transform.position + new Vector2(direction * 0.5f, 0.5f);
            if (Physics2D.Raycast(footProbe, Vector2.down, 1.5f, groundMask).collider == null)
            { speed = 0; if (!detected) direction *= -1; }
            if (transform.position.x <= game.PhaseStart + 0.5f && speed < 0) speed = 0;
            if (transform.position.x >= game.PhaseEnd - 0.5f && speed > 0) speed = 0;
            body.linearVelocity = new Vector2(speed, body.linearVelocity.y);
            visuals.SetMotion(speed, true, false, body.linearVelocity.y);
            if (!inRange || !clearShot) return;
            if (ranged)
            {
                Vector2 aim = (player.AimPosition - weapon.MuzzlePosition).normalized;
                float shotsPerSecond = (1f + (difficulty - 1f) * 0.25f) / attackInterval;
                if (weapon.TryFire(aim, Damage, shotsPerSecond)) visuals.PlayAttack();
            }
            else if (Time.time >= nextMelee && Mathf.Abs(delta.y - 0.9f) < 1.5f)
            {
                nextMelee = Time.time + attackInterval;
                visuals.PlayAttack();
                player.Health.TakeDamage(new DamageInfo(Damage, Team.Enemy));
            }
        }
    }
}
