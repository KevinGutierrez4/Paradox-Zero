using UnityEngine;

namespace OneMoreTime
{
    /// <summary>
    /// Robo de Vida: the only place where life is recovered by hitting. It does nothing until the player picks the
    /// "Robo de Vida" card (StatBlock.lifeStealLevel 0 = off). The percentage is read from the player's current stats
    /// (cumulative per level: 0,2 / 0,5 / 0,9 / 1,4 / 2,0 % by default) and applied to the current maximum health,
    /// so vitality upgrades count. One heal per attack at most: the axe's swing serial guards against repeated
    /// contacts, several targets or several hurtboxes of Jörmungandr in the same swing. Only hits that really dealt
    /// damage count (MeleeWeapon raises HitConfirmed only when Health.TakeDamage succeeded, so dead, invulnerable,
    /// blocked or missed hits never heal), only against enemies, and never above the maximum.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(MeleeWeapon))]
    [DisallowMultipleComponent]
    public sealed class PlayerLifeSteal : MonoBehaviour
    {
        private Health health;
        private MeleeWeapon weapon;
        private PlayerStats stats;
        private int lastSerial = -1;

        /// <summary>Current life-steal level (0 = not acquired).</summary>
        public int Level => stats != null ? stats.Current.lifeStealLevel : 0;
        /// <summary>Fraction of the maximum health recovered per valid hit (0 when not acquired).</summary>
        public float Fraction => Level > 0 ? stats.Current.lifeSteal : 0f;
        public int HealsApplied { get; private set; }
        public float TotalHealed { get; private set; }
        public float LastHeal { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>(); weapon = GetComponent<MeleeWeapon>(); stats = GetComponent<PlayerStats>();
            weapon.HitConfirmed += OnHit;
        }

        private void OnDestroy() { if (weapon != null) weapon.HitConfirmed -= OnHit; }

        public void ResetRun() { lastSerial = -1; HealsApplied = 0; TotalHealed = 0; LastHeal = 0; }

        private void OnHit(Health target, int swingSerial)
        {
            LastHeal = 0;
            float fraction = Fraction;
            if (fraction <= 0f) return; // not acquired: hitting never heals
            if (swingSerial == lastSerial || health == null || health.IsDead || target == null) return;
            var enemy = target.GetComponentInParent<EnemyActor>();
            if (enemy == null || target.Team != Team.Enemy) return; // walls, decorative or non-enemy objects never heal
            lastSerial = swingSerial; // one recovery per attack, whatever it touched
            float before = health.Current;
            health.Heal(health.Maximum * fraction);
            LastHeal = health.Current - before;
            if (LastHeal <= 0) return;
            HealsApplied++; TotalHealed += LastHeal;
            // Discreet green cue: a few rising specks at the chest.
            Vector2 chest = (Vector2)transform.position + Vector2.up * 1.15f;
            for (int i = 0; i < 4; i++)
                PixelFx.Emit(chest + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.3f)), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.9f, 1.5f)),
                    new Color(0.45f, 1f, 0.55f, 0.95f), 0.55f, -0.5f, 0.08f, 46);
        }
    }
}
