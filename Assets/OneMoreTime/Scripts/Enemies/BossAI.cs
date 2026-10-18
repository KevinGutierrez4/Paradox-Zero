using System.Collections;
using UnityEngine;

namespace OneMoreTime
{
    public sealed class BossAI : EnemyActor
    {
        [SerializeField, Min(0.2f)] private float specialWarning = 0.85f;
        [SerializeField, Min(1f)] private float bulletSpeed = 10f;
        private Coroutine attackRoutine;
        public string PatternName { get; private set; } = "Preparando ataque";
        public bool Enraged => Health != null && Health.Current <= Health.Maximum * 0.5f;

        public override void Initialize(GameManager manager, PlayerController target, ProjectilePool pool,
            int phase, LayerMask floorMask, float overrideHealth = 0)
        {
            base.Initialize(manager, target, pool, phase, floorMask, overrideHealth);
            body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(AttackLoop());
        }

        private void Update()
        {
            if (!CanAct) return;
            visuals.Face(player.transform.position.x - transform.position.x);
            visuals.SetMotion(0, true, false, 0);
        }

        private IEnumerator AttackLoop()
        {
            yield return new WaitForSeconds(1.2f);
            while (IsAlive)
            {
                if (!CanAct) { yield return null; continue; }
                PatternName = "Ráfaga";
                int volleys = Enraged ? 5 : 3;
                for (int i = 0; i < volleys; i++)
                {
                    // A pause can occur in the frame a scaled wait expires. Wait for resume
                    // instead of ending the boss's only attack coroutine.
                    while (IsAlive && !CanAct) yield return null;
                    if (!IsAlive) yield break;
                    visuals.PlayAttack(1);
                    visuals.SetTelegraph(true);
                    yield return new WaitForSeconds(0.28f);
                    while (IsAlive && !CanAct) yield return null;
                    if (!IsAlive) yield break;
                    visuals.SetTelegraph(false);
                    Fan(weapon.MuzzlePosition, player.AimPosition - weapon.MuzzlePosition, 3, 18, bulletSpeed, Damage);
                    yield return new WaitForSeconds(0.23f);
                }
                yield return new WaitForSeconds(Enraged ? 0.65f : 1.15f);
                while (IsAlive && !CanAct) yield return null;
                if (!IsAlive) yield break;
                PatternName = "¡ESPECIAL! Salta la onda";
                visuals.SetTelegraph(true);
                visuals.PlayAttack(2);
                yield return new WaitForSeconds(specialWarning);
                while (IsAlive && !CanAct) yield return null;
                if (!IsAlive) yield break;
                visuals.SetTelegraph(false);
                // Ground waves can be jumped; the high fan discourages standing still.
                Vector2 groundOrigin = new Vector2(transform.position.x, game.GroundHeight(transform.position.x) + 0.28f);
                projectiles.Fire(groundOrigin, Vector2.left, Damage * 1.5f, Team.Enemy, 8, 5, true);
                projectiles.Fire(groundOrigin, Vector2.right, Damage * 1.5f, Team.Enemy, 8, 5, true);
                Fan(weapon.MuzzlePosition, player.AimPosition - weapon.MuzzlePosition,
                    Enraged ? 11 : 7, 110, bulletSpeed * 0.72f, Damage);
                PatternName = "Recuperación";
                yield return new WaitForSeconds(Enraged ? 1.1f : 1.8f);
            }
        }

        private void Fan(Vector2 origin, Vector2 aim, int bullets, float spread, float speed, float damage)
        {
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int i = 0; i < bullets; i++)
            {
                float shot = angle + Mathf.Lerp(-spread * 0.5f, spread * 0.5f, (float)i / (bullets - 1));
                Vector2 heading = new Vector2(Mathf.Cos(shot * Mathf.Deg2Rad), Mathf.Sin(shot * Mathf.Deg2Rad));
                projectiles.Fire(origin, heading, damage, Team.Enemy, speed, 5f);
            }
        }
    }
}
