using System;
using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        private Health health;
        public StatBlock Current { get; private set; } = new StatBlock();
        public event Action Changed;

        private void Awake() { health = GetComponent<Health>(); }

        public void ResetStats(StatBlock initial)
        {
            if (health == null) health = GetComponent<Health>();
            Current = initial.Copy();
            Current.Clamp();
            health.ResetHealth(Current.maxHealth, Team.Player);
            Changed?.Invoke();
        }

        public void ApplyUpgrade(UpgradeDefinition upgrade)
        {
            if (upgrade == null || health.IsDead) return;
            upgrade.Apply(Current);
            health.SetMaximum(Current.maxHealth, true);
            Changed?.Invoke();
        }
    }
}
