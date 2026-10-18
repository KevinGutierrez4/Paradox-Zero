using System;
using System.Collections.Generic;

namespace OneMoreTime
{
    public enum StatKind { Damage, MoveSpeed, MaxHealth, FireRate }

    [Serializable]
    public sealed class StatBlock
    {
        public float maxHealth = 100f;
        public float moveSpeed = 7.5f;
        public float damage = 12f;
        // Shots per second. A larger value means faster shooting.
        public float fireRate = 7f;

        public StatBlock Copy() => new StatBlock
        { maxHealth = maxHealth, moveSpeed = moveSpeed, damage = damage, fireRate = fireRate };

        public void Clamp()
        {
            maxHealth = Safe(maxHealth, 1f, 10000f);
            moveSpeed = Safe(moveSpeed, 1f, 18f);
            damage = Safe(damage, 1f, 10000f);
            fireRate = Safe(fireRate, 0.25f, 25f);
        }

        private static float Safe(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return min;
            return Math.Max(min, Math.Min(max, value));
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

        public UpgradeDefinition(string id, string title, string description,
            StatKind stat, float multiplier, bool hasTradeoff = false,
            StatKind tradeoffStat = StatKind.MaxHealth, float tradeoffMultiplier = 1f)
        {
            Id = id; Title = title; Description = description;
            Stat = stat; Multiplier = multiplier; HasTradeoff = hasTradeoff;
            TradeoffStat = tradeoffStat; TradeoffMultiplier = tradeoffMultiplier;
        }

        public void Apply(StatBlock stats)
        {
            stats.Multiply(Stat, Multiplier);
            if (HasTradeoff) stats.Multiply(TradeoffStat, TradeoffMultiplier);
        }
    }

    /// <summary>Own RNG: combat randomness cannot change the upgrade sequence.</summary>
    public sealed class UpgradeDeck
    {
        private readonly Random random;
        public static readonly IReadOnlyList<UpgradeDefinition> Catalog = Array.AsReadOnly(new[]
        {
            new UpgradeDefinition("damage", "Munición temporal", "+20% de daño", StatKind.Damage, 1.20f),
            new UpgradeDefinition("speed", "Paso acelerado", "+15% de velocidad", StatKind.MoveSpeed, 1.15f),
            new UpgradeDefinition("health", "Blindaje reforzado", "+25% de vida máxima; recupera la vida añadida", StatKind.MaxHealth, 1.25f),
            new UpgradeDefinition("rate", "Gatillo veloz", "+20% de disparos por segundo", StatKind.FireRate, 1.20f),
            new UpgradeDefinition("glass", "Todo o nada", "+45% de daño / -15% de vida máxima", StatKind.Damage, 1.45f, true, StatKind.MaxHealth, 0.85f),
            new UpgradeDefinition("heavy", "Arsenal pesado", "+40% de cadencia / -10% de velocidad", StatKind.FireRate, 1.40f, true, StatKind.MoveSpeed, 0.90f)
        });

        public UpgradeDeck(int seed) { random = new Random(seed); }

        public UpgradeDefinition[] DrawThree()
        {
            var indices = new int[Catalog.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
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
