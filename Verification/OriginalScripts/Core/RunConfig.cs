using System;
using UnityEngine;

namespace OneMoreTime
{
    [Serializable]
    public sealed class PhaseSettings
    {
        [Min(24f)] public float length = 48f;
        [Min(1)] public int enemyBudget = 6;
        [Min(1)] public int maxAlive = 3;
        [Min(0.2f)] public float spawnInterval = 1.2f;
    }

    [CreateAssetMenu(menuName = "One More Time/Run Config")]
    public sealed class RunConfig : ScriptableObject
    {
        public StatBlock playerStats = new StatBlock();
        [Tooltip("0 = nueva semilla cada intento; cualquier otro valor = intento reproducible.")]
        public int fixedSeed;
        [Min(24f)] public float bossArenaLength = 32f;
        [Min(1f)] public float bossHealth = 850f;
        [Min(0f)] public float recoveryBetweenPhases = 8f;
        public PhaseSettings[] phases =
        {
            new PhaseSettings { length = 48, enemyBudget = 6, maxAlive = 3, spawnInterval = 1.3f },
            new PhaseSettings { length = 54, enemyBudget = 9, maxAlive = 4, spawnInterval = 1.1f },
            new PhaseSettings { length = 60, enemyBudget = 12, maxAlive = 5, spawnInterval = 0.95f },
            new PhaseSettings { length = 66, enemyBudget = 15, maxAlive = 6, spawnInterval = 0.8f },
            new PhaseSettings { length = 72, enemyBudget = 18, maxAlive = 7, spawnInterval = 0.65f }
        };

        public bool Validate(out string error)
        {
            if (playerStats == null || phases == null || phases.Length != RunProgression.NormalPhases)
            { error = "RunConfig necesita estadísticas y exactamente cinco fases normales."; return false; }
            foreach (var phase in phases)
                if (phase == null || phase.length < 24 || phase.enemyBudget < 1 || phase.maxAlive < 1 || phase.spawnInterval < 0.2f)
                { error = "Revisa longitud, presupuesto, máximo de enemigos e intervalo de las fases."; return false; }
            if (bossArenaLength < 24 || bossHealth <= 0)
            { error = "La arena y la vida del boss deben ser positivas."; return false; }
            error = string.Empty;
            return true;
        }
    }
}
