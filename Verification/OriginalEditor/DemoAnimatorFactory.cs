#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OneMoreTime.Editor
{
    internal static class DemoAnimatorFactory
    {
        internal static AnimatorController Create(string directory, bool player)
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
                var clip = Clip(name, player);
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
            return controller;
        }

        private static void Instant(AnimatorStateTransition transition)
        { transition.hasExitTime = false; transition.duration = 0.025f; transition.canTransitionToSelf = false; }

        private static AnimationClip Clip(string state, bool player)
        {
            bool attacking = state == "Attack" || state == "CrouchAttack";
            bool crouching = state == "Crouch" || state == "CrouchAttack";
            float length = attacking ? 0.12f : state == "Die" ? 0.5f : state == "Special" ? 0.85f : 0.55f;
            var clip = new AnimationClip { name = state, frameRate = 30 };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = state == "Idle" || state == "Walk" || state == "Jump" || state == "Crouch";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            bool walk = state == "Walk";
            float bob = walk ? 0.045f : state == "Idle" ? 0.015f : 0;
            Curve(clip, "Visual/Rig", "m_LocalPosition.y", length, 0, bob, 0);
            Curve(clip, "Visual/Rig", "m_LocalScale.y", length, crouching ? 0.55f : 1, crouching ? 0.55f : 1, crouching ? 0.55f : 1);
            Curve(clip, "Visual/Rig", "localEulerAnglesRaw.z", length, 0, state == "Die" ? -65 : 0, state == "Die" ? -90 : 0);
            float leg = walk ? 22 : state == "Jump" ? 12 : 0;
            Curve(clip, "Visual/Rig/FrontLeg", "localEulerAnglesRaw.z", length, leg, -leg, leg);
            Curve(clip, "Visual/Rig/BackLeg", "localEulerAnglesRaw.z", length, -leg, leg, -leg);
            float rest = player ? 85 : 15;
            Curve(clip, "Visual/Rig/FrontArm", "localEulerAnglesRaw.z", length, rest, rest + (attacking ? 20 : walk ? 10 : state == "Special" ? 70 : 0), rest);
            Curve(clip, "Visual/Rig/BackArm", "localEulerAnglesRaw.z", length, walk ? -14 : 0, walk ? 14 : 0, walk ? -14 : 0);
            return clip;
        }

        private static void Curve(AnimationClip clip, string path, string property, float duration, float start, float middle, float end)
        {
            clip.SetCurve(path, typeof(Transform), property,
                new AnimationCurve(new Keyframe(0, start), new Keyframe(duration * 0.5f, middle), new Keyframe(duration, end)));
        }
    }
}
#endif
