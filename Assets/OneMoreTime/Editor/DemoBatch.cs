#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OneMoreTime.Editor
{
    public static class DemoBatch
    {
        public static void RefreshPresentation()
        {
            string path = EditorBuildSettings.scenes[0].path;
            string root = path.Substring(0, path.LastIndexOf("/Scenes/", StringComparison.Ordinal));
            DemoRigFactory.SpriteDirectory = root + "/Sprites";
            DemoRigFactory.Material = AssetDatabase.LoadAssetAtPath<Material>(root + "/SpritesUnlit.mat");
            DemoAnimatorFactory.RefreshClips(root);
            foreach (string kind in new[] { "Player", "Archer", "Mage", "WhiteWolf", "BlueWolf", "Boss" })
            {
                string prefabPath = root + "/Prefabs/" + kind + ".prefab";
                var actor = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var pose = actor.GetComponent<ArticulatedRig>(); pose.Bind(); pose.EvaluatePose(0, 0, true, 0, 0, -1);
                    PrefabUtility.SaveAsPrefabAsset(actor, prefabPath, out bool saved);
                    if (!saved) throw new InvalidOperationException(prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(actor); }
            }
            AssetDatabase.SaveAssets();
            DemoVisualValidation.RenderPoses();
            Debug.Log("OMT_PRESENTATION_PASS");
        }
        public static void Build()
        {
            int errors = 0;
            Application.LogCallback listener = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            Application.logMessageReceived += listener;
            try
            {
                DemoSceneBuilder.Build();
                DemoValidation.ValidateAll();
                DemoVisualValidation.RenderPoses();
                File.WriteAllText("Verification/build-result.txt", "PASS: Unity " + Application.unityVersion + ", scene generated and references/rules validated.\nScene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                if (errors != 0) throw new InvalidOperationException("Unity logged " + errors + " errors while generating or rendering.");
                Debug.Log("OMT_BUILD_PASS");
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
            finally { Application.logMessageReceived -= listener; }
        }
    }
}
#endif
