#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneMoreTime.Editor
{
    public static class DemoVisualValidation
    {
        public static void RenderPoses()
        {
            string scene = EditorBuildSettings.scenes[0].path;
            string root = scene.Substring(0, scene.LastIndexOf("/Scenes/", StringComparison.Ordinal));
            Directory.CreateDirectory("Verification/Poses");
            var previewScene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("PoseCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1.55f;
            camera.transform.position = new Vector3(0, 1.15f, -10); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.22f); camera.scene = previewScene;
            var target = new RenderTexture(512, 512, 24); camera.targetTexture = target;
            string[] poses = { "Idle", "WalkContact", "WalkPassing", "Jump", "Fall", "Crouch", "Windup", "Impact", "Recovery", "Death" };
            try
            {
                foreach (string kind in new[] { "Player", "Archer", "Mage", "WhiteWolf", "BlueWolf", "Boss" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Prefabs/" + kind + ".prefab");
                    var actor = UnityEngine.Object.Instantiate(prefab); actor.transform.position = Vector3.zero;
                    var visual = actor.transform.Find("Visual");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, previewScene);
                    var animator = actor.GetComponent<Animator>(); if (animator != null) animator.enabled = false;
                    var rig = actor.GetComponent<ArticulatedRig>(); rig.Bind();
                    camera.orthographicSize = kind == "Boss" ? 2.1f : 1.55f;
                    for (int i = 0; i < poses.Length; i++)
                    {
                        SampleClip(actor, root, kind, i == 1 || i == 2 ? "Walk" : i == 3 ? "Jump" : i == 4 ? "Fall"
                            : i == 5 ? "Crouch" : i >= 6 && i <= 8 ? "Attack" : i == 9 ? "Die" : "Idle",
                            i == 2 ? 0.25f : i == 6 ? 0.28f : i == 7 ? 0.43f : i == 8 ? 0.68f : i == 9 ? 0.98f : 0);
                        camera.Render(); camera.Render();
                        var previous = RenderTexture.active; RenderTexture.active = target;
                        var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
                        texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); texture.Apply();
                        File.WriteAllBytes("Verification/Poses/" + kind + "_" + poses[i] + ".png", texture.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(texture); RenderTexture.active = previous;
                    }
                    Directory.CreateDirectory("Verification/Directions");
                    foreach (int direction in new[] { -1, 1 })
                    {
                        visual.localScale = new Vector3(direction, 1, 1);
                        foreach (bool attacking in new[] { false, true })
                        {
                            SampleClip(actor, root, kind, attacking ? "Attack" : "Walk", attacking ? 0.43f : 0);
                            camera.Render(); camera.Render();
                            var previous = RenderTexture.active; RenderTexture.active = target;
                            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
                            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                            File.WriteAllBytes("Verification/Directions/" + kind + "_" + (direction < 0 ? "Left" : "Right") + "_" + (attacking ? "Attack" : "Walk") + ".png", image.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = previous;
                        }
                    }
                    visual.localScale = Vector3.one;
                    if (kind == "Player")
                    {
                        Directory.CreateDirectory("Verification/Motion");
                        for (int frame = 0; frame < 48; frame++)
                        {
                            bool walking = frame < 24; float t = walking ? frame / 24f : (frame - 24) / 23f;
                            SampleClip(actor, root, kind, walking ? "Walk" : "Attack", t);
                            camera.Render(); camera.Render();
                            var previous = RenderTexture.active; RenderTexture.active = target;
                            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
                            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                            File.WriteAllBytes("Verification/Motion/" + (walking ? "walk_" : "axe_") + (walking ? frame : frame - 24).ToString("00") + ".png", image.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = previous;
                        }
                    }
                    UnityEngine.Object.DestroyImmediate(actor);
                }
            }
            finally { camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(previewScene); }
            Debug.Log("OMT_POSES_EXPORTED");
        }

        private static void SampleClip(GameObject actor, string root, string kind, string state, float normalized)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "/Animations/" + kind + "/" + state + ".anim")
                ?? AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "/Animations/" + kind + "/Idle.anim");
            if (clip != null) clip.SampleAnimation(actor, clip.length * normalized);
        }
    }
}
#endif
