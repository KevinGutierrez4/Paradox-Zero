using System;
using UnityEngine;

namespace OneMoreTime
{
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        private const string BestScoreKey = "OMT.BestScore.v1";
        [SerializeField] private RunConfig config;
        [SerializeField] private PlayerController player;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private UpgradeManager upgrades;
        [SerializeField] private ProjectilePool projectiles;
        [SerializeField] private UIManager ui;
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private BackgroundDirector backgrounds;
        [SerializeField] private Transform phaseExitMarker;
        [SerializeField] private float floorY;
        private readonly RunProgression progression = new RunProgression();
        private PortalTransitionController portals;
        private StageBuilder stage;
        private TutorialController tutorial;
        private bool ready;
        // Automated acceptance runs disable persistence while still exercising record calculation.
        private bool persistRecords = true;
        private float phaseTime;
        public RunState State => progression.State;
        public bool IsPlaying => ready && State == RunState.Playing;
        public int Phase => progression.Phase;
        public int Seed { get; private set; }
        public int Score { get; private set; }
        public int BestScore { get; private set; }
        public int Kills { get; private set; }
        public float RunTime { get; private set; }
        public float PhaseStart { get; private set; }
        public float PhaseEnd { get; private set; }
        public float FloorY => floorY;
        public SerpentSurface Surface => backgrounds != null ? backgrounds.Surface : null;
        public float GroundHeight(float x) => Surface != null ? Surface.HeightAt(x) : floorY;
        public PlayerController Player => player;
        public EnemySpawner Spawner => spawner;
        public CameraFollow2D CameraRig => cameraFollow;
        public PortalTransitionController Portals => portals;
        public StageBuilder Stage => stage;
        public TutorialController Tutorial => tutorial;
        public UIManager UI => ui;
        public bool IsBossPhase => progression.IsBossPhase;
        /// <summary>Fimbulvetr: phases 1-2 first winter, 3-4 second winter, 5 and the boss the third.</summary>
        public int Winter => Phase <= 2 ? 1 : Phase <= 4 ? 2 : 3;
        public string WinterName => Winter == 1 ? "Primer invierno" : Winter == 2 ? "Segundo invierno" : "Tercer invierno · Ragnarök";
        /// <summary>The exit may only be used while playing a normal phase whose enemies are all defeated.</summary>
        public bool CanLeaveThroughPortal => IsPlaying && !progression.IsBossPhase && spawner.IsCleared && !player.Health.IsDead;
        public event Action<RunState> StateChanged;

        private void Start()
        {
            if (config == null || player == null || spawner == null || upgrades == null || projectiles == null || ui == null)
            { Fail("Faltan referencias en GameManager. Ejecuta el generador o revisa el Inspector."); return; }
            if (!config.Validate(out string error)) { Fail(error); return; }
            if (!spawner.IsConfigured || !projectiles.IsConfigured)
            { Fail("Configura los prefabs de enemigos/boss/proyectil y GroundMask."); return; }
#if !ENABLE_LEGACY_INPUT_MANAGER
            Fail("Activa Edit > Project Settings > Player > Active Input Handling = Input Manager (Old) o Both y reinicia el editor.");
            return;
#endif
            var anyRenderer = player.GetComponentInChildren<SpriteRenderer>();
            if (anyRenderer != null) PixelFx.Initialize(anyRenderer.sharedMaterial);
            portals = new GameObject("PortalTransition").AddComponent<PortalTransitionController>();
            portals.transform.SetParent(transform, false);
            stage = new GameObject("StageBuilder").AddComponent<StageBuilder>();
            stage.transform.SetParent(transform, false);
            stage.Initialize(this, anyRenderer != null ? anyRenderer.sharedMaterial : null);
            tutorial = new GameObject("Tutorial").AddComponent<TutorialController>();
            tutorial.transform.SetParent(transform, false);
            projectiles.Initialize(this); player.Initialize(this, projectiles);
            portals.Initialize(this, player);
            LifeStealProgression.Configure(config.lifeStealBonusPerLevel, config.lifeStealDefensePenaltyPerLevel);
            player.Stats.ResetStats(InitialStats());
            player.Health.Died += OnPlayerDied;
            upgrades.Offered += ui.ShowUpgradeCards;
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            ready = true;
            ui.Initialize(this);
            tutorial.Initialize(this, player);
            cameraFollow?.Initialize(player.transform);
            PublishState();
        }

        private void Fail(string message) { Debug.LogError(message, this); enabled = false; }

        public void StartNewRun()
        {
            if (!ready || (State != RunState.MainMenu && State != RunState.GameOver && State != RunState.Victory)) return;
            projectiles.ClearActive(); spawner.Clear(); PixelFx.ClearAll();
            cameraFollow?.ResetFraming();
            Seed = config.fixedSeed == 0 ? Guid.NewGuid().GetHashCode() : config.fixedSeed;
            Score = Kills = 0; RunTime = phaseTime = 0;
            LifeStealProgression.Configure(config.lifeStealBonusPerLevel, config.lifeStealDefensePenaltyPerLevel);
            upgrades.ResetRun(unchecked(Seed ^ 0x51F15E));
            spawner.Initialize(this, player, projectiles, Seed);
            player.ResetForRun(InitialStats());
            progression.StartRun();
            BeginPhase(0);
            PublishState();
        }

