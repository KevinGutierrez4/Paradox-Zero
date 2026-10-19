using System;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>Visible stages of one attack. Damage is only allowed during <see cref="Active"/>.</summary>
    public enum AttackStage { None, Anticipation, Active, Recovery }

    /// <summary>
    /// Shared combat clock. The AI that owns an attack implements it and ArticulatedRig reads it,
    /// so the pose and the damage window can never drift apart.
    /// </summary>
    public interface IAttackClock
    {
        AttackStage Stage { get; }
        /// <summary>0..1 inside the current stage.</summary>
        float StageProgress { get; }
        /// <summary>Aim in degrees in the character's facing space (0 = straight ahead, + = up).</summary>
        float AimAngle { get; }
        /// <summary>Per-archetype pose variant (e.g. 0 = white wolf, 1 = blue wolf).</summary>
        int Variant { get; }
    }

    [Serializable]
    public struct AttackTiming
    {
        [Min(0.01f)] public float anticipation;
        [Min(0.01f)] public float active;
        [Min(0.01f)] public float recovery;
        public AttackTiming(float anticipation, float active, float recovery)
        { this.anticipation = anticipation; this.active = active; this.recovery = recovery; }
        public AttackTiming Scaled(float speed)
        {
            float s = Mathf.Max(0.1f, speed);
            return new AttackTiming(anticipation / s, active / s, recovery / s);
        }
    }

    /// <summary>Plain state holder: Anticipation → Active → Recovery → None, each with a defined start and end.</summary>
    public sealed class AttackTimeline
    {
        private AttackTiming timing;
        private float elapsed;
        public AttackStage Stage { get; private set; }
        public float Progress => Stage == AttackStage.None ? 0 : Mathf.Clamp01(elapsed / Duration(Stage));
        public bool Running => Stage != AttackStage.None;

        public void Begin(AttackTiming value) { timing = value; elapsed = 0; Stage = AttackStage.Anticipation; }
        public void Cancel() { Stage = AttackStage.None; elapsed = 0; }

        /// <summary>Advances the clock. Returns the stage that was entered this step, or None.</summary>
        public AttackStage Tick(float dt)
        {
            if (Stage == AttackStage.None) return AttackStage.None;
            elapsed += dt;
            if (elapsed < Duration(Stage)) return AttackStage.None;
            elapsed -= Duration(Stage);
            Stage = Stage == AttackStage.Anticipation ? AttackStage.Active : Stage == AttackStage.Active ? AttackStage.Recovery : AttackStage.None;
            if (Stage == AttackStage.None) elapsed = 0;
            return Stage == AttackStage.None ? AttackStage.None : Stage;
        }

        private float Duration(AttackStage stage) => Mathf.Max(0.01f, stage == AttackStage.Anticipation ? timing.anticipation : stage == AttackStage.Active ? timing.active : timing.recovery);
    }
}
