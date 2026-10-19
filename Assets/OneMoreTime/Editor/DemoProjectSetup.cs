#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreTime.Editor
{
    /// <summary>Opens the saved game and prepares only missing data; normal startup never regenerates the demo.</summary>
    [InitializeOnLoad]
    public static class DemoProjectSetup
    {
        public const string GameRoot = "Assets/OneMoreTime/Generated_Hacha_Final 2";
        public const string ScenePath = GameRoot + "/Scenes/OneMoreTime_Demo.unity";

        static DemoProjectSetup()
        {
            EditorApplication.delayCall += Prepare;
            EditorSceneManager.sceneOpened += (scene, mode) => { if (scene.path == ScenePath) RepairOpenScene(); };
        }

        public static void Prepare()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += Prepare; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null) return;
            EditorSceneManager.playModeStartScene = sceneAsset;
            if (EditorBuildSettings.scenes.Length != 1 || EditorBuildSettings.scenes[0].path != ScenePath || !EditorBuildSettings.scenes[0].enabled)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            var library = AssetDatabase.LoadAssetAtPath<ArenaPropLibrary>("Assets/OneMoreTime/Resources/" + ArenaPropLibrary.ResourceName + ".asset");
            if (library != null)
                foreach (string kind in ArenaPropFactory.PrefabKinds)
                    if (library.Prefab(kind) == null) { ArenaPrefabGenerator.Generate(); break; }

            // Once per editor session; recompiling scripts must not switch away from a scene being edited.
            if (!Application.isBatchMode && !SessionState.GetBool("OMT.ProjectOpened", false))
            {
                SessionState.SetBool("OMT.ProjectOpened", true);
                bool dirty = false;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    dirty |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty;
                if (!dirty && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
                    EditorSceneManager.OpenScene(ScenePath);
            }
            RepairOpenScene();
        }

        public static void RepairOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var spawner in root.GetComponentsInChildren<EnemySpawner>(true))
                {
                    var data = new SerializedObject(spawner);
                    var enemies = data.FindProperty("enemyPrefabs");
                    string[] kinds = { "Archer", "Mage", "WhiteWolf", "BlueWolf" };
                    if (enemies.arraySize == 0) enemies.arraySize = kinds.Length;
                    for (int i = 0; i < Mathf.Min(kinds.Length, enemies.arraySize); i++)
                        AssignMissing(enemies.GetArrayElementAtIndex(i), kinds[i], typeof(EnemyAI));
                    AssignMissing(data.FindProperty("bossPrefab"), "Boss", typeof(BossAI));
                    changed |= data.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var pool in root.GetComponentsInChildren<ProjectilePool>(true))
                {
                    var data = new SerializedObject(pool);
                    AssignMissing(data.FindProperty("projectilePrefab"), "Projectile", typeof(Projectile));
                    changed |= data.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                    if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
                    { scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; changed = true; }
            }
            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        }

        private static void AssignMissing(SerializedProperty property, string name, System.Type component)
        {
            if (property.objectReferenceValue != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameRoot + "/Prefabs/" + name + ".prefab");
            if (prefab != null) property.objectReferenceValue = prefab.GetComponent(component);
        }
    }
}
#endif
