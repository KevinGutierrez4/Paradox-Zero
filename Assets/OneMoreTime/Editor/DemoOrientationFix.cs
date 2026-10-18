#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneMoreTime.Editor
{
    public static class DemoOrientationFix
    {
        [MenuItem("Tools/One More Time/6. Corregir orientacion y brazos")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Sal de Play antes de corregir los prefabs.");
            string scenePath = EditorBuildSettings.scenes[0].path;
            string root = scenePath.Substring(0, scenePath.LastIndexOf("/Scenes/", StringComparison.Ordinal));
            Directory.CreateDirectory("Verification/BeforeOrientationFix");
            DemoRigFactory.SpriteDirectory = root + "/Sprites";
            DemoRigFactory.Material = AssetDatabase.LoadAssetAtPath<Material>(root + "/SpritesUnlit.mat");
            foreach (string kind in new[] { "Player", "Archer", "Mage", "WhiteWolf", "BlueWolf", "Boss" })
            {
                string path = root + "/Prefabs/" + kind + ".prefab";
                string backup = "Verification/BeforeOrientationFix/" + kind + ".prefab";
                if (!File.Exists(backup)) File.Copy(path, backup);
                var actor = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var oldVisual = actor.transform.Find("Visual");
                    if (oldVisual != null) UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
                    var visual = DemoRigFactory.Create(actor, kind);
                    DemoSceneBuilder.Ref(actor.GetComponent<AnimationDriver>(), "visual", visual);
                    var ranged = actor.GetComponent<ProjectileWeapon>();
                    if (ranged != null)
                        DemoSceneBuilder.Ref(ranged, "muzzle", kind == "Boss" ? visual.Find("Rig/Neck/Head/Muzzle") : visual.Find("Rig/Pelvis/Torso/FrontArm/Lower/Hand/Weapon/Muzzle"));
                    PrefabUtility.SaveAsPrefabAsset(actor, path, out bool saved);
                    if (!saved) throw new InvalidOperationException("No se pudo guardar " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(actor); }
            }
            DemoAnimatorFactory.RefreshClips(root);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(scenePath);
            DemoValidation.ValidateAll();
            DemoVisualValidation.RenderPoses();
            File.WriteAllText("Verification/orientation-fix.txt", "Updated original prefab assets and clips in place: " + scenePath);
            Debug.Log("OMT_ORIENTATION_FIX_PASS");
        }
    }
}
#endif
