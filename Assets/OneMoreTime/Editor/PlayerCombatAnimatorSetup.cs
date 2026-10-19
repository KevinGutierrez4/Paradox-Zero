#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OneMoreTime.Editor
{
    /// <summary>Adds missing shield/leg animation support without rebuilding existing clips.</summary>
    [InitializeOnLoad]
    public static class PlayerCombatAnimatorSetup
    {
        static PlayerCombatAnimatorSetup()
        {
            EditorApplication.delayCall += Ensure;
            EditorApplication.playModeStateChanged += state =>
            { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Ensure; };
        }

        public static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += Ensure; return; }
            string directory = DemoProjectSetup.GameRoot + "/Animations/Player";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(directory + "/Actor.controller");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemoProjectSetup.GameRoot + "/Prefabs/Player.prefab");
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(directory + "/Idle.anim");
            if (controller == null || prefab == null || idle == null) return;
            bool changed = false;
            var block = AssetDatabase.LoadAssetAtPath<AnimationClip>(directory + "/Block.anim");
            if (block == null)
            {
                block = BakeBlock(prefab, idle);
                AssetDatabase.CreateAsset(block, directory + "/Block.anim");
                changed = true;
            }
            bool hasBlocking = false;
            foreach (var parameter in controller.parameters) hasBlocking |= parameter.name == "Blocking";
            if (!hasBlocking) { controller.AddParameter("Blocking", AnimatorControllerParameterType.Bool); changed = true; }
            var machine = controller.layers[0].stateMachine;
            AnimatorState blockState = null, idleState = null;
            foreach (var state in machine.states)
            { if (state.state.name == "Block") blockState = state.state; if (state.state.name == "Idle") idleState = state.state; }
            if (blockState == null)
            {
                blockState = machine.AddState("Block"); blockState.motion = block; blockState.writeDefaultValues = true;
                var enter = machine.AddAnyStateTransition(blockState);
                enter.hasExitTime = false; enter.duration = 0.04f; enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0, "Blocking");
                enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                if (idleState != null)
                {
                    var exit = blockState.AddTransition(idleState); exit.hasExitTime = false; exit.duration = 0.04f;
                    exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Blocking");
                    exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                }
                changed = true;
            }
            bool hasLegLayer = false;
            foreach (var layer in controller.layers) hasLegLayer |= layer.name == "LowerBodyLocomotion";
            if (!hasLegLayer)
            {
                string maskPath = directory + "/LocomotionLegs.mask";
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
                if (mask == null)
                {
                    mask = new AvatarMask { name = "LocomotionLegs" };
                    for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                    var nodes = prefab.GetComponentsInChildren<Transform>(true);
                    mask.transformCount = nodes.Length;
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        string path = AnimationUtility.CalculateTransformPath(nodes[i], prefab.transform);
                        mask.SetTransformPath(i, path);
                        mask.SetTransformActive(i, path.StartsWith("Visual/Rig/Pelvis/FrontLeg") || path.StartsWith("Visual/Rig/Pelvis/BackLeg"));
                    }
                    AssetDatabase.CreateAsset(mask, maskPath);
                }
                var legs = new AnimatorStateMachine { name = "LowerBodyLocomotion", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(legs, controller);
                var walk = legs.AddState("Walk");
                walk.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(directory + "/Walk.anim");
                walk.writeDefaultValues = true; legs.defaultState = walk;
                controller.AddLayer(new AnimatorControllerLayer { name = "LowerBodyLocomotion", stateMachine = legs,
                    avatarMask = mask, defaultWeight = 0, blendingMode = AnimatorLayerBlendingMode.Override });
                changed = true;
            }
            if (changed) { EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets(); }
        }

        private static AnimationClip BakeBlock(GameObject prefab, AnimationClip idle)
        {
            var clip = Object.Instantiate(idle); clip.name = "Player_Block";
            var actor = Object.Instantiate(prefab); actor.hideFlags = HideFlags.HideAndDontSave;
            actor.GetComponent<Animator>().enabled = false;
            string root = "Visual/Rig/Pelvis/Torso/BackArm";
            string[] paths = { root, root + "/Lower", root + "/Lower/Hand", root + "/Lower/Hand/ShieldSocket" };
            var curves = new Dictionary<string, AnimationCurve>();
            foreach (string path in paths) curves[path] = new AnimationCurve();
            try
            {
                int count = Mathf.Max(1, Mathf.CeilToInt(idle.length * 60));
                for (int frame = 0; frame <= count; frame++)
                {
                    float t = idle.length * frame / count;
                    idle.SampleAnimation(actor, t);
                    var pelvis = actor.transform.Find("Visual/Rig/Pelvis");
                    var torso = pelvis.Find("Torso");
                    float wrist = -Mathf.DeltaAngle(0, pelvis.localEulerAngles.z) - Mathf.DeltaAngle(0, torso.localEulerAngles.z) + 15 - 175 - 8;
                    float[] angles = { -15, 175, wrist, 0 };
                    for (int i = 0; i < paths.Length; i++)
                        if (actor.transform.Find(paths[i]) != null) curves[paths[i]].AddKey(t, angles[i]);
                }
                foreach (var pair in curves)
                    if (pair.Value.length > 0) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pair.Key, typeof(Transform), "localEulerAnglesRaw.z"), pair.Value);
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                return clip;
            }
            finally { Object.DestroyImmediate(actor); }
        }
    }
}
#endif
