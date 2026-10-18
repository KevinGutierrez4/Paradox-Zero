from pathlib import Path
p=Path('Assets/OneMoreTime/Scripts/Progression/UpgradeDefinition.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('MaxHealth, FireRate }','MaxHealth, FireRate, DamageTaken, KillHealing }')
s=s.replace('public float fireRate = 1.6f;','public float fireRate = 1.6f;\n        public float damageTaken = 1f;\n        public float killHealing = 5f;')
s=s.replace('damage = damage, fireRate = fireRate };','damage = damage, fireRate = fireRate, damageTaken = damageTaken, killHealing = killHealing };')
s=s.replace('fireRate = Safe(fireRate, 0.5f, 4f);','fireRate = Safe(fireRate, 0.5f, 4f);\n            damageTaken = Safe(damageTaken, 0.35f, 1f);\n            killHealing = Safe(killHealing, 0f, 30f);')
s=s.replace('case StatKind.FireRate: fireRate *= multiplier; break;','case StatKind.FireRate: fireRate *= multiplier; break;\n                case StatKind.DamageTaken: damageTaken *= multiplier; break;\n                case StatKind.KillHealing: killHealing *= multiplier; break;')
a=s.index('        public void Multiply(StatKind kind, float multiplier)')
s=s[:a]+'''        public float Get(StatKind kind)
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

'''+s[a:]
s=s.replace('public float TradeoffMultiplier { get; }','public float TradeoffMultiplier { get; }\n        public float FlatBonus { get; }')
s=s.replace('StatKind tradeoffStat = StatKind.MaxHealth, float tradeoffMultiplier = 1f)','StatKind tradeoffStat = StatKind.MaxHealth, float tradeoffMultiplier = 1f, float flatBonus = 0)')
s=s.replace('TradeoffStat = tradeoffStat; TradeoffMultiplier = tradeoffMultiplier;','TradeoffStat = tradeoffStat; TradeoffMultiplier = tradeoffMultiplier; FlatBonus = flatBonus;')
s=s.replace('stats.Multiply(Stat, Multiplier);','if (FlatBonus != 0) stats.Add(Stat, FlatBonus);\n            else stats.Multiply(Stat, Multiplier);')
needle='''        public void Apply(StatBlock stats)'''
s=s.replace(needle,'''        public bool CanApply(StatBlock current)
        {
            var next = current.Copy(); Apply(next);
            return Math.Abs(next.Get(Stat) - current.Get(Stat)) > 0.0001f;
        }
        public string Describe(StatBlock current)
        {
            if (current == null) return Description;
            var next = current.Copy(); Apply(next);
            string text = Description + "\\n\\n" + Change(Stat, current, next);
            if (HasTradeoff) text += "\\n" + Change(TradeoffStat, current, next);
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

'''+needle)
a=s.index('            new UpgradeDefinition("damage"');b=s.index('\n        });',a)
s=s[:a]+'''            new UpgradeDefinition("damage", "Filo templado", "+20% de daño por golpe.", StatKind.Damage, 1.20f),
            new UpgradeDefinition("speed", "Paso ligero", "+12% de velocidad de movimiento.", StatKind.MoveSpeed, 1.12f),
            new UpgradeDefinition("health", "Vitalidad", "+20% de vida máxima. Recuperas la capacidad añadida.", StatKind.MaxHealth, 1.20f),
            new UpgradeDefinition("rate", "Técnica del hacha", "+15% de ataques por segundo. Golpes más rápidos.", StatKind.FireRate, 1.15f),
            new UpgradeDefinition("stone", "Piel de piedra", "Recibes 15% menos daño, pero te mueves 10% más lento.", StatKind.DamageTaken, 0.85f, true, StatKind.MoveSpeed, 0.90f),
            new UpgradeDefinition("vampire", "Pacto vampírico", "+2 de vida por baja, pero pierdes 10% de vida máxima.", StatKind.KillHealing, 1f, true, StatKind.MaxHealth, 0.90f, 2f)'''+s[b:]
s=s.replace('public UpgradeDefinition[] DrawThree()','public UpgradeDefinition[] DrawThree(StatBlock current = null)')
s=s.replace('''            var indices = new int[Catalog.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;''','''            var candidates = new List<int>();
            for (int i = 0; i < Catalog.Count; i++) if (current == null || Catalog[i].CanApply(current)) candidates.Add(i);
            // A normal five-offer run always has at least three useful stats. Keep custom/debug
            // configurations bounded even if every stat starts at its absolute cap.
            if (candidates.Count < 3)
                for (int i = 0; i < Catalog.Count; i++) if (!candidates.Contains(i)) candidates.Add(i);
            var indices = candidates.ToArray();''')
p.write_text(s,encoding='utf-8')
