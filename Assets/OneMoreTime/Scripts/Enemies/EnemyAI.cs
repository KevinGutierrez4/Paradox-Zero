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
        private float spawnX, direction = -1f, nextAttack, shotTime, attackEnd;
        private bool preparing;
        private MeleeWeapon melee;
        private Vector2 lockedAim;

        public override void Initialize(GameManager manager, PlayerController target, ProjectilePool pool,
            int phase, LayerMask floorMask, float overrideHealth = 0)
        {
            base.Initialize(manager, target, pool, phase, floorMask, overrideHealth);
            melee = GetComponent<MeleeWeapon>(); spawnX = transform.position.x;
            nextAttack = Time.time + 0.6f; preparing = false;
            game.StateChanged += StateChanged;
        }

        private void StateChanged(RunState state)
        {
            if (state == RunState.Playing) return;
            preparing = false; visuals.LockFacing(false); visuals.SetTelegraph(false);
            if (melee != null) melee.Cancel();
            nextAttack = Time.time + 0.3f;
        }

        private void FixedUpdate()
        {
            if (!CanAct) return;
            if (IsRecoiling)
            {
                // Do not let steering overwrite the impact velocity during the short recoil.
                visuals.SetMotion(0, false, false, body.linearVelocity.y);
                return;
            }
            Vector2 delta = player.AimPosition - ((Vector2)transform.position + Vector2.up * 0.7f);
            float distanceX = Mathf.Abs(delta.x);
            bool detected = distanceX <= detectionRange;
            if (detected) direction = delta.x >= 0 ? 1 : -1;
            else if (Mathf.Abs(transform.position.x - spawnX) > patrolDistance) direction = transform.position.x > spawnX ? -1 : 1;
            visuals.Face(direction);
            bool inRange = detected && distanceX <= attackRange && Mathf.Abs(delta.y) < (ranged ? 6 : 1.35f);
            bool clearShot = !ranged || (weapon != null && Physics2D.Linecast(weapon.MuzzlePosition, player.AimPosition, groundMask).collider == null);
            float speed = (!inRange || !clearShot) && !preparing && (melee == null || !melee.IsAttacking)
                ? baseSpeed * (1 + (difficulty - 1f) * 0.3f) * direction : 0;
            Vector2 footProbe = (Vector2)transform.position + new Vector2(direction * 0.5f, 0.7f);
            var floor = Physics2D.Raycast(footProbe, Vector2.down, 1.5f, groundMask);
            if (floor.collider == null) { speed = 0; if (!detected) direction *= -1; }
            if (transform.position.x <= game.PhaseStart + 0.5f && speed < 0) speed = 0;
            if (transform.position.x >= game.PhaseEnd - 0.5f && speed > 0) speed = 0;
            float vy = body.linearVelocity.y;
            if (floor.collider != null && Mathf.Abs(floor.point.y - transform.position.y) < 0.3f && vy < 1.5f)
                vy = -speed * floor.normal.x / Mathf.Max(0.25f, floor.normal.y) - 1f;
            body.linearVelocity = new Vector2(speed, vy);
            visuals.SetMotion(speed, true, false, vy);
            if (preparing)
            {
                if (Time.time >= shotTime)
                {
                    weapon.TryFire(lockedAim, Damage, 1f / attackInterval);
                    shotTime = float.PositiveInfinity; visuals.SetTelegraph(false);
                }
                if (Time.time >= attackEnd) { preparing = false; visuals.LockFacing(false); }
                return;
            }
            if (!inRange || !clearShot || Time.time < nextAttack) return;
            float interval = attackInterval / (1f + (difficulty - 1f) * 0.25f);
            if (ranged && weapon != null)
            {
                preparing = true; lockedAim = (player.AimPosition - weapon.MuzzlePosition).normalized;
                shotTime = Time.time + 0.4f; attackEnd = Time.time + 0.8f;
                nextAttack = Time.time + Mathf.Max(0.85f, interval);
                visuals.LockFacing(true); visuals.PlayAttack(); visuals.SetTelegraph(true);
            }
            else if (melee != null && melee.RequestAttack(Damage, 1f / Mathf.Max(0.55f, interval)))
                nextAttack = Time.time + interval;
        }
        protected override void OnHitReceived(DamageInfo hit)
        {
            base.OnHitReceived(hit);
            if (!IsRecoiling) return;
            preparing = false; visuals.LockFacing(false);
            nextAttack = Mathf.Max(nextAttack, Time.time + 0.3f);
        }
        protected override void OnDestroy()
        {
            if (game != null) game.StateChanged -= StateChanged;
            base.OnDestroy();
        }
    }
}
