#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OneMoreTime.Editor
{
    public static class DemoValidation
    {
        public static void ValidateAll()
        {
            ValidateRules();
            var game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Check(game != null, "No hay GameManager en la escena abierta.");
            Require(game, "config", "player", "spawner", "upgrades", "projectiles", "ui", "cameraFollow", "backgrounds", "phaseExitMarker");
            var config = (RunConfig)new SerializedObject(game).FindProperty("config").objectReferenceValue;
            Check(config.Validate(out string error), error);
            var spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            Check(spawner != null && spawner.IsConfigured, "Spawner incompleto: prefabs o GroundMask.");
            var pool = UnityEngine.Object.FindAnyObjectByType<ProjectilePool>();
            Check(pool != null && pool.IsConfigured, "Falta el prefab de proyectil.");
            var ui = UnityEngine.Object.FindAnyObjectByType<UIManager>();
            Require(ui, "mainMenu", "optionsPanel", "hud", "pausePanel", "upgradePanel", "resultPanel",
                "playButton", "optionsButton", "optionsBackButton", "quitButton", "resumeButton", "pauseMenuButton", "retryButton", "resultMenuButton",
                "healthBar", "bossBar", "volumeSlider", "healthText", "phaseText", "statsText", "progressText", "scoreText", "bossText", "resultTitle", "resultDetails", "menuRecord");
            var cards = new SerializedObject(ui).FindProperty("cards");
            Check(cards.arraySize == 3, "La UI debe tener tres tarjetas.");
            for (int i = 0; i < cards.arraySize; i++) Require(cards.GetArrayElementAtIndex(i).objectReferenceValue, "title", "description");
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Check(player != null && player.GetComponent<Rigidbody2D>() != null, "Falta jugador 2D.");
            Require(player, "groundCheck");
            Check(player.GetComponent<MeleeWeapon>() != null && player.GetComponent<ProjectileWeapon>() == null, "El jugador debe usar exclusivamente el hacha cuerpo a cuerpo.");
            Check(player.GetComponent<ArticulatedRig>() != null, "Falta el rig articulado.");
            Check(player.GetComponent<Animator>().runtimeAnimatorController != null, "Falta el Animator Controller del jugador.");
            Check(UnityEngine.Object.FindAnyObjectByType<EventSystem>()?.GetComponent<StandaloneInputModule>() != null, "Falta EventSystem con StandaloneInputModule.");
#if !ENABLE_LEGACY_INPUT_MANAGER
            throw new InvalidOperationException("Activa Input Manager (Old) o Both antes de jugar.");
#endif
            Debug.Log("One More Time: reglas y referencias principales correctas. Completa también las pruebas de Play Mode del LEEME.");
        }
        public static void ValidateRules()
        {
            var run = new RunProgression();
            Check(!run.CompletePhase() && !run.ChooseUpgrade(), "El menú no debe completar fases.");
            run.StartRun();
            for (int phase = 1; phase <= 5; phase++)
            {
                Check(run.Phase == phase && run.State == RunState.Playing, "Orden de fases incorrecto.");
                Check(run.TogglePause() && !run.CompletePhase() && !run.ChooseUpgrade(), "La pausa permitió progresar.");
                Check(run.TogglePause() && run.CompletePhase(), "No se pudo completar una fase normal.");
                Check(run.State == RunState.ChoosingUpgrade && !run.CompletePhase() && !run.TogglePause(), "Se salió de la elección sin tarjeta.");
                Check(run.ChooseUpgrade() && !run.ChooseUpgrade(), "Una tarjeta avanzó dos fases.");
            }
            Check(run.Phase == 6 && run.IsBossPhase && run.CompletePhase(), "El boss no ocupa la fase 6.");
            Check(run.State == RunState.Victory && !run.ChooseUpgrade(), "La victoria ofreció una séptima fase.");
            run.StartRun(); Check(run.Phase == 1 && run.Die() && !run.Die(), "Reinicio o muerte duplicada incorrectos.");
            Check(!run.ChooseUpgrade() && !run.CompletePhase(), "Se pudo avanzar después de morir.");
            run.ReturnToMenu(); Check(run.Phase == 0 && run.State == RunState.MainMenu, "No se volvió al menú.");

            for (int seed = 0; seed < 256; seed++)
            {
                var a = new UpgradeDeck(seed); var b = new UpgradeDeck(seed);
                for (int stage = 0; stage < 5; stage++)
                {
                    var ca = a.DrawThree(); var cb = b.DrawThree(); var ids = new HashSet<string>();
                    for (int i = 0; i < 3; i++)
                    { Check(ids.Add(ca[i].Id), "Una oferta contiene duplicados."); Check(ca[i].Id == cb[i].Id, "La semilla no reproduce las mejoras."); }
                }
            }
            var stats = new StatBlock();
            var benefits = new HashSet<string>();
            foreach (var upgrade in UpgradeDeck.Catalog)
            {
                Check(benefits.Add(upgrade.Stat + "/" + upgrade.TradeoffStat), "Dos cartas repiten la misma combinación de BUFF y DEBUFF.");
                Check(upgrade.HasTradeoff && upgrade.TradeoffStat != upgrade.Stat, "Una carta no tiene DEBUFF real.");
                Check(upgrade.Describe(stats).Contains("BUFF") && upgrade.Describe(stats).Contains("DEBUFF"), "La carta no muestra BUFF y DEBUFF.");
                Check(upgrade.CanApply(stats), "Una carta no tiene efecto inicial.");
                Check(upgrade.Describe(stats).Contains("→"), "La carta no muestra el resultado efectivo.");
            }
            var cappedStats = stats.Copy(); cappedStats.fireRate = 4;
            for (int seed = 0; seed < 256; seed++)
                foreach (var upgrade in new UpgradeDeck(seed).DrawThree(cappedStats))
                    Check(upgrade.Id != "hacha", "Se ofreció cadencia sin efecto al alcanzar su límite.");
            var copy = stats.Copy(); copy.Multiply(StatKind.Damage, 1.2f);
            Check(Math.Abs(stats.damage - 12f) < 0.001f && Math.Abs(copy.damage - 14.4f) < 0.001f, "La copia modificó las estadísticas base.");
            for (int i = 0; i < 100; i++) copy.Multiply(StatKind.FireRate, 1.4f);
            Check(copy.fireRate == 4f, "La cadencia superó su límite seguro.");

            var temporary = new GameObject("OMT_Validation_Temporary");
            try
            {
                var hp = temporary.AddComponent<Health>();
                hp.ResetHealth(100, Team.Player);
                int deaths = 0; hp.Died += ignored => deaths++;
                Check(!hp.TakeDamage(new DamageInfo(50, Team.Player)) && hp.Current == 100, "Fuego amigo habilitado.");
                Check(hp.TakeDamage(new DamageInfo(150, Team.Enemy)) && !hp.TakeDamage(new DamageInfo(150, Team.Enemy)) && deaths == 1, "Muerte duplicada.");
                hp.Heal(100); Check(hp.IsDead, "Curar resucitó al jugador.");
                hp.ResetHealth(100, Team.Player); hp.SetMaximum(125, true);
                Check(hp.Current == 125, "La mejora de vida no recuperó la capacidad añadida.");
                var upgrades = temporary.AddComponent<UpgradeManager>(); upgrades.ResetRun(42); upgrades.Offer();
                Check(!upgrades.TryConsume(-1, out _), "Se aceptó un índice inválido.");
                Check(upgrades.TryConsume(0, out _) && !upgrades.TryConsume(1, out _), "Se aceptaron dos cartas de la misma oferta.");
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
            Debug.Log("One More Time: pruebas de reglas terminadas correctamente (256 semillas, cinco ofertas por semilla).");
        }

        private static void Require(UnityEngine.Object target, params string[] fields)
        {
            Check(target != null, "Falta un componente requerido.");
            var serialized = new SerializedObject(target);
            foreach (string field in fields)
            {
                var property = serialized.FindProperty(field);
                Check(property != null && property.objectReferenceValue != null, "Referencia sin asignar: " + target.name + "." + field);
            }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
