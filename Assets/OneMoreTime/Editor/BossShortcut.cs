#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneMoreTime.Editor
{
    [InitializeOnLoad]
    public static class BossShortcut
    {
        public const string MenuPath = "Tools/One More Time/Ir directo al jefe";
        private const string Pending = "OMT.BossShortcut.Pending";
        private static double deadline;

        static BossShortcut()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false)) WaitForGame();
                if (state == PlayModeStateChange.EnteredEditMode)
                { SessionState.SetBool(Pending, false); EditorApplication.update -= TryStart; }
            };
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPaused = false;
                SessionState.SetBool(Pending, true); WaitForGame();
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DemoProjectSetup.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(DemoProjectSetup.ScenePath);
            }
            PlayerCombatAnimatorSetup.Ensure();
            DemoProjectSetup.Prepare();
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void WaitForGame()
        {
            deadline = EditorApplication.timeSinceStartup + 20;
            EditorApplication.update -= TryStart; EditorApplication.update += TryStart;
        }

        private static void TryStart()
        {
            if (!EditorApplication.isPlaying) return;
            var game = Object.FindAnyObjectByType<GameManager>();
            if (game != null && game.IsReady)
            {
                EditorApplication.update -= TryStart; SessionState.SetBool(Pending, false);
                game.StartBossPreview();
                Debug.Log("Prueba del jefe: fase 6 con estadísticas iniciales. Los récords de esta prueba no se guardan.");
            }
            else if (EditorApplication.timeSinceStartup > deadline)
            {
                EditorApplication.update -= TryStart; SessionState.SetBool(Pending, false);
                Debug.LogError("No se pudo iniciar la prueba del jefe: la escena del juego no terminó de inicializarse.");
            }
        }
    }
}
#endif
