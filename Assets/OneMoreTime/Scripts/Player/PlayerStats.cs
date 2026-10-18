using System;
using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        private Health health;
        private UpgradeFeedback feedback;
        public StatBlock Current { get; private set; } = new StatBlock();
        public event Action Changed;
        public event Action<UpgradeDefinition> UpgradeApplied;

        private void Awake()
        {
            health = GetComponent<Health>();
            feedback = GetComponent<UpgradeFeedback>();
            if (feedback == null) feedback = gameObject.AddComponent<UpgradeFeedback>();
        }

        public void ResetStats(StatBlock initial)
        {
            if (health == null) health = GetComponent<Health>();
            Current = initial.Copy();
            Current.Clamp();
            health.ResetHealth(Current.maxHealth, Team.Player);
            health.DamageTakenMultiplier = Current.damageTaken;
            if (feedback != null) feedback.Cancel();
            Changed?.Invoke();
        }

        public void ApplyUpgrade(UpgradeDefinition upgrade)
        {
            if (upgrade == null || health.IsDead) return;
            upgrade.Apply(Current);
            health.SetMaximum(Current.maxHealth, true);
            health.DamageTakenMultiplier = Current.damageTaken;
            Changed?.Invoke();
            feedback?.Play(upgrade);
            UpgradeApplied?.Invoke(upgrade);
        }
    }
}
