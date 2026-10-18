using UnityEngine;

namespace OneMoreTime
{
    public sealed partial class ArticulatedRig
    {
        private Transform axeWrist, shieldWrist, axeSocket, shieldSocket;
        private PlayerGuard guard;
        private float guardBlend;
        private float deathGroundY;
        public bool HasShieldRig => shieldSocket != null;
        private void BindShield()
        {
            axeWrist = frontElbow?.Find("Hand"); shieldWrist = backElbow?.Find("Hand");
            axeSocket = axeWrist?.Find("AxeSocket"); shieldSocket = shieldWrist?.Find("ShieldSocket");
            guard = GetComponent<PlayerGuard>();
        }
        private void ShieldPose(float phase, float motion, bool onGround, float duck, float vy, float attack, float death, float land, float damage, float block)
        {
            if (death >= 0) { phase = motion = duck = vy = land = damage = 0; attack = -1; block = 0; onGround = true; }
            if (block < 0) block = Application.isPlaying ? guardBlend : 0;
            float move = Mathf.Clamp01(motion / 2), breath = Mathf.Sin(phase), effort = attack >= 0 ? AttackWeight(attack) : 0;
            float collapse = death < 0 ? 0 : Ease((death - 0.12f) / 0.35f);
            float fall = death < 0 ? 0 : Ease((death - 0.35f) / 0.50f);
            float hipY = 0.83f - 0.24f * duck - land * 0.09f - collapse * 0.20f - block * 0.025f
                - Mathf.Cos(phase * 2) * 0.018f * move + breath * 0.005f * (1 - move);
            SetPosition(rig, -fall * 0.18f, 0); Angle(rig, -90 * fall);
            SetPosition(pelvis, effort * 0.045f, hipY); Angle(pelvis, Mathf.Sin(phase) * 1.1f * move);
            SetPosition(torso, 0, breath * 0.006f * (1 - move));
            float lean = -3 - move * 5 - duck * 12 - effort * 12 - block * 4 + damage * 55;
            if (!onGround) lean = vy > 0 ? 2 : -9;
            if (death >= 0) lean += 12 * (1 - fall);
            Angle(torso, lean + breath * 0.6f * (1 - move)); Angle(head, -lean * 0.65f);
            SetPosition(head, -0.015f, 0.59f);
            if (onGround)
            {
                float stance = attack >= 0 ? 0.06f * Ease(attack / 0.18f) * Ease((1 - attack) / 0.2f) * (1 - move) : 0;
                Vector2 a = ShieldFoot(phase, move, duck) + Vector2.right * (0.23f + stance);
                Vector2 b = ShieldFoot(phase + Mathf.PI, move, duck) - Vector2.right * (0.23f + stance);
                SolveLimb(frontLeg, frontKnee, rig.TransformPoint(a), 0.40f, 0.31f, -1);
                SolveLimb(backLeg, backKnee, rig.TransformPoint(b), 0.40f, 0.31f, -1);
                Angle(frontFoot, -Roll(pelvis) - Roll(frontLeg) - Roll(frontKnee));
                Angle(backFoot, -Roll(pelvis) - Roll(backLeg) - Roll(backKnee));
            }
            else
            {
                float apex = 1 - Mathf.Clamp01(Mathf.Abs(vy) / 8);
                Angle(frontLeg, vy > 0 ? 20 + apex * 14 : -12 + apex * 30); Angle(frontKnee, -45 - apex * 25);
                Angle(backLeg, vy > 0 ? -15 : 28); Angle(backKnee, -75 + Mathf.Clamp01(-vy / 14) * 35);
                Angle(frontFoot, -15); Angle(backFoot, -25);
            }
            float swing = Mathf.Sin(phase) * move;
            float shoulder = -25 + swing * 8, elbow = 35 - swing * 5, wrist = -10 + breath * 0.7f;
            if (attack >= 0 && death < 0)
            {
                if (attack < 0.30f)
                { float t = Ease(attack / 0.30f); shoulder = Mathf.Lerp(-25, -165, t); elbow = Mathf.Lerp(35, -25, t); wrist = Mathf.Lerp(-10, 10, t); }
                else if (attack < 0.50f)
                { float t = Ease((attack - 0.30f) / 0.20f); shoulder = Mathf.Lerp(195, 75, t); elbow = Mathf.Lerp(-25, 15, t); wrist = Mathf.Lerp(10, -25, t); }
                else if (attack < 0.66f)
                { float t = Ease((attack - 0.50f) / 0.16f); shoulder = Mathf.Lerp(75, 65, t); elbow = Mathf.Lerp(15, 0, t); wrist = Mathf.Lerp(-25, -70, t); }
                else
                { float t = Ease((attack - 0.66f) / 0.34f); shoulder = Mathf.Lerp(65, -25, t); elbow = Mathf.Lerp(0, 35, t); wrist = Mathf.Lerp(-70, -10, t); }
            }
            else if (!onGround)
            { shoulder = vy > 0 ? -55 : -15; elbow = vy > 0 ? 60 : 18; wrist = vy > 0 ? 20 : -25; }
            shoulder += duck * 10; elbow += duck * 12;
            Angle(frontArm, shoulder); Angle(frontElbow, elbow); Angle(axeWrist, wrist);
            Angle(axeSocket, -120);
            Angle(backArm, Mathf.Lerp(25 - swing * 6, -15, block));
            Angle(backElbow, Mathf.Lerp(15 + swing * 4, 175, block));
            // Wrist counter-rotation keeps the shield upright while its arm raises it.
            Angle(shieldWrist, -Roll(pelvis) - Roll(torso) - Roll(backArm) - Roll(backElbow) + block * -8);
            Angle(shieldSocket, 0);
            Angle(heroCloth, Mathf.Sin(phase - 0.6f) * (1.2f + move * 3) + (onGround ? 0 : Mathf.Clamp(-vy * 2, -15, 24)));
            heroSwipe?.Pose(death >= 0 || block > 0.1f ? -1 : attack, duck);
            if (death >= 0 && fall > 0.02f)
            {
                float min = float.PositiveInfinity;
                foreach (var draw in rig.GetComponentsInChildren<SpriteRenderer>())
                    if (draw.enabled && draw.sprite != null)
                    {
                        var bounds = draw.bounds;
                        foreach (float x in new[] { bounds.min.x, bounds.max.x })
                            min = Mathf.Min(min, rig.parent.InverseTransformPoint(new Vector3(x, bounds.min.y, bounds.center.z)).y);
                    }
                float floor = Application.isPlaying && dead ? rig.parent.InverseTransformPoint(new Vector3(transform.position.x, deathGroundY, transform.position.z)).y : 0;
                if (!float.IsInfinity(min)) rig.localPosition += Vector3.up * (floor + 0.015f - min) * fall;
            }
        }
        private static Vector2 ShieldFoot(float phase, float move, float duck)
        {
            float u = Mathf.Repeat(phase, Mathf.PI * 2) / (Mathf.PI * 2), stride = 0.25f * move * (1 - duck * 0.65f);
            float x = u < 0.5f ? Mathf.Lerp(stride, -stride, u * 2) : Mathf.Lerp(-stride, stride, Ease((u - 0.5f) * 2));
            return new Vector2(x, 0.115f + (u < 0.5f ? 0 : Mathf.Sin((u - 0.5f) * Mathf.PI * 2) * 0.18f * move));
        }
    }
}
