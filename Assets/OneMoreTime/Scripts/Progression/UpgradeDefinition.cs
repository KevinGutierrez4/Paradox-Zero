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
            damageTaken = Safe(damageTaken, 0.35f, 1.5f);
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

        /// <summary>Minimum values a debuff may leave: a card can never make the hero unplayable.</summary>
        public static bool Playable(StatBlock s)
            => s.moveSpeed >= 5.2f && s.maxHealth >= 55f && s.fireRate >= 0.95f && s.damage >= 7f && s.damageTaken <= 1.35f;

        public bool CanApply(StatBlock current)
        {
            var next = current.Copy(); Apply(next);
            return Math.Abs(next.Get(Stat) - current.Get(Stat)) > 0.0001f && Playable(next);
        }

        public const string BuffColor = "#7CFFB2", DebuffColor = "#FF8A7A";

        public string BuffLine(StatBlock current)
        {
            if (current == null) return "";
            var next = current.Copy(); Apply(next);
            return $"<color={BuffColor}>[+] BUFF · {Change(Stat, current, next)}</color>";
        }

        public string DebuffLine(StatBlock current)
        {
            if (current == null || !HasTradeoff) return "";
            var next = current.Copy(); Apply(next);
            return $"<color={DebuffColor}>[-] DEBUFF · {Change(TradeoffStat, current, next)}</color>";
        }

        public string Describe(StatBlock current)
        {
            if (current == null) return Description;
            string text = Description + "\n\n" + BuffLine(current);
            if (HasTradeoff) text += "\n" + DebuffLine(current);
            return text;
        }

        public static string Label(StatKind stat) => stat == StatKind.Damage ? "Daño/golpe" : stat == StatKind.MoveSpeed ? "Movimiento" :
                stat == StatKind.MaxHealth ? "Vida máxima" : stat == StatKind.FireRate ? "Ataques/s" :
                stat == StatKind.KillHealing ? "Vida/baja" : "Daño recibido";

        private static string Change(StatKind stat, StatBlock before, StatBlock after)
        {
            string label = Label(stat);
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
        // Every card has a real BUFF and a real DEBUFF; values are tuned so no combination becomes unplayable.
        public static readonly IReadOnlyList<UpgradeDefinition> Catalog = Array.AsReadOnly(new[]
        {
            new UpgradeDefinition("berserker", "Furia del Berserker", "La furia guía el hacha: golpeas más fuerte, pero bajas la guardia.", StatKind.Damage, 1.25f, true, StatKind.DamageTaken, 1.12f),
            new UpgradeDefinition("midgard", "Resistencia de Midgard", "Aguantas como la tierra misma, aunque te vuelves más pesado.", StatKind.MaxHealth, 1.25f, true, StatKind.MoveSpeed, 0.92f),
            new UpgradeDefinition("devastador", "Golpe Devastador", "Cada tajo parte escudos, pero el hacha tarda más en volver.", StatKind.Damage, 1.40f, true, StatKind.FireRate, 0.85f),
            new UpgradeDefinition("guerrero", "Paso del Guerrero", "Corres como el viento del norte, con menos reservas de vida.", StatKind.MoveSpeed, 1.15f, true, StatKind.MaxHealth, 0.92f),
            new UpgradeDefinition("hacha", "Técnica del hacha", "Golpes más rápidos y ligeros: más ataques, algo menos de daño.", StatKind.FireRate, 1.18f, true, StatKind.Damage, 0.92f),
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
            if (candidates.Count < 3 && current != null)
                for (int i = 0; i < Catalog.Count && candidates.Count < 3; i++)
                {
                    if (candidates.Contains(i)) continue;
                    var next = current.Copy(); Catalog[i].Apply(next);
                    if (UpgradeDefinition.Playable(next)) candidates.Add(i);
                }
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
