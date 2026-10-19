#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMoreTime.Editor
{
    internal static class BossArenaAuthoring
    {
        // Only migrates arenas which have never had authored boss placement. User edits are never rebuilt.
        internal static void Ensure(string path, SerpentSurface surface, Material material)
        {
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (saved == null || saved.GetComponentInChildren<BossArenaLayout>(true) != null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var art = AssetDatabase.LoadAssetAtPath<GameObject>(DemoProjectSetup.GameRoot + "/Prefabs/Boss.prefab").GetComponent<JormungandrBody>();
                surface.Configure(6, 0, StageLayoutEditorWindow.Length(6));
                var arena = Child("Jefe y escamas", root.transform).AddComponent<BossArenaLayout>();
                var origin = Child("Base del jefe", arena.transform).transform;
                float x = StageLayoutEditorWindow.Length(6) - 3;
                origin.position = new Vector3(x, surface.HeightAt(x), 0);
                arena.neckBase = origin;
                arena.moundCollider = Mound("Montículo sólido", origin, -.1f, new Vector2(-1.05f, 1), art.MoundSprite, surface, material);
                Mound("Montículo lateral", origin, 1.6f, new Vector2(.8f, .8f), art.MoundSideSprite, surface, material);
                float front = x - 2.3f;
                var scales = Child("Escamas sobre el suelo", arena.transform).transform;
                for (int i = 0; i < art.DefaultPlateCount; i++)
                {
                    var anchor = Child("Escama " + (i + 1).ToString("00"), scales).AddComponent<BossScaleAnchor>();
                    float px = Mathf.Lerp(front - 1.4f, 2.4f, (float)i / Mathf.Max(1, art.DefaultPlateCount - 1));
                    anchor.transform.position = new Vector3(px, surface.HeightAt(px), 0);
                    float size = .62f + .1f * ((i * 5) % 3);
                    anchor.transform.localScale = new Vector3((i % 2 == 0 ? 1 : -1) * size, size, 1);
                    anchor.sprite = art.PlateSprite(i);
                    var preview = Child("Referencia de la escama", anchor.transform);
                    preview.tag = "EditorOnly";
                    var renderer = preview.AddComponent<SpriteRenderer>();
                    renderer.sprite = anchor.sprite; renderer.sharedMaterial = material; renderer.sortingOrder = -8;
                    renderer.color = new Color(.86f, .93f, 1);
                    anchor.preview = renderer;
                    preview.transform.position = anchor.RenderPosition(anchor.sprite);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static void EnsureNoClimbBoundary(string path, SerpentSurface surface)
        {
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var existing = saved != null ? saved.GetComponentInChildren<BossArenaLayout>(true) : null;
            if (existing == null || existing.noClimbBoundary != null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var arena = root.GetComponentInChildren<BossArenaLayout>();
                surface.Configure(6, 0, StageLayoutEditorWindow.Length(6));
                Physics2D.SyncTransforms();
                float x = arena.moundCollider.bounds.min.x;
                var wall = Child("Bloqueo del jefe (no escalable)", arena.neckBase);
                wall.layer = LayerMask.NameToLayer("OMT_Prop");
                wall.transform.position = new Vector3(x, surface.HeightAt(x) - 2, 0);
                var collider = wall.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(.16f, 26);
                collider.offset = new Vector2(0, 13);
                collider.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(DemoProjectSetup.GameRoot + "/SinFriccion.physicsMaterial2D");
                arena.noClimbBoundary = collider;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Collider2D Mound(string name, Transform origin, float offsetX, Vector2 scale, Sprite sprite, SerpentSurface surface, Material material)
        {
            var go = Child(name, origin); go.layer = LayerMask.NameToLayer("OMT_Prop");
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingOrder = -4;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1);
            float x = origin.position.x + offsetX;
            go.transform.position = new Vector3(x, surface.HeightAt(x) - sprite.bounds.min.y * scale.y - .08f, 0);
            var collider = go.AddComponent<PolygonCollider2D>();
            int count = sprite.GetPhysicsShapeCount();
            if (count > 0)
            {
                collider.pathCount = count;
                var points = new List<Vector2>();
                for (int i = 0; i < count; i++) { sprite.GetPhysicsShape(i, points); collider.SetPath(i, points); }
            }
            return collider;
        }

        internal static void SnapToGround(GameObject root, SerpentSurface surface)
        {
            var arena = root.GetComponentInChildren<BossArenaLayout>();
            if (arena == null) return;
            surface.Configure(6, 0, StageLayoutEditorWindow.Length(6));
            var transforms = new List<Object>();
            foreach (var scale in arena.Scales) transforms.Add(scale.transform);
            if (arena.neckBase != null) transforms.Add(arena.neckBase);
            Undo.RecordObjects(transforms.ToArray(), "Ajustar jefe y escamas al suelo");
            foreach (var item in transforms)
            {
                var t = (Transform)item;
                t.position = new Vector3(t.position.x, surface.HeightAt(t.position.x), t.position.z);
            }
            EditorSceneManager.MarkSceneDirty(root.scene);
        }
    }
}
#endif
