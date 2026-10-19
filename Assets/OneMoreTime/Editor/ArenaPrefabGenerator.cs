#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OneMoreTime.Editor
{
    /// <summary>
    /// Saves the reusable scenery prefabs (platforms, covers, traps, archer posts) built from the new atlases into
    /// Assets/OneMoreTime/Prefabs/Environment and registers them in the Resources library. Touches nothing else:
    /// no scene, no existing prefab, no old Generated folder.
    /// </summary>
    public static class ArenaPrefabGenerator
    {
        private const string Folder = "Assets/OneMoreTime/Prefabs/Environment";
        private const string LibraryPath = "Assets/OneMoreTime/Resources/" + ArenaPropLibrary.ResourceName + ".asset";
        private const string MaterialPath = "Assets/OneMoreTime/Generated_Hacha_Final 2/SpritesUnlit.mat";
        public static void Generate()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArenaPropLibrary>(LibraryPath);
            if (library == null) { Debug.LogError("Falta " + LibraryPath); return; }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!AssetDatabase.IsValidFolder("Assets/OneMoreTime/Prefabs")) AssetDatabase.CreateFolder("Assets/OneMoreTime", "Prefabs");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/OneMoreTime/Prefabs", "Environment");
            int count = 0;
            foreach (string kind in ArenaPropFactory.PrefabKinds)
            {
                var go = ArenaPropFactory.Create(kind, library, material);
                string path = Folder + "/" + kind + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
                library.SetPrefab(kind, prefab);
                count++;
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("OMT_ENV_PREFABS " + count + " prefabs en " + Folder);
        }
    }
}
#endif
