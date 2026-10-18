#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneMoreTime.Editor
{
    /// <summary>
    /// Films the V5 features in Play Mode (enemy attacks, portal, boss presentation and fight) into
    /// Verification/V5Play/*.png so animation timing can be reviewed frame by frame. Read-only for gameplay.
    /// </summary>
    [InitializeOnLoad]
    public static class DemoV5VisualCheck
    {
        static DemoV5VisualCheck() { EditorApplication.playModeStateChanged += Changed; }

        [MenuItem("Tools/One More Time/8. Capturas V5 (enemigos, portal, jefe)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Sal de Play antes de capturar.");
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
            SessionState.SetBool("OMT.V5Visual", true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool("OMT.V5Visual", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("V5VisualRunner").AddComponent<DemoV5VisualRunner>();
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool("OMT.V5Visual", false);
        }
    }

    public sealed class DemoV5VisualRunner : MonoBehaviour
    {
        private const string Dir = "Verification/V5Play/";
        private GameManager game;
        private PlayerController player;
        private Rigidbody2D body;
        private int errors;
        private StreamWriter log;

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
            log.WriteLine("ERRORS " + errors);
            log.Close();
            Time.timeScale = 1;
            EditorApplication.isPlaying = false;
        }

        private void Place(float x) { body.position = new Vector2(x, game.GroundHeight(x) + 0.05f); body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }

        private void Capture(string name)
        {
            var camera = Camera.main; var previousTarget = camera.targetTexture;
            var target = new RenderTexture(800, 450, 24); camera.targetTexture = target;
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(800, 450, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 800, 450), 0, 0); image.Apply();
            File.WriteAllBytes(Dir + name + ".png", image.EncodeToPNG());
            Destroy(image); RenderTexture.active = previous; camera.targetTexture = previousTarget;
            target.Release(); Destroy(target);
        }

        private IEnumerator Film(string prefix, float seconds, float step, Func<string> label = null)
        {
            int i = 0;
            for (float t = 0; t < seconds; t += step)
            {
                // Real time: the upgrade choice pauses the game (timeScale 0) and must not freeze the filming.
                yield return new WaitForSecondsRealtime(step);
                string extra = label != null ? "_" + Sanitize(label()) : "";
                Capture($"{prefix}_{i:000}{extra}");
                i++;
            }
        }

        private static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            return s.Replace(' ', '-').Replace('·', '-');
        }

        private IEnumerator Run()
        {
            yield return null; yield return null;
            game = FindAnyObjectByType<GameManager>(); player = game.Player; body = player.GetComponent<Rigidbody2D>();
            typeof(GameManager).GetField("persistRecords", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(game, false);
            game.StartNewRun();
            game.Spawner.enabled = false;
            yield return new WaitForSeconds(0.6f);
            player.Health.Invulnerable = true;
            Place(game.PhaseStart + 4);
            player.GetComponent<AnimationDriver>().Face(1);
            yield return new WaitForSeconds(0.3f);
            string root = EditorBuildSettings.scenes[0].path; root = root.Substring(0, root.LastIndexOf("/Scenes/", StringComparison.Ordinal));
            var pool = FindAnyObjectByType<ProjectilePool>();
            foreach (var (kind, dx, seconds) in new[] { ("WhiteWolf", 3.2f, 2.4f), ("BlueWolf", 4.2f, 2.4f), ("Archer", 5.5f, 2.6f), ("Mage", 5.5f, 2.8f) })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Prefabs/" + kind + ".prefab");
                float x = body.position.x + dx;
                var enemy = Instantiate(prefab, new Vector3(x, game.GroundHeight(x) + 0.04f, 0), Quaternion.identity).GetComponent<EnemyAI>();
                enemy.Initialize(game, player, pool, 1, LayerMask.GetMask("OMT_Ground"));
                yield return Film(kind, seconds, 0.07f, () => enemy != null ? enemy.State + "-" + enemy.Stage : "gone");
                log.WriteLine(kind + " final state " + (enemy != null ? enemy.State.ToString() : "destroyed"));
                if (enemy != null) { enemy.Health.Kill(); yield return Film(kind + "_death", 1.4f, 0.1f); }
                pool.ClearActive();
                yield return new WaitForSeconds(0.3f);
            }
            // Portal opening, idle and travel.
            game.Spawner.enabled = true;
            float until = Time.realtimeSinceStartup + 25; Time.timeScale = 10;
            while (!game.Portals.ExitOpen && !game.Portals.ExitOpening && Time.realtimeSinceStartup < until)
            {
                Place(game.PhaseEnd - 6f);
                foreach (var e in FindObjectsByType<EnemyActor>()) if (e.IsAlive) e.Health.Kill();
                yield return null;
            }
            Time.timeScale = 1;
            Place(game.PhaseEnd - 5.5f);
            yield return Film("Portal_open", 1.2f, 0.12f);
            Place(game.PhaseEnd - 2.6f);
            yield return Film("Portal_idle", 0.6f, 0.15f);
            game.Portals.RequestEnter();
            yield return Film("Portal_enter", 1.0f, 0.1f);
            log.WriteLine("Portal state after enter " + game.State);
            float wait = Time.realtimeSinceStartup + 3;
            while (game.State != RunState.ChoosingUpgrade && Time.realtimeSinceStartup < wait) yield return null;
            game.SelectUpgrade(0);
            yield return Film("Portal_arrival", 1.2f, 0.12f);
            // Advance to the boss quickly.
            Time.timeScale = 10;
            while (game.Phase < 6 && game.State != RunState.GameOver && Time.realtimeSinceStartup < until + 120)
            {
                if (game.State == RunState.ChoosingUpgrade) { game.SelectUpgrade(0); yield return null; continue; }
                Place(game.PhaseEnd - 1.4f);
                foreach (var e in FindObjectsByType<EnemyActor>()) if (e.IsAlive) e.Health.Kill();
                if (game.Portals.ExitOpen) game.Portals.RequestEnter();
                yield return null;
            }
            Time.timeScale = 1;
            var boss = game.Spawner.CurrentBoss;
            log.WriteLine("Reached phase " + game.Phase + " boss " + (boss != null));
            if (boss == null) yield break;
            player.Health.Invulnerable = true;
            Place(boss.transform.position.x - 9f);
            yield return Film("Boss_intro", 7.4f, 0.2f, () => boss.PatternName);
            log.WriteLine("Intro finished " + boss.IntroFinished + " locked " + player.ControlLocked + " invulnerable " + boss.Health.Invulnerable);
            yield return Film("Boss_s1", 14f, 0.25f, () => boss.Stage + "-" + boss.PatternName);
            boss.Health.TakeDamage(new DamageInfo(boss.Health.Maximum * 0.4f, Team.Player));
            yield return Film("Boss_s2", 18f, 0.25f, () => boss.Stage + "-" + boss.PatternName);
            boss.Health.TakeDamage(new DamageInfo(boss.Health.Maximum * 0.3f, Team.Player));
            yield return Film("Boss_s3", 20f, 0.25f, () => boss.Stage + "-" + boss.PatternName);
            player.Health.Invulnerable = false;
            boss.Health.Kill();
            yield return null;
            for (int i = 0; i < 8; i++) { yield return new WaitForSecondsRealtime(0.3f); Capture($"Boss_death_{i:000}"); }
            log.WriteLine("End state " + game.State + " projectiles " + FindObjectsByType<Projectile>().Length + " hazards " + FindObjectsByType<BossHazard>().Length);
        }
    }
}
#endif
