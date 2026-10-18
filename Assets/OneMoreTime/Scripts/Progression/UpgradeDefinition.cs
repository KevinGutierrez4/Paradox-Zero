using System;
using System.Collections.Generic;

namespace OneMoreTime
{
    public enum StatKind { Damage, MoveSpeed, MaxHealth, FireRate, DamageTaken, KillHealing }

    [Serializable]
    public sealed class StatBlock
    {
        public float maxHealth = 100f;
        public float moveSpeed = 7.5f;
        public float damage = 12f;
        // Attacks per second; scales windup, impact window and recovery together.
        public float fireRate = 1.6f;
        public float damageTaken = 1f;
        public float killHealing = 5f;

        public StatBlock Copy() => new StatBlock
        { maxHealth = maxHealth, moveSpeed = moveSpeed, damage = damage, fireRate = fireRate, damageTaken = damageTaken, killHealing = killHealing };

        public void Clamp()
        {
            maxHealth = Safe(maxHealth, 1f, 10000f);
            moveSpeed = Safe(moveSpeed, 1f, 18f);
            damage = Safe(damage, 1f, 10000f);
            fireRate = Safe(fireRate, 0.5f, 4f);
            damageTaken = Safe(damageTaken, 0.35f, 1f);
            killHealing = Safe(killHealing, 0f, 30f);
        }

        private static float Safe(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return min;
            return Math.Max(min, Math.Min(max, value));
        }

        public float Get(StatKind kind)
        {
            switch (kind)
            {
                case StatKind.Damage: return damage;
                case StatKind.MoveSpeed: return moveSpeed;
                case StatKind.MaxHealth: return maxHealth;
                case StatKind.FireRate: return fireRate;
                case StatKind.DamageTaken: return damageTaken;
                case StatKind.KillHealing: return killHealing;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public void Add(StatKind kind, float amount)
        {
            if (kind != StatKind.KillHealing) throw new ArgumentOutOfRangeException(nameof(kind));
            killHealing += amount; Clamp();
        }

        public void Multiply(StatKind kind, float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            switch (kind)
            {
                case StatKind.Damage: damage *= multiplier; break;
                case StatKind.MoveSpeed: moveSpeed *= multiplier; break;
                case StatKind.MaxHealth: maxHealth *= multiplier; break;
                case StatKind.FireRate: fireRate *= multiplier; break;
                case StatKind.DamageTaken: damageTaken *= multiplier; break;
                case StatKind.KillHealing: killHealing *= multiplier; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            Clamp();
        }
    }

    public sealed class UpgradeDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public StatKind Stat { get; }
        public float Multiplier { get; }
        public bool HasTradeoff { get; }
        public StatKind TradeoffStat { get; }
        public float TradeoffMultiplier { get; }
        public float FlatBonus { get; }

        public UpgradeDefinition(string id, string title, string description,
            StatKind stat, float multiplier, bool hasTradeoff = false,
            StatKind tradeoffStat = StatKind.MaxHealth, float tradeoffMultiplier = 1f, float flatBonus = 0)
        {
            Id = id; Title = title; Description = description;
            Stat = stat; Multiplier = multiplier; HasTradeoff = hasTradeoff;
            TradeoffStat = tradeoffStat; TradeoffMultiplier = tradeoffMultiplier; FlatBonus = flatBonus;
        }

        public bool CanApply(StatBlock current)
        {
            var next = current.Copy(); Apply(next);
            return Math.Abs(next.Get(Stat) - current.Get(Stat)) > 0.0001f;
        }
        public string Describe(StatBlock current)
        {
            if (current == null) return Description;
            var next = current.Copy(); Apply(next);
            string text = Description + "\n\n" + Change(Stat, current, next);
            if (HasTradeoff) text += "\n" + Change(TradeoffStat, current, next);
            return text;
        }
        private static string Change(StatKind stat, StatBlock before, StatBlock after)
        {
            string label = stat == StatKind.Damage ? "Daño/golpe" : stat == StatKind.MoveSpeed ? "Movimiento" :
                stat == StatKind.MaxHealth ? "Vida máxima" : stat == StatKind.FireRate ? "Ataques/s" :
                stat == StatKind.KillHealing ? "Vida/baja" : "Daño recibido";
            float a = before.Get(stat), b = after.Get(stat);
            if (stat == StatKind.DamageTaken) return $"{label}: {a * 100:0.#}% → {b * 100:0.#}%";
            return $"{label}: {a:0.#} → {b:0.#}";
        }

        public void Apply(StatBlock stats)
        {
            if (FlatBonus != 0) stats.Add(Stat, FlatBonus);
            else stats.Multiply(Stat, Multiplier);
            if (HasTradeoff) stats.Multiply(TradeoffStat, TradeoffMultiplier);
        }
    }

    /// <summary>Own RNG: combat randomness cannot change the upgrade sequence.</summary>
    public sealed class UpgradeDeck
    {
        private readonly Random random;
        public static readonly IReadOnlyList<UpgradeDefinition> Catalog = Array.AsReadOnly(new[]
        {
            new UpgradeDefinition("damage", "Filo templado", "+20% de daño por golpe.", StatKind.Damage, 1.20f),
            new UpgradeDefinition("speed", "Paso ligero", "+12% de velocidad de movimiento.", StatKind.MoveSpeed, 1.12f),
            new UpgradeDefinition("health", "Vitalidad", "+20% de vida máxima. Recuperas la capacidad añadida.", StatKind.MaxHealth, 1.20f),
            new UpgradeDefinition("rate", "Técnica del hacha", "+15% de ataques por segundo. Golpes más rápidos.", StatKind.FireRate, 1.15f),
            new UpgradeDefinition("stone", "Piel de piedra", "Recibes 15% menos daño, pero te mueves 10% más lento.", StatKind.DamageTaken, 0.85f, true, StatKind.MoveSpeed, 0.90f),
            new UpgradeDefinition("vampire", "Pacto vampírico", "+2 de vida por baja, pero pierdes 10% de vida máxima.", StatKind.KillHealing, 1f, true, StatKind.MaxHealth, 0.90f, 2f)
        });

        public UpgradeDeck(int seed) { random = new Random(seed); }

        public UpgradeDefinition[] DrawThree(StatBlock current = null)
        {
            var candidates = new List<int>();
            for (int i = 0; i < Catalog.Count; i++) if (current == null || Catalog[i].CanApply(current)) candidates.Add(i);
            // A normal five-offer run always has at least three useful stats. Keep custom/debug
            // configurations bounded even if every stat starts at its absolute cap.
            if (candidates.Count < 3)
                for (int i = 0; i < Catalog.Count; i++) if (!candidates.Contains(i)) candidates.Add(i);
            var indices = candidates.ToArray();
            // Partial Fisher-Yates: three distinct options, repeatable given a seed.
            var result = new UpgradeDefinition[3];
            for (int i = 0; i < result.Length; i++)
            {
                int j = random.Next(i, indices.Length);
                int temp = indices[i]; indices[i] = indices[j]; indices[j] = temp;
                result[i] = Catalog[indices[i]];
            }
            return result;
        }
    }
}
