#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneMoreTime.Editor
{
    [CustomEditor(typeof(SerpentSurface))]
    public sealed class SerpentSurfaceEditor : UnityEditor.Editor
    {
        private static readonly string[] PhaseNames = { "Fase 1", "Fase 2", "Fase 3", "Fase 4", "Fase 5", "Fase 6 · Jefe" };

        [MenuItem("Tools/One More Time/Editar plataformas y trampas")]
        private static void EditPlacements()
        {
            StageLayoutEditorWindow.ShowEditor();
        }

        [MenuItem("Tools/One More Time/Editar terreno de las fases")]
        private static void SelectSurface()
        {
            var surface = Object.FindAnyObjectByType<SerpentSurface>();
            if (!EditorApplication.isPlaying && (surface == null || surface.gameObject.scene.path != GameProjectStartup.ScenePath))
            {
                GameProjectStartup.OpenGame();
                surface = Object.FindAnyObjectByType<SerpentSurface>();
            }
            if (surface != null) Selection.activeGameObject = surface.gameObject;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Cada perfil define la imagen y la línea de suelo. Las plataformas y trampas se editan visualmente con el botón de abajo.", MessageType.Info);
            if (GUILayout.Button("Abrir distribución de plataformas y trampas")) EditPlacements();
            var phase = serializedObject.FindProperty("previewPhase");
            EditorGUI.BeginChangeCheck();
            phase.intValue = EditorGUILayout.Popup("Fase para editar", Mathf.Clamp(phase.intValue - 1, 0, 5), PhaseNames) + 1;
            bool changedPhase = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changedPhase && !EditorApplication.isPlaying) RefreshPreview();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Mostrar fase y editar suelo en Scene"))
                {
                    RefreshPreview();
                    var view = SceneView.lastActiveSceneView;
                    if (view != null) view.Frame(((SerpentSurface)target).GetComponent<SpriteRenderer>().bounds, false);
                }
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("phaseProfiles"), new GUIContent("Perfiles de las seis fases"), true);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    RefreshPreview();
                }
            }
            EditorGUILayout.HelpBox("Los puntos verdes se pueden arrastrar con este objeto seleccionado. Guarda la escena fuera de Play para conservar tus cambios.", MessageType.None);
        }

        internal void RefreshPreview()
        {
            serializedObject.Update();
            var surface = (SerpentSurface)target;
            int phase = serializedObject.FindProperty("previewPhase").intValue;
            var config = AssetDatabase.LoadAssetAtPath<RunConfig>(DemoProjectSetup.GameRoot + "/RunConfig.asset");
            float length = config == null ? 48 : phase == 6 ? config.bossArenaLength : config.phases[phase - 1].length;
            Undo.RecordObjects(new Object[] { surface, surface.transform, surface.GetComponent<SpriteRenderer>(), surface.GetComponent<EdgeCollider2D>() }, "Previsualizar fase");
            surface.Configure(phase, 0, length);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            if (EditorApplication.isPlaying) return;
            serializedObject.Update();
            int index = serializedObject.FindProperty("previewPhase").intValue - 1;
            var profiles = serializedObject.FindProperty("phaseProfiles");
            if (index < 0 || index >= profiles.arraySize) return;
            var profile = profiles.GetArrayElementAtIndex(index);
            var sprite = profile.FindPropertyRelative("image").objectReferenceValue as Sprite;
            if (sprite == null || ((SerpentSurface)target).GetComponent<SpriteRenderer>().sprite != sprite) return;
            var points = profile.FindPropertyRelative("points");
            var surface = (SerpentSurface)target;
            Handles.color = Color.green;
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = points.GetArrayElementAtIndex(i);
                Vector3 position = surface.transform.TransformPoint(Vector2.Scale(point.vector2Value - Vector2.one * .5f, sprite.bounds.size));
                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(position, HandleUtility.GetHandleSize(position) * .06f, Vector3.zero, Handles.DotHandleCap);
                if (!EditorGUI.EndChangeCheck()) continue;
                Vector3 local = surface.transform.InverseTransformPoint(moved);
                float minX = i == 0 ? 0 : points.GetArrayElementAtIndex(i - 1).vector2Value.x + .001f;
                float maxX = i == points.arraySize - 1 ? 1 : points.GetArrayElementAtIndex(i + 1).vector2Value.x - .001f;
                point.vector2Value = new Vector2(Mathf.Clamp(local.x / sprite.bounds.size.x + .5f, minX, maxX), Mathf.Clamp01(local.y / sprite.bounds.size.y + .5f));
                serializedObject.ApplyModifiedProperties();
                RefreshPreview();
                break;
            }
        }
    }
}
#endif
