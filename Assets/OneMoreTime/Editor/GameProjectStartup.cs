#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OneMoreTime.Editor
{
    [InitializeOnLoad]
    public static class GameProjectStartup
    {
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        static GameProjectStartup()
        {
            EditorApplication.delayCall += Initialize;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Configure();
            };
        }

        private static void Initialize()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += Initialize; return; }
            Configure();
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path == ScenePath || !scene.path.EndsWith("/Scenes/OneMoreTime_Demo.unity", StringComparison.Ordinal)) return;
            OpenGame();
        }

        internal static string ScenePath => EditorBuildSettings.scenes.Length > 0 ? EditorBuildSettings.scenes[0].path : string.Empty;

        public static void Configure()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
            if (pipeline != null)
            {
                if (GraphicsSettings.defaultRenderPipeline != pipeline) GraphicsSettings.defaultRenderPipeline = pipeline;
                if (QualitySettings.renderPipeline != pipeline) QualitySettings.renderPipeline = pipeline;
            }
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene != null && EditorSceneManager.playModeStartScene != scene) EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem("Tools/One More Time/Abrir juego")]
        public static void OpenGame()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Sal de Play antes de abrir la escena del juego."); return; }
            Configure();
            // Preserve unsaved edits before leaving an obsolete or broken scene.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty || !scene.isLoaded) continue;
                const string recovery = "Assets/_Recovery";
                Directory.CreateDirectory(recovery);
                AssetDatabase.Refresh();
                string backup = AssetDatabase.GenerateUniqueAssetPath(recovery + "/BeforeStartupRepair_" + scene.name + ".unity");
                if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new InvalidOperationException("No se pudo respaldar la escena abierta: " + scene.name);
                Debug.Log("Copia de los cambios de la escena anterior: " + backup);
            }
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Juego preparado. Pulsa Play y después JUGAR.");
        }
    }
}
#endif
