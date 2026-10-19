using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private EnemyAI[] enemyPrefabs;
        [SerializeField] private BossAI bossPrefab;
        [SerializeField] private LayerMask groundMask;
        [SerializeField, Min(2f)] private float minimumAhead = 10f;
        [SerializeField, Min(2f)] private float maximumAhead = 16f;
        private readonly HashSet<EnemyActor> alive = new HashSet<EnemyActor>();
        private readonly HashSet<EnemyActor> owned = new HashSet<EnemyActor>();
        private readonly List<EnemyActor> scratch = new List<EnemyActor>();
        private GameManager game;
        private PlayerController player;
        private ProjectilePool pool;
        private System.Random random;
        private PhaseSettings settings;
        private int spawned, phase;
        private float nextSpawn;
        private bool active, bossPhase;
        public int AliveCount => alive.Count;
        public int SpawnedCount => spawned;
        public int Budget => bossPhase ? 1 : settings != null ? settings.enemyBudget : 0;
        public bool IsCleared => active && spawned >= Budget && alive.Count == 0;
        public BossAI CurrentBoss { get; private set; }
        public bool IsConfigured => enemyPrefabs != null && enemyPrefabs.Length > 0
            && Array.TrueForAll(enemyPrefabs, enemy => enemy != null) && bossPrefab != null && groundMask.value != 0;

        public void Initialize(GameManager manager, PlayerController target, ProjectilePool projectiles, int seed)
        { game = manager; player = target; pool = projectiles; random = new System.Random(seed); }

        public void BeginPhase(int phaseNumber, PhaseSettings phaseSettings)
        {
            Clear();
            phase = phaseNumber; settings = phaseSettings; bossPhase = false;
            spawned = 0; active = true; nextSpawn = Time.time + 0.6f;
        }

        public void BeginBoss(float bossHealth)
        {
            Clear(); bossPhase = true; active = true; spawned = 0;
            Vector2 point = GroundPosition(game.PhaseEnd - 7f);
            CurrentBoss = Instantiate(bossPrefab, point, Quaternion.identity, transform);
            CurrentBoss.Initialize(game, player, pool, RunProgression.TotalPhases, groundMask, bossHealth);
            Track(CurrentBoss);
            spawned = 1;
        }

        private void Update()
        {
            if (game != null && game.Tutorial != null && game.Tutorial.Active) { nextSpawn = Time.time + 0.8f; return; }
            if (!active || bossPhase || game == null || !game.IsPlaying || Time.time < nextSpawn
                || spawned >= settings.enemyBudget || alive.Count >= settings.maxAlive) return;
            // Progress unlocks more of the fixed budget; all are unlocked near the exit.
            float progress = Mathf.InverseLerp(game.PhaseStart, game.PhaseEnd - 6f, player.transform.position.x);
            int unlocked = Mathf.Clamp(Mathf.CeilToInt((progress + 0.18f) * settings.enemyBudget), 1, settings.enemyBudget);
            if (spawned >= unlocked) return;
            float ahead = Mathf.Lerp(minimumAhead, Mathf.Max(minimumAhead, maximumAhead), (float)random.NextDouble());
            float x = Mathf.Clamp(player.transform.position.x + ahead, game.PhaseStart + 5f, game.PhaseEnd - 2f);
            // Near an arena's end there may be no space ahead; never spawn overlapping the player.
            if (Mathf.Abs(x - player.transform.position.x) < 3.5f)
                x = Mathf.Clamp(player.transform.position.x - 7f, game.PhaseStart + 3f, game.PhaseEnd - 3f);
            var prefab = enemyPrefabs[random.Next(enemyPrefabs.Length)];
            // Never inside a cover or a trap.
            if (game.Stage != null) x = game.Stage.FreeX(x, game.PhaseStart + 3f, game.PhaseEnd - 2f);
            Vector2 point = GroundPosition(x);
            ArcherPost post = null;
            var ai = prefab.GetComponent<EnemyAI>();
            if (ai != null && ai.name.Contains("Archer"))
            {
                // Archers take a free elevated post ahead of the player when there is one.
                foreach (var candidate in ArcherPost.All)
                {
                    if (!candidate.Free) continue;
                    float cx = candidate.CenterX;
                    if (cx < game.PhaseStart || cx > game.PhaseEnd || cx < player.transform.position.x + 4f) continue;
                    post = candidate; point = new Vector2(cx, candidate.TopY + 0.05f); break;
                }
            }
            var enemy = Instantiate(prefab, point, Quaternion.identity, transform);
            enemy.Initialize(game, player, pool, phase, groundMask);
            if (post != null && enemy is EnemyAI archer) archer.AnchorTo(post);
            Track(enemy); spawned++;
            nextSpawn = Time.time + settings.spawnInterval * Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble());
        }

        private Vector2 GroundPosition(float x)
        {
            if (game.Surface != null) return new Vector2(x, game.GroundHeight(x) + 0.04f);
            var hit = Physics2D.Raycast(new Vector2(x, game.FloorY + 8f), Vector2.down, 24, groundMask);
            if (hit.collider == null)
                throw new InvalidOperationException("No hay suelo bajo el punto de spawn. Revisa GroundMask y colliders 2D.");
            return hit.point + Vector2.up * 0.04f;
        }

        private void Track(EnemyActor actor)
        {
            alive.Add(actor); owned.Add(actor);
            actor.Defeated += HandleDefeated; actor.Removed += HandleRemoved;
        }

        private void HandleDefeated(EnemyActor actor)
        {
            if (!alive.Remove(actor)) return;
            game.RegisterKill(actor is BossAI ? 1000 : 100, actor.Health.LastDamageSource == Team.Player);
        }

        private void HandleRemoved(EnemyActor actor)
        { alive.Remove(actor); owned.Remove(actor); }

        public void Clear()
        {
            active = false;
            scratch.Clear(); scratch.AddRange(owned);
            foreach (var actor in scratch)
            {
                if (actor == null) continue;
                actor.Defeated -= HandleDefeated; actor.Removed -= HandleRemoved;
                if (actor is BossAI boss) boss.Stun?.Close();
                actor.gameObject.SetActive(false);
                Destroy(actor.gameObject);
            }
            alive.Clear(); owned.Clear(); scratch.Clear(); CurrentBoss = null;
        }
    }
}
