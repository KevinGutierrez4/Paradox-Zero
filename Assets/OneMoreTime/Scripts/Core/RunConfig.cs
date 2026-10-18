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
        [Tooltip("Vida recuperada por el jugador al matar un enemigo. 0 desactiva la curación.")]
        [Min(0f)] public float healOnKill = 5f;
        [Header("Mejora Robo de Vida (solo se obtiene eligiendo su carta)")]
        [Tooltip("Bonificación que añade cada nivel, en fracción de la vida máxima por golpe válido (0,002 = 0,2 %). " +
                 "El número de entradas es el nivel máximo. Valores por defecto: 0,2 % + 0,3 % + 0,4 % + 0,5 % + 0,6 % = 2,0 %.")]
        public float[] lifeStealBonusPerLevel = { 0.002f, 0.003f, 0.004f, 0.005f, 0.006f };
        [Tooltip("Debuff por nivel adquirido: puntos de daño recibido añadidos (0,01 = -1 % de defensa).")]
        [Range(0f, 0.05f)] public float lifeStealDefensePenaltyPerLevel = 0.01f;
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
            if (bossArenaLength < 24 || bossHealth <= 0 || healOnKill < 0 || float.IsNaN(healOnKill) || float.IsInfinity(healOnKill))
            { error = "Revisa la arena, vida del boss y curación por baja (valor positivo o cero)."; return false; }
            error = string.Empty;
            return true;
        }
    }
}
