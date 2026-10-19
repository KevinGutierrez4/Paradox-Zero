#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMoreTime.Editor
{
    public sealed class StageLayoutEditorWindow : EditorWindow
    {
        internal const string Folder = "Assets/OneMoreTime/Levels";
        internal const string LibraryPath = "Assets/OneMoreTime/Resources/OMT_StageLayouts.asset";
        [SerializeField] private int phase = 1;
        [SerializeField] private int kind;
        private static readonly string[] Phases = { "Fase 1", "Fase 2", "Fase 3", "Fase 4", "Fase 5", "Fase 6 · Jefe" };
        private static readonly string[] Labels =
        {
            "Plataforma de hielo", "Plataforma de escamas", "Cobertura de piedra", "Barricada de madera",
            "Cobertura rompible", "Puesto de arquero", "Pinchos de hielo", "Pinchos de hueso", "Conducto venenoso",
            "Trampa de mandíbulas", "Géiser de hielo", "Hielo peligroso", "Grieta", "Plataforma inestable",
            "Plataforma móvil", "Trampa de carámbanos", "Plataforma de escamas que se rompe"
        };

        public static void ShowEditor()
        {
            var window = GetWindow<StageLayoutEditorWindow>("Editor de fases");
            window.minSize = new Vector2(340, 330);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Plataformas y trampas", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Abre una fase, selecciona un objeto en Scene o Hierarchy y muévelo con W. Puedes añadir, duplicar o borrar objetos. Estos mismos prefabs se usan al jugar.", MessageType.Info);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Sal de Play para editar y guardar el nivel.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                phase = EditorGUILayout.Popup("Fase", Mathf.Clamp(phase - 1, 0, 5), Phases) + 1;
                if (GUILayout.Button("Abrir fase en Scene", GUILayout.Height(30))) OpenPhase(phase);
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                bool editing = stage != null && stage.assetPath == PathFor(phase);
                using (new EditorGUI.DisabledScope(!editing))
                {
                    EditorGUILayout.Space(12);
                    kind = EditorGUILayout.Popup("Añadir objeto", kind, Labels);
                    if (GUILayout.Button("Añadir a esta fase")) AddProp(phase, ArenaPropFactory.PrefabKinds[kind]);
                    if (GUILayout.Button("Guardar fase")) EditorApplication.ExecuteMenuItem("File/Save");
                    if (GUILayout.Button("Seleccionar todos los objetos"))
                        Selection.activeGameObject = stage.prefabContentsRoot.transform.Find(StageLayoutLibrary.ContentName).gameObject;
                    if (phase == 6)
                    {
                        EditorGUILayout.Space(8);
                        EditorGUILayout.HelpBox("Dentro de «Jefe y escamas» puedes mover las escamas y la base del jefe. La línea naranja marca el bloqueo que impide subir al montículo.", MessageType.Info);
                        if (GUILayout.Button("Seleccionar jefe y escamas"))
                            Selection.activeGameObject = stage.prefabContentsRoot.GetComponentInChildren<BossArenaLayout>().gameObject;
                        if (GUILayout.Button("Seleccionar bloqueo del jefe"))
                        {
                            var boundary = stage.prefabContentsRoot.GetComponentInChildren<BossArenaLayout>().noClimbBoundary;
                            if (boundary != null) Selection.activeGameObject = boundary.gameObject;
                        }
                        if (GUILayout.Button("Ajustar escamas y base al suelo"))
                            BossArenaAuthoring.SnapToGround(stage.prefabContentsRoot,
                                stage.prefabContentsRoot.transform.Find(StageLayoutLibrary.PreviewName).GetComponent<SerpentSurface>());
                    }
                }
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("Guarda con Ctrl+S o activa Auto Save en el modo Prefab. Para volver al juego usa la flecha de regreso de Hierarchy. El fondo es una referencia; el relieve se edita con «Editar terreno de las fases».", MessageType.None);
            }
        }

        internal static string PathFor(int number) => Folder + "/Fase_" + number.ToString("00") + ".prefab";
        private static RunConfig Config => AssetDatabase.LoadAssetAtPath<RunConfig>(DemoProjectSetup.GameRoot + "/RunConfig.asset");
        internal static float Length(int number) => number == 6 ? Config.bossArenaLength : Config.phases[number - 1].length;

        public static void OpenPhase(int number)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureAssets();
            string path = PathFor(number);
            var current = PrefabStageUtility.GetCurrentPrefabStage();
            if (current == null || current.assetPath != path) RefreshReference(number);
            var stage = PrefabStageUtility.OpenPrefab(path);
            var content = stage.prefabContentsRoot.transform.Find(StageLayoutLibrary.ContentName);
            Selection.activeGameObject = content.gameObject;
            var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            view.in2DMode = true;
            var background = stage.prefabContentsRoot.transform.Find(StageLayoutLibrary.PreviewName).GetComponent<SpriteRenderer>();
            view.Frame(background.bounds, false);
        }

        /// <summary>Creates only missing phase prefabs. Existing authored content is never regenerated.</summary>
        public static void EnsureAssets()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var library = AssetDatabase.LoadAssetAtPath<StageLayoutLibrary>(LibraryPath);
            if (library == null)
            {
                library = CreateInstance<StageLayoutLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            if (library.phasePrefabs == null || library.phasePrefabs.Length != 6) Array.Resize(ref library.phasePrefabs, 6);
            var scene = EditorSceneManager.OpenPreviewScene(DemoProjectSetup.ScenePath);
            try
            {
                var source = FindSurface(scene);
                var props = ArenaPropLibrary.Load();
                var material = source.GetComponent<SpriteRenderer>().sharedMaterial;
                for (int number = 1; number <= 6; number++)
                {
                    string path = PathFor(number);
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (saved == null)
                    {
                        var root = new GameObject("Fase " + number);
                        SceneManager.MoveGameObjectToScene(root, scene);
                        try
                        {
                            var reference = MakeReference(source, root.transform, number);
                            var content = new GameObject(StageLayoutLibrary.ContentName).transform;
                            content.SetParent(root.transform, false);
                            float from = number == 1 ? 16 : 6.5f, to = number == 6 ? 26 : Length(number) - 6;
                            foreach (var placement in StageBuilder.Layout(number, number == 6))
                            {
                                var go = CreateProp(placement.kind, props, material, scene);
                                go.transform.SetParent(content, true);
                                float x = Mathf.Lerp(from, to, placement.t);
                                StageBuilder.PositionProp(go, placement, x, reference.HeightAt(x));
                            }
                            if (number == 1)
                            {
                                foreach (var item in new[] { ("TutorialCrate", 7.5f), ("TutorialSpikes", 12.5f) })
                                {
                                    var go = CreateProp(item.Item1, props, material, scene);
                                    go.transform.SetParent(content, true);
                                    go.transform.position = new Vector3(item.Item2, reference.HeightAt(item.Item2) - .03f, 0);
                                }
                            }
                            saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                            if (saved == null) throw new InvalidOperationException("No se pudo guardar " + path);
                        }
                        finally { DestroyImmediate(root); }
                    }
                    if (number == 6)
                    {
                        BossArenaAuthoring.Ensure(path, source, material);
                        BossArenaAuthoring.EnsureNoClimbBoundary(path, source);
                    }
                    library.phasePrefabs[number - 1] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssetIfDirty(library);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static SerpentSurface FindSurface(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<SerpentSurface>(true);
                if (found != null) return found;
            }
            throw new InvalidOperationException("La escena del juego no contiene el terreno.");
        }

        private static SerpentSurface MakeReference(SerpentSurface source, Transform parent, int number)
        {
            var copy = Instantiate(source);
            copy.name = StageLayoutLibrary.PreviewName;
            if (copy.gameObject.scene != parent.gameObject.scene)
                SceneManager.MoveGameObjectToScene(copy.gameObject, parent.gameObject.scene);
            copy.transform.SetParent(parent, false);
            copy.Configure(number, 0, Length(number));
            copy.GetComponent<EdgeCollider2D>().enabled = false;
            copy.gameObject.tag = "EditorOnly";
            copy.gameObject.hideFlags = HideFlags.NotEditable;
            return copy;
        }

        private static void RefreshReference(int number)
        {
            var scene = EditorSceneManager.OpenPreviewScene(DemoProjectSetup.ScenePath);
            var root = PrefabUtility.LoadPrefabContents(PathFor(number));
            try
            {
                var old = root.transform.Find(StageLayoutLibrary.PreviewName);
                if (old != null) DestroyImmediate(old.gameObject);
                MakeReference(FindSurface(scene), root.transform, number);
                PrefabUtility.SaveAsPrefabAsset(root, PathFor(number));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static GameObject CreateProp(string type, ArenaPropLibrary library, Material material, Scene scene)
        {
            var prefab = library != null ? library.Prefab(type) : null;
            var go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene) : StageBuilder.CreateProp(type, library, material);
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            go.name = type;
            return go;
        }

        internal static GameObject AddProp(int number, string type)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != PathFor(number)) return null;
            var root = stage.prefabContentsRoot.transform;
            var reference = root.Find(StageLayoutLibrary.PreviewName).GetComponent<SerpentSurface>();
            reference.Configure(number, 0, Length(number));
            var go = CreateProp(type, ArenaPropLibrary.Load(), reference.GetComponent<SpriteRenderer>().sharedMaterial, stage.scene);
            go.transform.SetParent(root.Find(StageLayoutLibrary.ContentName), true);
            float x = Length(number) * .5f;
            StageBuilder.PositionProp(go, new StageBuilder.Placement(type, .5f, go.GetComponent<OneWayPlatform>() != null ? 2.2f : 0, 3), x, reference.HeightAt(x));
            Undo.RegisterCreatedObjectUndo(go, "Añadir plataforma o trampa");
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();
            return go;
        }
    }
}
#endif
