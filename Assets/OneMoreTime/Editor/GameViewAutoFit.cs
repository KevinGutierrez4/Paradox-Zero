#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneMoreTime.Editor
{
    /// <summary>The editor's Game-view zoom is independent of Camera and Canvas scaling.</summary>
    [InitializeOnLoad]
    public static class GameViewAutoFit
    {
        private const string Preference = "OMT.GameView.AutoFit";
        private const string Menu = "Tools/One More Time/Ajustar siempre Game a la ventana";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type ViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
        private static double nextUpdate;
        private static bool unavailable;

        static GameViewAutoFit() { EditorApplication.update += Update; }

        [MenuItem(Menu)]
        private static void Toggle() { EditorPrefs.SetBool(Preference, !EditorPrefs.GetBool(Preference, true)); FitAll(); }
        [MenuItem(Menu, true)]
        private static bool Checked() { UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(Preference, true)); return true; }

        private static void Update()
        {
            if (unavailable || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = EditorApplication.timeSinceStartup + 0.15;
            if (EditorPrefs.GetBool(Preference, true)) FitAll();
        }

        public static void FitAll()
        {
            if (ViewType == null) return;
            foreach (var view in Resources.FindObjectsOfTypeAll(ViewType))
                Fit((EditorWindow)view);
        }

        public static bool Fit(EditorWindow view)
        {
            try
            {
                var area = ViewType.GetField("m_ZoomArea", Flags)?.GetValue(view);
                var minimum = ViewType.GetProperty("minScale", Flags);
                if (area == null || minimum == null) return false;
                float fit = (float)minimum.GetValue(view);
                if (fit <= 0 || float.IsNaN(fit) || float.IsInfinity(fit)) return false;
                var areaType = area.GetType();
                var scale = (Vector2)areaType.GetProperty("scale", Flags).GetValue(area);
                var translation = (Vector2)areaType.GetProperty("translation", Flags).GetValue(area);
                if ((scale - Vector2.one * fit).sqrMagnitude > 0.000001f || translation.sqrMagnitude > 0.01f)
                {
                    areaType.GetMethod("SetTransform", Flags, null, new[] { typeof(Vector2), typeof(Vector2) }, null)
                        .Invoke(area, new object[] { Vector2.zero, Vector2.one * fit });
                    view.Repaint();
                }
                return true;
            }
            catch (Exception e)
            {
                if (!unavailable) Debug.LogWarning("No se pudo ajustar la vista Game automáticamente: " + e.GetBaseException().Message);
                unavailable = true;
                return false;
            }
        }
    }
}
#endif
