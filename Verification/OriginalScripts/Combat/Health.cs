using System;
using UnityEngine;

namespace OneMoreTime
{
    public enum Team { Player, Enemy, Environment }

    public readonly struct DamageInfo
    {
        public float Amount { get; }
        public Team Source { get; }
        public DamageInfo(float amount, Team source) { Amount = amount; Source = source; }
    }

    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float invulnerabilitySeconds = 0.4f;
        [SerializeField] private Team team = Team.Player;
        private float invulnerableUntil;
        public float Current { get; private set; }
        public float Maximum => maxHealth;
        public Team Team => team;
        public bool IsDead => Current <= 0f;
        public event Action<Health> Changed;
        public event Action<Health> Died;

        private void Awake() { Current = maxHealth; }

        public void ResetHealth(float maximum, Team ownerTeam)
        {
            team = ownerTeam;
            maxHealth = Mathf.Max(1f, maximum);
            Current = maxHealth;
            invulnerableUntil = 0;
            Changed?.Invoke(this);
        }

        public void SetMaximum(float maximum, bool healAddedCapacity)
        {
            float added = Mathf.Max(0, maximum - maxHealth);
            maxHealth = Mathf.Max(1f, maximum);
            Current = Mathf.Clamp(Current + (healAddedCapacity ? added : 0), 0, maxHealth);
            Changed?.Invoke(this);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(this);
        }

        public bool TakeDamage(DamageInfo hit)
        {
            if (IsDead || hit.Source == team || hit.Amount <= 0 || float.IsNaN(hit.Amount)
                || float.IsInfinity(hit.Amount) || Time.time < invulnerableUntil) return false;
            invulnerableUntil = Time.time + invulnerabilitySeconds;
            Current = Mathf.Max(0, Current - hit.Amount);
            Changed?.Invoke(this);
            if (IsDead) Died?.Invoke(this); // Once: later calls return at the first guard.
            return true;
        }

        public void Kill()
        {
            if (IsDead) return;
            Current = 0;
            Changed?.Invoke(this);
            Died?.Invoke(this);
        }
    }
}