        private void Update()
        {
            if (!ready) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
#endif
            if (!IsPlaying) return;
            RunTime += Time.deltaTime; phaseTime += Time.deltaTime;
            if (progression.IsBossPhase)
            {
                if (spawner.IsCleared) FinishPhase();
            }
            else if (spawner.IsCleared)
            {
                // Objectives complete: the rune portal opens at the end of the stretch; entering it finishes the phase.
                float x = PhaseEnd - 2.2f;
                portals.OpenExit(new Vector2(x, GroundHeight(x)), Winter);
            }
        }

        private void BeginPhase(float startX)
        {
            PhaseStart = startX;
            PhaseEnd = startX + (progression.IsBossPhase ? config.bossArenaLength : config.phases[Phase - 1].length);
            backgrounds?.SetPhase(Phase, PhaseStart, PhaseEnd);
            floorY = Surface != null ? Surface.MinimumY : floorY;
            player.SetPhaseBounds(PhaseStart, PhaseEnd, GroundHeight(PhaseStart + 2f));
            player.SetControlLocked(false);
            player.SuppressUntilRelease();
            phaseTime = 0;
            PixelFx.ClearAll();
            cameraFollow?.ResetFraming();
            var surfaceArt = Surface != null ? Surface.GetComponent<SpriteRenderer>() : null;
            if (surfaceArt != null) cameraFollow?.SetVerticalRange(surfaceArt.bounds.min.y + 0.05f, surfaceArt.bounds.max.y - 0.05f);
            cameraFollow?.SetBounds(PhaseStart, PhaseEnd, true);
            // The rune portal replaces the old exit bar; the marker object is kept (scene reference) but hidden.
            if (phaseExitMarker != null) phaseExitMarker.gameObject.SetActive(false);
            portals.OnPhaseBegun(new Vector2(PhaseStart + 2f, GroundHeight(PhaseStart + 2f)), Winter);
            stage?.Build(Phase, PhaseStart, PhaseEnd, progression.IsBossPhase);
            tutorial?.OnPhaseBegun(progression.IsBossPhase ? 6 : Phase);
            // Boss setup starts a coroutine. State is already Playing before it runs.
            if (progression.IsBossPhase) spawner.BeginBoss(config.bossHealth);
            else spawner.BeginPhase(Phase, config.phases[Phase - 1]);
        }

        private void FinishPhase()
        {
            if (!progression.CompletePhase()) return;
            PixelFx.ClearAll();
            Score += 250 + Mathf.Max(0, 1000 - Mathf.FloorToInt(phaseTime * 20f));
            projectiles.ClearActive();
            if (State == RunState.Victory) SaveRecord();
            PublishState();
            if (State == RunState.ChoosingUpgrade) upgrades.Offer(player.Stats.Current);
        }

        /// <summary>Called by the exit portal after its travel animation. Re-validates the objectives.</summary>
        public bool CompletePhaseThroughPortal()
        {
            if (!CanLeaveThroughPortal) return false;
            FinishPhase();
            return State == RunState.ChoosingUpgrade;
        }

        public void SetPlayerControlLocked(bool locked) { if (player != null) player.SetControlLocked(locked); }

        public void SelectUpgrade(int index)
        {
            if (!ready || State != RunState.ChoosingUpgrade || !upgrades.TryConsume(index, out var choice)) return;
            player.Stats.ApplyUpgrade(choice);
            player.Health.Heal(config.recoveryBetweenPhases);
            if (!progression.ChooseUpgrade()) return;
            BeginPhase(PhaseEnd + 6f);
            PublishState();
        }

        public void TogglePause()
        {
            if (!ready || !progression.TogglePause()) return;
            player.SuppressUntilRelease();
            PublishState();
        }

        public void ReturnToMenu()
        {
            if (!ready) return;
            SaveRecord();
            upgrades.Cancel(); projectiles.ClearActive(); spawner.Clear();
            portals?.ResetAll(); stage?.Clear(); tutorial?.OnPhaseBegun(0); PixelFx.ClearAll(); cameraFollow?.ResetFraming();
            player.SetControlLocked(false);
            progression.ReturnToMenu(); PublishState();
        }

        private void OnPlayerDied(Health ignored)
        {
            if (!progression.Die()) return;
            player.PlayDeath(); upgrades.Cancel(); projectiles.ClearActive();
            SaveRecord(); PublishState();
        }

        public void RegisterKill(int points, bool byPlayer = true)
        {
            if (!IsPlaying) return;
            Score += points; Kills++;
            if (byPlayer && !player.Health.IsDead) player.Health.Heal(player.Stats.Current.killHealing);
        }

        private StatBlock InitialStats()
        {
            var initial = config.playerStats.Copy(); initial.killHealing = config.healOnKill;
            return initial;
        }

        private void SaveRecord()
        {
            if (Score <= BestScore) return;
            BestScore = Score;
            if (!persistRecords) return;
            PlayerPrefs.SetInt(BestScoreKey, BestScore); PlayerPrefs.Save();
        }

        private void PublishState()
        {
            Time.timeScale = State == RunState.Playing ? 1f : 0f;
            StateChanged?.Invoke(State);
        }

        /// <summary>Automated runs keep playing when another window takes the focus.</summary>
        public static bool SuppressFocusPause;

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && ready && IsPlaying && !SuppressFocusPause) TogglePause();
        }

        private void OnDestroy()
        {
            if (player != null && player.Health != null) player.Health.Died -= OnPlayerDied;
            if (upgrades != null && ui != null) upgrades.Offered -= ui.ShowUpgradeCards;
            Time.timeScale = 1f;
        }
    }
}
