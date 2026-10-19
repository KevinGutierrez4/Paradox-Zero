#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneMoreTime.Editor
{
    [InitializeOnLoad]
    public static class BossGuardValidation
    {
        private const string Pending = "OMT.BossGuardValidation";
        static BossGuardValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Pending, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("BossGuardValidation").AddComponent<BossGuardValidationRunner>();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Pending, false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Pending + ".Passed", false) ? 0 : 1);
                }
            };
        }

        public static void Run()
        {
            EditorApplication.delayCall += () =>
            {
                PlayerCombatAnimatorSetup.Ensure();
                EditorSceneManager.OpenScene(DemoProjectSetup.ScenePath);
                SessionState.SetBool(Pending, true);
                EditorApplication.isPlaying = true;
            };
        }
    }

    public sealed class BossGuardValidationRunner : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>();
        private int errors;
        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; GameManager.SuppressFocusPause = false; }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { errors++; checks.Add("ERROR: " + message + "\n" + stack); }
        }
        private void Check(bool value, string message)
        { checks.Add((value ? "PASS: " : "FAIL: ") + message); if (!value) errors++; }

        private IEnumerator Start()
        {
            GameManager.SuppressFocusPause = true;
            yield return null;
            var test = Tests();
            while (true)
            {
                object current;
                try { if (!test.MoveNext()) break; current = test.Current; }
                catch (Exception e) { errors++; checks.Add("FAIL: " + e); break; }
                yield return current;
            }
            Directory.CreateDirectory("Verification/BossGuard");
            File.WriteAllLines("Verification/BossGuard/results.txt", checks);
            SessionState.SetBool("OMT.BossGuardValidation.Passed", errors == 0);
            Debug.Log(errors == 0 ? "OMT_BOSS_GUARD_PASS" : "OMT_BOSS_GUARD_FAIL");
            EditorApplication.isPlaying = false;
        }

        private IEnumerator Tests()
        {
            var game = FindAnyObjectByType<GameManager>();
            Check(game != null && game.IsReady, "Game initialized");
            if (game == null || !game.IsReady) yield break;
            int record = PlayerPrefs.GetInt("OMT.BestScore.v1", 0);
            int bestScore = game.BestScore;
            game.StartNewRun();
            game.Spawner.enabled = false; game.Spawner.Clear();
            var player = game.Player;
            yield return new WaitForSeconds(0.7f);
            player.enabled = false;
            var body = player.GetComponent<Rigidbody2D>(); body.simulated = false;
            var guard = player.Guard;
            var animator = player.GetComponent<Animator>();
            var driver = player.GetComponent<AnimationDriver>();
            var weapon = player.GetComponent<MeleeWeapon>();
            var health = player.Health;
            driver.SetMotion(0, true, false, 0);
            health.ResetHealth(100, Team.Player);
            guard.SetBlocking(true);
            Check(guard.IsBlocking && animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Block"), "Shield raises immediately using the authored Block clip");
            Check(Mathf.Abs(guard.Reduction - 0.65f) < 0.001f && Mathf.Abs(guard.MoveMultiplier - 0.45f) < 0.001f, "Shield restores 65% damage reduction and slower movement");
            health.TakeDamage(new DamageInfo(20, Team.Enemy));
            Check(Mathf.Abs(health.Current - 93) < 0.001f, "Blocked 20-damage hit deals 7 damage in the actual game scene");
            Check(!weapon.RequestAttack(12, 1.6f), "Cannot attack while blocking");
            guard.SetBlocking(false);
            health.ResetHealth(100, Team.Player);
            health.TakeDamage(new DamageInfo(20, Team.Enemy));
            Check(Mathf.Abs(health.Current - 80) < 0.001f && Mathf.Abs(guard.MoveMultiplier - 1) < 0.001f, "Releasing shield restores full incoming damage and movement");
            health.ResetHealth(100, Team.Player);
            driver.SetMotion(7.5f, true, false, 0);
            Check(weapon.RequestAttack(12, 0.5f), "Attack starts while walking");
            yield return null; yield return null;
            int legs = animator.GetLayerIndex("LowerBodyLocomotion");
            var knee = player.transform.Find("Visual/Rig/Pelvis/FrontLeg/Lower");
            float first = knee.localEulerAngles.z;
            yield return new WaitForSeconds(0.15f);
            Check(legs >= 0 && animator.GetLayerWeight(legs) > 0.99f && Mathf.Abs(Mathf.DeltaAngle(first, knee.localEulerAngles.z)) > 0.5f,
                "Legs continue walking during the axe attack");
            Check(weapon.IsAttacking && animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Attack"), "Walking legs preserve the upper-body attack and its combat clock");
            guard.SetBlocking(true);
            Check(!weapon.IsAttacking && animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Block"), "Raising shield cancels an active swing and switches to Block");
            first = knee.localEulerAngles.z;
            yield return new WaitForSeconds(0.15f);
            Check(animator.GetLayerWeight(legs) > 0.99f && Mathf.Abs(Mathf.DeltaAngle(first, knee.localEulerAngles.z)) > 0.5f,
                "Legs also keep walking while the shield stays raised");
            driver.SetMotion(0, true, false, 0);
            yield return null;
            Check(animator.GetLayerWeight(legs) < 0.01f, "Standing still disables the walking override");
            game.TogglePause();
            Check(!guard.IsBlocking && Mathf.Abs(health.GuardDamageMultiplier - 1) < 0.001f, "Pause releases guard and clears damage reduction");
            game.Spawner.enabled = true;
            Check(EditorApplication.ExecuteMenuItem(BossShortcut.MenuPath), "Boss tool menu is available");
            float deadline = Time.realtimeSinceStartup + 3;
            while (game.Phase != 6 && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.IsPlaying && game.Phase == 6 && game.IsBossPreview && game.Score == 0 && game.Spawner.CurrentBoss != null,
                "Boss tool jumps from pause to a fresh phase 6 without playing phases 1-5");
            Check(player.ControlLocked && !game.Spawner.CurrentBoss.IntroFinished, "Boss shortcut preserves its presentation");
            Check(game.Spawner.CurrentBoss.Body != null && game.Spawner.CurrentBoss.Body.HasArt
                && game.Spawner.CurrentBoss.Body.UsesContinuousNeck, "Recovered boss uses the three-phase artwork and continuous neck");
            Check(AssetDatabase.GetAssetPath(game.Surface.GetComponent<UnityEngine.SpriteRenderer>().sprite)
                == "Assets/OneMoreTime/Art/Fondos/BossArena.png", "Recovered boss arena keeps the supplied final background");
            game.Spawner.CurrentBoss.Stun.Begin(KeyCode.A, KeyCode.D, ignored => { });
            EditorApplication.ExecuteMenuItem(BossShortcut.MenuPath);
            yield return null; yield return null; yield return null;
            Check(player.ControlLocked && !game.Spawner.CurrentBoss.IntroFinished, "Restarting boss preview closes the old QTE without unlocking the new introduction");
            Time.timeScale = 3;
            deadline = Time.realtimeSinceStartup + 18;
            while (!game.Spawner.CurrentBoss.IntroFinished && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Spawner.CurrentBoss.IntroFinished && !player.ControlLocked && game.IsPlaying, "Boss presentation finishes and hands control back for combat");
            var skull = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/OneMoreTime/Art/JormungandrPhases/JP1_Skull.png");
            Check(skull != null && Mathf.Abs(skull.pixelsPerUnit - 72) < .01f
                && game.Spawner.CurrentBoss.Body.HeadBounds.size.x > 3f, "Recovered giant boss keeps the intended sprite scale and aligned jaw");
            game.RegisterKill(100000);
            game.ReturnToMenu();
            Check(game.BestScore == bestScore && PlayerPrefs.GetInt("OMT.BestScore.v1", 0) == record, "Boss preview cannot alter saved or displayed records");
            player.enabled = true;
            game.StartNewRun();
            Check(game.Phase == 1 && !game.IsBossPreview && game.IsPlaying, "Normal JUGAR still starts phase 1 after a boss preview");
            game.ReturnToMenu();
            Check(errors == 0, "No runtime errors during guard, walking-attack, or boss shortcut tests");
        }
    }
}
#endif
