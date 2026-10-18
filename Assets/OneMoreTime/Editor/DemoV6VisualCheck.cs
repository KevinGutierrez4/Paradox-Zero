#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneMoreTime.Editor
{
    /// <summary>
    /// Films the V6 work in Play Mode into contact sheets (Verification/V6Play/*.png): interactive tutorial,
    /// scenery of every winter, upgrade cards, and Jörmungandr phase by phase (intro, each attack, transformation,
    /// roar stun QTE, transition specials, phase three, defeat). UI is included. Read-only for gameplay.
    /// </summary>
    [InitializeOnLoad]
    public static class DemoV6VisualCheck
    {
        static DemoV6VisualCheck() { EditorApplication.playModeStateChanged += Changed; }

        [MenuItem("Tools/One More Time/10. Capturas V6 (jefe por fases, QTE, trampas, tutorial)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Sal de Play antes de capturar.");
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
            SessionState.SetBool("OMT.V6Visual", true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool("OMT.V6Visual", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("V6VisualRunner").AddComponent<DemoV6VisualRunner>();
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool("OMT.V6Visual", false);
        }
    }

    public sealed class DemoV6VisualRunner : MonoBehaviour
    {
        private const string Dir = "Verification/V6Play/";
        private const int W = 480, H = 270, Cols = 4;
        private GameManager game;
        private PlayerController player;
        private Rigidbody2D body;
        private UIManager ui;
        private int errors;
        private StreamWriter log;
        private readonly List<Texture2D> frames = new List<Texture2D>();
        private readonly List<string> labels = new List<string>();

        private void OnEnable() { Application.logMessageReceived += Log; }
        private void OnDisable() { Application.logMessageReceived -= Log; }
        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) { errors++; log?.WriteLine("ERROR " + message + "\n" + stack); }
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(Dir);
            foreach (var f in Directory.GetFiles(Dir, "*.png")) File.Delete(f);
            log = new StreamWriter(Dir + "log.txt", false);
            var run = Run();
            while (true)
            {
                object next;
                try { if (!run.MoveNext()) break; next = run.Current; }
                catch (Exception e) { log.WriteLine("EXCEPTION " + e); break; }
                yield return next;
            }
            Flush("zz_rest");
            log.WriteLine("ERRORS " + errors);
            log.Close();
            Time.timeScale = 1;
            TutorialController.Suppressed = false;
            EditorApplication.isPlaying = false;
        }

        private void Place(float x) { body.position = new Vector2(x, game.GroundHeight(x) + 0.05f); body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }

        private Texture2D Grab()
        {
            var canvas = ui != null ? ui.GetComponent<Canvas>() : null;
            RenderMode mode = RenderMode.ScreenSpaceOverlay; Camera cam = null; float distance = 1;
            if (canvas != null) { mode = canvas.renderMode; cam = canvas.worldCamera; distance = canvas.planeDistance; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = 1; Canvas.ForceUpdateCanvases(); }
            var camera = Camera.main; var previousTarget = camera.targetTexture;
            var target = new RenderTexture(W, H, 24); camera.targetTexture = target;
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(W, H, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, W, H), 0, 0); image.Apply();
            RenderTexture.active = previous; camera.targetTexture = previousTarget;
            target.Release(); Destroy(target);
            if (canvas != null) { canvas.renderMode = mode; canvas.worldCamera = cam; canvas.planeDistance = distance; }
            return image;
        }

        private void Shot(string label) { frames.Add(Grab()); labels.Add(label); }

        /// <summary>Writes the collected frames as one contact sheet (row-major, 4 columns) plus an index in the log.</summary>
        private void Flush(string name)
        {
            if (frames.Count == 0) return;
            int rows = (frames.Count + Cols - 1) / Cols;
            var sheet = new Texture2D(Cols * W, rows * H, TextureFormat.RGB24, false);
            var black = new Color[sheet.width * sheet.height]; sheet.SetPixels(black);
            for (int i = 0; i < frames.Count; i++)
            {
                int c = i % Cols, r = rows - 1 - i / Cols;
                sheet.SetPixels(c * W, r * H, W, H, frames[i].GetPixels());
                Destroy(frames[i]);
            }
            sheet.Apply();
            File.WriteAllBytes(Dir + name + ".png", sheet.EncodeToPNG());
            Destroy(sheet);
            log.WriteLine("SHEET " + name + ": " + string.Join(" | ", labels));
            frames.Clear(); labels.Clear();
        }

        private IEnumerator Film(string name, float seconds, float step, Func<string> label)
        {
            for (float t = 0; t < seconds; t += step)
            {
                yield return new WaitForSecondsRealtime(step);
                Shot(label != null ? label() : name);
            }
        }

        private IEnumerator Run()
        {
            yield return null; yield return null;
            game = FindFirstObjectByType<GameManager>(); player = game.Player; body = player.GetComponent<Rigidbody2D>();
            ui = FindFirstObjectByType<UIManager>();
            typeof(GameManager).GetField("persistRecords", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(game, false);
            TutorialController.Suppressed = false; GameManager.SuppressFocusPause = true;
            game.Tutorial.Persist = false;
            game.StartNewRun();
            yield return new WaitForSeconds(0.5f);
            player.Health.Invulnerable = true;
            // 1. Tutorial with large keys (each step forced, as if performed).
            Place(game.PhaseStart + 3.5f);
            for (int i = 0; i < 6; i++)
            {
                if (i == 1) Place(game.PhaseStart + 6.2f);
                if (i == 4) Place(game.PhaseStart + 11.2f);
                yield return new WaitForSecondsRealtime(0.45f);
                Shot("Tutorial " + game.Tutorial.Current);
                game.Tutorial.ForceAdvance();
            }
            yield return new WaitForSecondsRealtime(0.3f); Shot("Tutorial done");
            Flush("01_tutorial");
            // 2. Scenery of each winter and the upgrade cards.
            for (int phase = 1; phase <= 5; phase++)
            {
                game.Spawner.enabled = false;
                foreach (float t in new[] { 0.25f, 0.45f, 0.65f, 0.85f })
                {
                    Place(Mathf.Lerp(game.PhaseStart + 6, game.PhaseEnd - 4, t));
                    yield return new WaitForSecondsRealtime(0.5f);
                    Shot("Phase " + phase + " t=" + t);
                }
                game.Spawner.enabled = true;
                float until = Time.realtimeSinceStartup + 25; Time.timeScale = 10;
                while (game.State == RunState.Playing && Time.realtimeSinceStartup < until)
                {
                    Place(game.PhaseEnd - 1.4f);
                    foreach (var e in FindObjectsByType<EnemyActor>(FindObjectsSortMode.None)) if (e.IsAlive) e.Health.Kill();
                    if (game.Portals.ExitOpen) game.Portals.RequestEnter();
                    yield return null;
                }
                Time.timeScale = 1;
                if (phase == 1) { yield return new WaitForSecondsRealtime(0.3f); Shot("Upgrade cards"); }
                game.SelectUpgrade(0);
                yield return new WaitForSecondsRealtime(0.2f);
            }
            Flush("02_scenery_and_cards");
            // 3. Boss.
            var boss = game.Spawner.CurrentBoss;
            log.WriteLine("Boss phase " + game.Phase + " boss " + (boss != null));
            if (boss == null) yield break;
            Place(game.PhaseStart + 6);
            yield return Film("intro", 7.6f, 0.4f, () => "Intro " + boss.PatternName);
            Flush("03_boss_intro");
            boss.AutoAttack = false;
            yield return new WaitForSecondsRealtime(1.2f);
            var attacks = new[] { BossAttack.MordidaDeMidgard, BossAttack.VentiscaRasante, BossAttack.OlaDeEscamas, BossAttack.ErupcionDeColmillos,
                BossAttack.RespiracionDeVeneno, BossAttack.ContraccionDosTiempos, BossAttack.EspinasCarmesi, BossAttack.AlientoDelRagnarok,
                BossAttack.FracturaDelMundo, BossAttack.JuicioDeLaSerpiente };
            int sheet = 0;
            foreach (var attack in attacks)
            {
                Place(boss.FrontX - (attack == BossAttack.MordidaDeMidgard || attack == BossAttack.JuicioDeLaSerpiente ? 4f : 7f));
                var run = boss.StartCoroutine(boss.Run(attack));
                float dur = attack == BossAttack.JuicioDeLaSerpiente ? 6.4f : attack == BossAttack.VentiscaRasante || attack == BossAttack.RespiracionDeVeneno || attack == BossAttack.AlientoDelRagnarok ? 4.8f : 3.6f;
                yield return Film(attack.ToString(), dur, 0.4f, () => attack + " " + boss.AttackStageNow + " " + boss.StageProgress.ToString("0.0"));
                boss.StopCoroutine(run);
                yield return new WaitForSecondsRealtime(0.8f);
                if (++sheet % 2 == 0) Flush("04_attacks_" + sheet / 2);
            }
            Flush("04_attacks_last");
            // Transformation into phase two, roar stun QTE (A + D) and Quebranto glacial.
            boss.AutoAttack = true;
            boss.Health.TakeDamage(new DamageInfo(boss.Health.Maximum * 0.31f, Team.Player));
            float deadline = Time.realtimeSinceStartup + 14;
            while (boss.TransitionStep != BossTransitionStep.Stun && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(0.35f);
                Shot("T2 " + boss.TransitionStep);
            }
            yield return new WaitForSecondsRealtime(0.3f); Shot("T2 stun QTE 0/4");
            for (int i = 0; i < 4; i++)
            {
                boss.Stun.Press(boss.Stun.FirstKey); boss.Stun.Press(boss.Stun.SecondKey);
                if (i == 1) { yield return null; Shot("T2 stun QTE 2/4"); }
            }
            yield return null; Shot("T2 recovered");
            Flush("05_transition_2");
            deadline = Time.realtimeSinceStartup + 16;
            while (boss.Transitioning && Time.realtimeSinceStartup < deadline) { yield return new WaitForSecondsRealtime(0.4f); Shot("T2 " + boss.TransitionStep + " " + boss.PatternName); }
            Flush("06_quebranto");
            yield return Film("phase2", 6f, 0.5f, () => "P2 " + boss.PatternName);
            Flush("07_phase2_fight");
            // Phase three: Eclipse carmesí after the W + D stun (left to time out here to film the penalty path).
            boss.Health.TakeDamage(new DamageInfo(Mathf.Max(1, boss.Health.Current - boss.Health.Maximum * 0.33f), Team.Player));
            deadline = Time.realtimeSinceStartup + 30;
            while (boss.Transitioning && Time.realtimeSinceStartup < deadline) { yield return new WaitForSecondsRealtime(0.5f); Shot("T3 " + boss.TransitionStep + " " + boss.PatternName); }
            Flush("08_transition_3_eclipse");
            yield return Film("phase3", 8f, 0.5f, () => "P3 " + boss.PatternName);
            Flush("09_phase3_fight");
            player.Health.Invulnerable = false;
            boss.Health.Kill();
            yield return Film("death", 3.2f, 0.4f, () => "Defeat");
            Flush("10_defeat");
            log.WriteLine("End " + game.State + " projectiles " + FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length + " hazards " + FindObjectsByType<BossHazard>(FindObjectsSortMode.None).Length);
        }
    }
}
#endif
