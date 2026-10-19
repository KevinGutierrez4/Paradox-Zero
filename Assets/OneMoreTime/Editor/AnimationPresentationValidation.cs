#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;

namespace OneMoreTime.Editor
{
    public static class AnimationPresentationValidation
    {
        public const string Output = "Verification/AnimationReview";

        public static void PrepareAndRun()
        {
            PrepareEnemyAnimations();
            StartupCameraValidation.Run();
        }

        public static void PrepareEnemyAnimations()
        {
            Directory.CreateDirectory(Output);
            var report = new List<string>();
            foreach (string kind in new[] { "Archer", "Mage", "WhiteWolf", "BlueWolf", "Boss" })
            {
                var kept = new HashSet<string> { "Idle", "Walk", "Attack", "Die" };
                if (kind == "Boss") { kept.Remove("Walk"); kept.Add("Special"); }
                string folder = DemoProjectSetup.GameRoot + "/Animations/" + kind;
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "/Actor.controller");
                foreach (var layer in controller.layers)
                {
                    var machine = layer.stateMachine;
                    foreach (var transition in machine.anyStateTransitions)
                        if (transition.destinationState != null && !kept.Contains(transition.destinationState.name))
                            machine.RemoveAnyStateTransition(transition);
                    foreach (var child in machine.states)
                    {
                        foreach (var transition in child.state.transitions)
                            if (transition.destinationState != null && !kept.Contains(transition.destinationState.name))
                                child.state.RemoveTransition(transition);
                    }
                    foreach (var child in machine.states)
                        if (!kept.Contains(child.state.name)) { report.Add(kind + ": removed unused state " + child.state.name); machine.RemoveState(child.state); }
                    foreach (var child in machine.states)
                    {
                        child.state.writeDefaultValues = true;
                        EditorUtility.SetDirty(child.state);
                    }
                    EditorUtility.SetDirty(machine);
                }
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                foreach (string state in new[] { "Walk", "Jump", "Fall", "Crouch", "CrouchAttack", "Special", "Block" })
                {
                    if (kept.Contains(state)) continue;
                    string path = folder + "/" + state + ".anim";
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == null) continue;
                    bool referenced = false;
                    foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController"))
                        foreach (string dependency in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid)))
                            if (dependency == path) referenced = true;
                    if (referenced) throw new InvalidOperationException("Clip still used by a controller: " + path);
                    AssetDatabase.DeleteAsset(path);
                    report.Add("Removed unreferenced clip: " + path);
                }
            }
            var playerController = AssetDatabase.LoadAssetAtPath<AnimatorController>(DemoProjectSetup.GameRoot + "/Animations/Player/Actor.controller");
            foreach (var layer in playerController.layers)
                foreach (var child in layer.stateMachine.states)
                { child.state.writeDefaultValues = true; EditorUtility.SetDirty(child.state); }
            EditorUtility.SetDirty(playerController);
            foreach (string kind in new[] { "Archer", "Mage", "WhiteWolf", "BlueWolf", "Player" })
            {
                string path = DemoProjectSetup.GameRoot + "/Prefabs/" + kind + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animator = prefab.GetComponent<Animator>();
                    if (!animator.enabled) { animator.enabled = true; PrefabUtility.SaveAsPrefabAsset(prefab, path); }
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            report.Add("Authored states reset unkeyed properties to prefab defaults; clip keys are preserved.");
            File.WriteAllLines(Output + "/unused-animation-cleanup.txt", report);
            AssetDatabase.SaveAssets();
            Inspect();
        }

        public static IEnumerator CheckRuntime(Action<bool, string> check)
        {
            foreach (string kind in new[] { "Archer", "Mage", "WhiteWolf", "BlueWolf", "Player" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemoProjectSetup.GameRoot + "/Prefabs/" + kind + ".prefab");
                var actor = UnityEngine.Object.Instantiate(prefab, new Vector3(-1000, 0, 0), Quaternion.identity);
                AnimationClip edited = null;
                AnimatorOverrideController controller = null;
                try
                {
                    var ai = actor.GetComponent<EnemyAI>(); if (ai != null) ai.enabled = false;
                    actor.GetComponent<Rigidbody2D>().simulated = false;
                    actor.GetComponent<Health>().ResetHealth(100, kind == "Player" ? Team.Player : Team.Enemy);
                    var animator = actor.GetComponent<Animator>();
                    var driver = actor.GetComponent<AnimationDriver>();
                    var rig = actor.GetComponent<ArticulatedRig>();
                    driver.SetMotion(0, true, false, 0);
                    check(animator.enabled && driver.UsesAuthoredAnimations && !driver.RigOwnsPose,
                        kind + ": prefab Animator owns the runtime pose");
                    var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(DemoProjectSetup.GameRoot + "/Animations/" + kind + "/Idle.anim");
                    edited = UnityEngine.Object.Instantiate(idle);
                    string path = kind.Contains("Wolf") ? "Visual/Rig/Neck/Head" : "Visual/Rig/Pelvis/Torso/Head";
                    edited.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.Constant(0, idle.length, 37));
                    controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
                    controller[idle.name] = edited;
                    animator.runtimeAnimatorController = controller;
                    animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                    animator.Play("Base Layer.Idle", 0, 0.35f); animator.Update(0);
                    yield return null; yield return null;
                    float angle = Mathf.DeltaAngle(0, actor.transform.Find(path).localEulerAngles.z);
                    check(Mathf.Abs(angle - 37) < 0.1f,
                        kind + ": an edited clip key survives runtime LateUpdate (head=" + angle.ToString("F2") + ")");
                    animator.Play("Base Layer.Walk", 0, 0.25f); animator.Update(0);
                    animator.Play("Base Layer.Attack", 0, 0.4f); animator.Update(0);
                    var sprites = actor.GetComponentsInChildren<SpriteRenderer>(true);
                    var expectedPositions = Array.ConvertAll(sprites, sprite => sprite.transform.localPosition);
                    var expectedRotations = Array.ConvertAll(sprites, sprite => sprite.transform.localRotation);
                    animator.Play("Base Layer.Walk", 0, 0.85f); animator.Update(0);
                    animator.Play("Base Layer.Attack", 0, 0.4f); animator.Update(0);
                    bool attached = true;
                    for (int i = 0; i < sprites.Length; i++)
                        attached &= Vector3.Distance(expectedPositions[i], sprites[i].transform.localPosition) < 0.0001f
                            && Quaternion.Angle(expectedRotations[i], sprites[i].transform.localRotation) < 0.01f;
                    check(attached, kind + ": attachment positions do not inherit a previous walk frame");
                    if (ai != null)
                    {
                        var clock = (AttackTimeline)typeof(EnemyAI).GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ai);
                        clock.Begin(new AttackTiming(1, 1, 1)); clock.Tick(0.5f);
                        driver.SynchronizeCombatPose();
                        check(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - 0.3f) < 0.002f,
                            kind + ": edited Attack clip follows the anticipation clock");
                        clock.Tick(0.5f); driver.SynchronizeCombatPose();
                        check(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - 0.6f) < 0.002f,
                            kind + ": release/hit uses the authored active frame");
                        clock.Cancel(); driver.SynchronizeCombatPose();
                        check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), kind + ": canceled attack returns to locomotion");
                    }
                }
                finally
                {
                    UnityEngine.Object.Destroy(actor);
                    if (controller != null) UnityEngine.Object.Destroy(controller);
                    if (edited != null) UnityEngine.Object.Destroy(edited);
                }
            }
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(gameViewType);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var originalRect = view.position;
            try
            {
                foreach (var size in new[] { new Vector2(1390, 580), new Vector2(640, 360), new Vector2(400, 900) })
                {
                    view.position = new Rect(0, 0, size.x, size.y);
                    view.Repaint(); yield return null;
                    var area = gameViewType.GetField("m_ZoomArea", flags).GetValue(view);
                    area.GetType().GetMethod("SetTransform", flags).Invoke(area, new object[] { new Vector2(120, 75), Vector2.one * 1.7f });
                    bool fitted = GameViewAutoFit.Fit(view);
                    var scale = (Vector2)area.GetType().GetProperty("scale", flags).GetValue(area);
                    float minimum = (float)gameViewType.GetProperty("minScale", flags).GetValue(view);
                    var translation = (Vector2)area.GetType().GetProperty("translation", flags).GetValue(area);
                    check(fitted && Mathf.Abs(scale.x - minimum) < 0.001f && translation.sqrMagnitude < 0.01f,
                        "Game window " + size + ": 1.7x zoom and panning reset to fit (" + scale + ")");
                }
            }
            finally { view.position = originalRect; }
        }

        public static void Inspect()
        {
            Directory.CreateDirectory(Output);
            var scene = EditorSceneManager.NewPreviewScene();
            var log = new List<string>();
            var cameraObject = new GameObject("AnimationReviewCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.19f, 0.23f);
            const int cell = 320;
            var target = new RenderTexture(cell, cell, 24);
            camera.targetTexture = target;
            try
            {
                foreach (string kind in new[] { "Archer", "Mage", "WhiteWolf", "BlueWolf", "Player", "Boss" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemoProjectSetup.GameRoot + "/Prefabs/" + kind + ".prefab");
                    var actor = UnityEngine.Object.Instantiate(prefab);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, scene);
                    actor.transform.position = Vector3.zero;
                    actor.GetComponent<Animator>().enabled = false;
                    var nodes = actor.GetComponentsInChildren<Transform>();
                    var positions = Array.ConvertAll(nodes, node => node.localPosition);
                    var rotations = Array.ConvertAll(nodes, node => node.localRotation);
                    var scales = Array.ConvertAll(nodes, node => node.localScale);
                    var sheet = new Texture2D(cell * 4, cell * 3, TextureFormat.RGB24, false);
                    int index = 0;
                    foreach (string state in new[] { "Idle", "Walk", "Attack" })
                    {
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DemoProjectSetup.GameRoot + "/Animations/" + kind + "/" + state + ".anim")
                            ?? AssetDatabase.LoadAssetAtPath<AnimationClip>(DemoProjectSetup.GameRoot + "/Animations/" + kind + "/Idle.anim");
                        foreach (float normalized in new[] { 0f, 0.25f, 0.6f, 0.85f })
                        {
                            for (int n = 0; n < nodes.Length; n++)
                            { nodes[n].localPosition = positions[n]; nodes[n].localRotation = rotations[n]; nodes[n].localScale = scales[n]; }
                            clip.SampleAnimation(actor, clip.length * normalized);
                            camera.orthographicSize = kind == "Boss" ? 2.4f : 1.5f;
                            camera.transform.position = new Vector3(0, kind.Contains("Wolf") ? 0.75f : 1.1f, -10);
                            camera.Render(); camera.Render();
                            var previous = RenderTexture.active;
                            RenderTexture.active = target;
                            sheet.ReadPixels(new Rect(0, 0, cell, cell), index % 4 * cell, (2 - index / 4) * cell);
                            RenderTexture.active = previous;
                            index++;
                        }
                    }
                    sheet.Apply(); File.WriteAllBytes(Output + "/" + kind + "-clips.png", sheet.EncodeToPNG());
                    foreach (var node in actor.GetComponentsInChildren<Transform>())
                    {
                        var sprite = node.GetComponent<SpriteRenderer>();
                        log.Add(kind + "/" + AnimationUtility.CalculateTransformPath(node, actor.transform)
                            + " pos=" + node.localPosition.ToString("F3") + " scale=" + node.localScale.ToString("F3")
                            + (sprite != null && sprite.sprite != null ? " sprite=" + sprite.sprite.name + " bounds=" + sprite.bounds : ""));
                    }
                    UnityEngine.Object.DestroyImmediate(sheet);
                    UnityEngine.Object.DestroyImmediate(actor);
                }
                File.WriteAllLines(Output + "/rig-diagnostics.txt", log);
                Debug.Log("OMT_ANIMATION_INSPECTION_PASS");
            }
            finally
            {
                camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
