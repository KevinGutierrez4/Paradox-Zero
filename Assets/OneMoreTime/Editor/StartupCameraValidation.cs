#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreTime.Editor
{
    [InitializeOnLoad]
    public static class StartupCameraValidation
    {
        private const string Pending = "OMT.StartupCameraValidation";
        static StartupCameraValidation() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de probar.");
            // Let editor startup/indexing callbacks finish before observing gameplay errors.
            EditorApplication.delayCall += Begin;
        }

        private static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            DemoProjectSetup.Prepare();
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>()) camera.enabled = false;
                new GameObject("StartupCameraValidation").AddComponent<StartupCameraValidationRunner>();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Pending, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Pending + ".Passed", false) ? 0 : 1);
            }
        }
    }

    public sealed class StartupCameraValidationRunner : MonoBehaviour
    {
        private readonly List<string> results = new List<string>();
        private int errors;
        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; GameManager.SuppressFocusPause = false; }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; results.Add("ERROR: " + message); }
        }
        private void Check(bool condition, string message)
        {
            results.Add((condition ? "PASS: " : "FAIL: ") + message);
            if (!condition) errors++;
        }
        private static T Reference<T>(UnityEngine.Object obj, string field) where T : UnityEngine.Object
            => new SerializedObject(obj).FindProperty(field).objectReferenceValue as T;

        private IEnumerator RunChecks(IEnumerator test)
        {
            while (true)
            {
                object current;
                try { if (!test.MoveNext()) yield break; current = test.Current; }
                catch (Exception e) { Check(false, e.ToString()); yield break; }
                yield return current;
            }
        }

        private IEnumerator Capture(GameManager game, Vector2Int size, string label = "game")
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var camera = game.CameraRig.GetComponent<Camera>();
            var canvas = game.UI.GetComponent<Canvas>();
            var originalMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            var originalDistance = canvas.planeDistance;
            var originalTarget = camera.targetTexture;
            var target = new RenderTexture(size.x, size.y, 24);
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                yield return null;
                game.CameraRig.SetBounds(game.PhaseStart, game.PhaseEnd, true);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                File.WriteAllBytes("Verification/startup-" + label + "-" + size.x + "x" + size.y + ".png", image.EncodeToPNG());
                var corners = new Vector3[4];
                bool inside = true;
                foreach (var text in canvas.GetComponentsInChildren<Text>())
                {
                    text.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = camera.WorldToViewportPoint(corner);
                        if (point.x < -0.001f || point.x > 1.001f || point.y < -0.001f || point.y > 1.001f)
                            results.Add("Outside screen: " + text.name + " at " + point);
                        inside &= point.x >= -0.001f && point.x <= 1.001f && point.y >= -0.001f && point.y <= 1.001f;
                    }
                }
                Check(inside, label + " " + size + ": visible UI labels fit entirely inside rendered screen");
            }
            finally
            {
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalDistance;
                camera.targetTexture = originalTarget;
                RenderTexture.active = previous;
                target.Release(); Destroy(target);
                if (image != null) Destroy(image);
            }
        }

        private IEnumerator Start()
        {
            GameManager.SuppressFocusPause = true;
            yield return null;
            var test = Tests();
            while (true)
            {
                object current;
                try { if (!test.MoveNext()) break; current = test.Current; }
                catch (Exception e) { results.Add("FAIL: " + e); errors++; break; }
                yield return current;
            }
            Directory.CreateDirectory("Verification");
            File.WriteAllLines("Verification/startup-camera-results.txt", results);
            SessionState.SetBool("OMT.StartupCameraValidation.Passed", errors == 0);
            Debug.Log(errors == 0 ? "OMT_STARTUP_CAMERA_PASS" : "OMT_STARTUP_CAMERA_FAIL");
            EditorApplication.isPlaying = false;
        }

        private IEnumerator Tests()
        {
            var game = FindAnyObjectByType<GameManager>();
            Check(game != null && game.enabled && game.State == RunState.MainMenu, "Scene initializes into the main menu");
            if (game == null || !game.enabled) yield break;
            DemoValidation.ValidateAll();
            Check(EditorSceneManager.playModeStartScene != null && AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == DemoProjectSetup.ScenePath,
                "Play always uses the configured game scene without running Tools");
            var player = game.Player;
            var camera = game.CameraRig.GetComponent<Camera>();
            results.Add("Menu player=" + player.transform.position + " camera=" + camera.transform.position);
            var initialView = camera.WorldToViewportPoint(player.GetComponent<BoxCollider2D>().bounds.center);
            Check(initialView.x > 0 && initialView.x < 1 && initialView.y > 0 && initialView.y < 1,
                "Player is framed immediately while menu time is paused");
            yield return RunChecks(AnimationPresentationValidation.CheckRuntime(Check));
            Reference<Button>(game.UI, "playButton").onClick.Invoke();
            Check(game.IsPlaying && Time.timeScale == 1, "JUGAR starts phase 1 and resumes gameplay time");
            player.Health.Invulnerable = true;
            typeof(GameManager).GetField("persistRecords", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, false);
            yield return new WaitForSeconds(0.7f);
            Check(player.GetComponent<AnimationDriver>().Fade > 0.99f, "Player is visible after arrival");
            results.Add("Playing player=" + player.transform.position + " camera=" + camera.transform.position);
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080), new Vector2Int(720, 1280) })
            {
                camera.aspect = (float)size.x / size.y;
                game.CameraRig.SetBounds(game.PhaseStart, game.PhaseEnd, true);
                yield return null;
                var bounds = player.GetComponent<BoxCollider2D>().bounds;
                var bottom = camera.WorldToViewportPoint(bounds.min);
                var top = camera.WorldToViewportPoint(bounds.max);
                Check(bottom.x >= 0 && top.x <= 1 && bottom.y >= 0 && top.y <= 1,
                    size + ": whole player is inside viewport (" + bottom + " to " + top + ")");
                yield return Capture(game, size);
            }
            camera.ResetAspect();
            var canvas = game.UI.GetComponent<CanvasScaler>();
            Check(canvas != null && canvas.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand,
                "Canvas retains the full 1280x720 layout on narrow and wide displays");
            player.SetPhaseBounds(100, 148, game.GroundHeight(2));
            game.CameraRig.SetBounds(100, 148, true);
            var teleportedView = camera.WorldToViewportPoint(player.GetComponent<BoxCollider2D>().bounds.center);
            Check(teleportedView.x > 0 && teleportedView.x < 1,
                "Camera sees the teleported player immediately, before another physics step");
            game.TogglePause();
            Check(game.State == RunState.Paused && Time.timeScale == 0, "Pause works");
            game.ReturnToMenu();
            Reference<Button>(game.UI, "playButton").onClick.Invoke();
            yield return new WaitForSeconds(0.7f);
            Check(game.IsPlaying && player.GetComponent<AnimationDriver>().Fade > 0.99f, "Returning to menu and starting again restores gameplay");
            typeof(GameManager).GetMethod("FinishPhase", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Check(game.State == RunState.ChoosingUpgrade, "Upgrade cards open while gameplay is paused");
            foreach (var size in new[] { new Vector2Int(1390, 550), new Vector2Int(640, 360), new Vector2Int(400, 900) })
            {
                camera.aspect = (float)size.x / size.y;
                yield return Capture(game, size, "upgrades");
            }
            camera.ResetAspect();
            game.ReturnToMenu();
            Check(errors == 0, "All startup and screen checks pass without runtime errors");
        }
    }
}
#endif
