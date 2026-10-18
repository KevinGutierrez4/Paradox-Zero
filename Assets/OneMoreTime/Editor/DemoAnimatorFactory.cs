#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OneMoreTime.Editor
{
    internal static class DemoAnimatorFactory
    {
        internal static void RefreshHero(string root)
        {
            string directory = root + "/Animations/Player";
            foreach (string state in new[] { "Idle", "Walk", "Crouch", "Jump", "Fall", "Attack", "CrouchAttack", "Special", "Die", "Block" })
            {
                string path = directory + "/" + state + ".anim";
                var source = Clip(state, "Player");
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) AssetDatabase.CreateAsset(source, path);
                else { EditorUtility.CopySerialized(source, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(source); }
            }
            AddHeroFall(AssetDatabase.LoadAssetAtPath<AnimatorController>(directory + "/Actor.controller"), AssetDatabase.LoadAssetAtPath<AnimationClip>(directory + "/Fall.anim"));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(directory + "/Actor.controller");
            bool hasBlock = false; foreach (var item in controller.layers[0].stateMachine.states) if (item.state.name == "Block") hasBlock = true;
            if (!hasBlock)
            {
                controller.AddParameter("Blocking", AnimatorControllerParameterType.Bool);
                var state = controller.layers[0].stateMachine.AddState("Block"); state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(directory + "/Block.anim");
                var enter = controller.layers[0].stateMachine.AddAnyStateTransition(state); Instant(enter);
                enter.AddCondition(AnimatorConditionMode.If, 0, "Blocking"); enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                foreach (var item in controller.layers[0].stateMachine.states) if (item.state.name == "Idle")
                { var exit = state.AddTransition(item.state); Instant(exit); exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Blocking"); }
                EditorUtility.SetDirty(controller);
            }
        }
        private static void AddHeroFall(AnimatorController controller, AnimationClip clip)
        {
            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>(); foreach (var item in machine.states) states[item.state.name] = item.state;
            if (states.ContainsKey("Fall")) { states["Fall"].motion = clip; return; }
            var fall = machine.AddState("Fall"); fall.motion = clip; fall.writeDefaultValues = false;
            var enter = states["Jump"].AddTransition(fall); Instant(enter);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); enter.AddCondition(AnimatorConditionMode.Less, 0, "VerticalSpeed");
            foreach (string state in new[] { "Idle", "Walk", "Crouch", "Jump" })
            {
                var tr = fall.AddTransition(states[state]); Instant(tr);
                tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                tr.AddCondition(state == "Jump" ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 0, "Grounded");
                if (state == "Jump") tr.AddCondition(AnimatorConditionMode.Greater, 0, "VerticalSpeed");
                else
                {
                    tr.AddCondition(state == "Crouch" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Crouch");
                    if (state != "Crouch") tr.AddCondition(state == "Walk" ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0.1f, "Speed");
                }
            }
            EditorUtility.SetDirty(controller);
        }
        internal static void RefreshClips(string root)
        {
            foreach (string kind in new[] { "Player", "Archer", "Mage", "WhiteWolf", "BlueWolf", "Boss" })
                foreach (string state in new[] { "Idle", "Walk", "Crouch", "Jump", "Attack", "CrouchAttack", "Special", "Die" })
                {
                    var destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "/Animations/" + kind + "/" + state + ".anim");
                    var source = Clip(state, kind);
                    EditorUtility.CopySerialized(source, destination); EditorUtility.SetDirty(destination);
                    Object.DestroyImmediate(source);
                }
        }
        internal static AnimatorController Create(string directory, string kind)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(directory + "/Actor.controller");
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Pattern", AnimatorControllerParameterType.Int);
            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            foreach (string name in new[] { "Idle", "Walk", "Crouch", "Jump", "Attack", "CrouchAttack", "Special", "Die" })
            {
                var clip = Clip(name, kind);
                AssetDatabase.CreateAsset(clip, directory + "/" + name + ".anim");
                var state = machine.AddState(name);
                state.motion = clip; state.writeDefaultValues = false;
                states.Add(name, state);
            }
            machine.defaultState = states["Idle"];
            var death = machine.AddAnyStateTransition(states["Die"]);
            Instant(death); death.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            var special = machine.AddAnyStateTransition(states["Special"]);
            Instant(special); special.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            special.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            special.AddCondition(AnimatorConditionMode.Equals, 2, "Pattern");
            var attack = machine.AddAnyStateTransition(states["Attack"]);
            Instant(attack); attack.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            attack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            attack.AddCondition(AnimatorConditionMode.NotEqual, 2, "Pattern");
            attack.AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch");
            var crouchAttack = machine.AddAnyStateTransition(states["CrouchAttack"]);
            Instant(crouchAttack); crouchAttack.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            crouchAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            crouchAttack.AddCondition(AnimatorConditionMode.NotEqual, 2, "Pattern");
            crouchAttack.AddCondition(AnimatorConditionMode.If, 0, "Crouch");
            foreach (string from in new[] { "Idle", "Walk", "Crouch", "Jump" })
            {
                foreach (string to in new[] { "Idle", "Walk", "Crouch", "Jump" })
                {
                    if (from == to) continue;
                    var transition = states[from].AddTransition(states[to]);
                    Instant(transition);
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                    if (to == "Jump") transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
                    else
                    {
                        transition.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
                        transition.AddCondition(to == "Crouch" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Crouch");
                        if (to != "Crouch") transition.AddCondition(to == "Walk" ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0.1f, "Speed");
                    }
                }
            }
            foreach (string from in new[] { "Attack", "CrouchAttack", "Special" })
            {
                var exit = states[from].AddTransition(states["Idle"]);
                exit.hasExitTime = true; exit.exitTime = 1; exit.duration = 0.02f;
                exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            EditorUtility.SetDirty(controller);
            if (kind == "Player")
            {
                var fall = Clip("Fall", kind); AssetDatabase.CreateAsset(fall, directory + "/Fall.anim"); AddHeroFall(controller, fall);
            }
            return controller;
        }

        private static void Instant(AnimatorStateTransition transition)
        { transition.hasExitTime = false; transition.duration = 0.025f; transition.canTransitionToSelf = false; }

        private static AnimationClip Clip(string state, string kind)
        {
            bool attack = state == "Attack" || state == "CrouchAttack" || state == "Special";
            bool crouch = state == "Crouch" || state == "CrouchAttack";
            float length = attack ? (kind == "Player" ? 0.625f : 0.8f) : state == "Die" ? (kind == "Player" ? 0.9f : 0.65f) : state == "Walk" ? (kind == "Player" ? 0.8f : 0.65f) : kind == "Player" ? 1.2f : 2f;
            var clip = new AnimationClip { name = kind + "_" + state, frameRate = 60 };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = state == "Idle" || state == "Walk" || state == "Crouch" || state == "Block";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var sample = new GameObject("PoseBake");
            try
            {
                var visual = DemoRigFactory.Create(sample, kind);
                var pose = sample.GetComponent<ArticulatedRig>();
                var joints = visual.GetComponentsInChildren<Transform>();
                var curves = new Dictionary<Transform, AnimationCurve[]>();
                foreach (var joint in joints)
                {
                    if (joint == visual || joint.GetComponent<SpriteRenderer>() != null) continue;
                    curves[joint] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                }
                int samples = Mathf.CeilToInt(length * 60);
                for (int i = 0; i <= samples; i++)
                {
                    float t = length * i / samples, normalized = (float)i / samples;
                    pose.EvaluatePose(normalized * Mathf.PI * 2, state == "Walk" ? 7.5f : 0,
                        state != "Jump" && state != "Fall", crouch ? 1 : 0, state == "Jump" ? Mathf.Lerp(12, kind == "Player" ? 1 : -12, normalized) : state == "Fall" ? Mathf.Lerp(-2, -14, normalized) : 0,
                        attack ? normalized : -1, state == "Die" ? t : -1, 0, 0, state == "Block" ? 1 : 0);
                    foreach (var pair in curves)
                    {
                        pair.Value[0].AddKey(t, pair.Key.localPosition.x);
                        pair.Value[1].AddKey(t, pair.Key.localPosition.y);
                        float angle = Mathf.DeltaAngle(0, pair.Key.localEulerAngles.z);
                        if (i > 0) { float prior = pair.Value[2].keys[i - 1].value; angle = prior + Mathf.DeltaAngle(prior, angle); }
                        pair.Value[2].AddKey(t, angle);
                    }
                }
                foreach (var pair in curves)
                {
                    string path = AnimationUtility.CalculateTransformPath(pair.Key, sample.transform);
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", pair.Value[0]);
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", pair.Value[1]);
                    clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", pair.Value[2]);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
            return clip;
        }
    }
}
#endif
