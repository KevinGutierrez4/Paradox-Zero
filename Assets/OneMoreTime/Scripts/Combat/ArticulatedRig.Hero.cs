using UnityEngine;

namespace OneMoreTime
{
    public sealed partial class ArticulatedRig
    {
        private Transform heroGrip, heroCloth, frontFoot, backFoot;
        private AxeSwipe heroSwipe;
        private void BindHero()
        {
            heroGrip = kind == RigKind.Player ? torso?.Find("AxeGrip") : null;
            heroCloth = pelvis?.Find("Cloth");
            frontFoot = frontKnee?.Find("Foot"); backFoot = backKnee?.Find("Foot");
            heroSwipe = rig != null ? rig.GetComponentInChildren<AxeSwipe>() : null;
        }

        private void HeroPose(float phase, float motion, bool onGround, float duck, float vy, float attack, float death, float land, float damage)
        {
            float move = Mathf.Clamp01(motion / 2), breath = Mathf.Sin(phase);
            float bob = -Mathf.Cos(phase * 2) * 0.018f * move;
            float effort = attack < 0 ? 0 : AttackWeight(attack);
            float hipX = Mathf.Sin(phase) * 0.014f * move + effort * 0.075f;
            float hipY = 0.81f - duck * 0.32f + bob + breath * 0.008f * (1 - move) - land * 0.08f - Mathf.Abs(effort) * 0.055f;
            SetPosition(rig, 0, death >= 0 ? -0.2f * Mathf.Clamp01(death / 0.55f) : 0);
            Angle(rig, death >= 0 ? -88 * Ease(death / 0.55f) : 0);
            SetPosition(pelvis, hipX, hipY); Angle(pelvis, Mathf.Sin(phase) * 1.4f * move);
            float lean = -3 - move * 5 - duck * 16 - effort * 14 + damage * 70;
            if (!onGround) lean = vy > 0 ? 4 : -12;
            Angle(torso, lean + breath * 0.65f * (1 - move));
            Angle(head, -lean * 0.65f - breath * 0.4f);
            SetPosition(torso, 0, breath * 0.006f * (1 - move));
            SetPosition(head, 0.03f, 0.76f - duck * 0.04f);

            if (onGround)
            {
                float stance = attack >= 0 ? 0.12f * (1 - move) * Ease(attack / 0.18f) * Ease((1 - attack) / 0.20f) : 0;
                Vector2 frontTarget = HeroFoot(phase, move, duck) + Vector2.right * (0.18f + stance);
                Vector2 backTarget = HeroFoot(phase + Mathf.PI, move, duck) - Vector2.right * (0.18f + stance);
                SolveLimb(frontLeg, frontKnee, rig.TransformPoint(frontTarget), 0.44f, 0.30f, 1);
                SolveLimb(backLeg, backKnee, rig.TransformPoint(backTarget), 0.44f, 0.30f, 1);
                Angle(frontFoot, -Roll(pelvis) - Roll(frontLeg) - Roll(frontKnee));
                Angle(backFoot, -Roll(pelvis) - Roll(backLeg) - Roll(backKnee));
            }
            else
            {
                float flutter = Mathf.Sin(phase) * 4;
                Angle(frontLeg, vy > 0 ? -32 + flutter : -13 + flutter);
                Angle(frontKnee, vy > 0 ? 65 : 33);
                Angle(backLeg, vy > 0 ? 20 - flutter : -38 - flutter);
                Angle(backKnee, vy > 0 ? 92 : 68);
                Angle(frontFoot, vy > 0 ? -15 : 18); Angle(backFoot, vy > 0 ? -30 : 8);
            }

            Vector2 grip = new Vector2(0.40f, 1.28f);
            float axeAngle = -25;
            if (attack >= 0)
            {
                if (attack < 0.30f)
                { float t = Ease(attack / 0.30f); grip = Vector2.Lerp(grip, new Vector2(-0.02f, 1.82f), t); axeAngle = Mathf.Lerp(-25, 48, t); }
                else if (attack < 0.50f)
                { float t = Ease((attack - 0.30f) / 0.20f); grip = Vector2.Lerp(new Vector2(-0.02f, 1.82f), new Vector2(0.56f, 1.10f), t); axeAngle = Mathf.Lerp(48, -112, t); }
                else if (attack < 0.66f)
                { float t = Ease((attack - 0.50f) / 0.16f); grip = Vector2.Lerp(new Vector2(0.56f, 1.10f), new Vector2(0.43f, 0.98f), t); axeAngle = Mathf.Lerp(-112, -150 + duck * 35, t); }
                else
                { float t = Ease((attack - 0.66f) / 0.34f); grip = Vector2.Lerp(new Vector2(0.43f, 0.98f), grip, t); axeAngle = Mathf.Lerp(-150 + duck * 35, -25, t); }
            }
            else if (!onGround)
            { grip = vy > 0 ? new Vector2(0.23f, 1.62f) : new Vector2(0.53f, 1.17f); axeAngle = vy > 0 ? 20 : -135; }
            grip.y -= duck * 0.34f;
            grip.x += duck * 0.10f;
            grip += new Vector2(0, breath * 0.012f * (1 - move) + bob * 0.65f);
            axeAngle += Mathf.Sin(phase - 0.35f) * (move > 0 ? 2 : 0.8f);
            heroGrip.position = rig.TransformPoint(grip);
            Angle(heroGrip, axeAngle - Roll(pelvis) - Roll(torso));
            // Both wrists solve towards two fixed points on the same shaft, never independently swing.
            SolveLimb(frontArm, frontElbow, heroGrip.Find("FrontGrip").position, 0.29f, 0.32f, -1);
            SolveLimb(backArm, backElbow, heroGrip.Find("RearGrip").position, 0.29f, 0.32f, -1);
            float clothAngle = Mathf.Sin(phase - 0.7f) * (2 + move * 5) - effort * 12;
            if (!onGround) clothAngle += vy < 0 ? -48 : 18;
            Angle(heroCloth, clothAngle);
            heroSwipe?.Pose(attack, duck);
        }

        private static float AttackWeight(float t)
        {
            if (t < 0.30f) return Mathf.Lerp(0, -0.4f, Ease(t / 0.30f));
            if (t < 0.50f) return Mathf.Lerp(-0.4f, 1, Ease((t - 0.30f) / 0.20f));
            return Mathf.Lerp(1, 0, Ease((t - 0.50f) / 0.50f));
        }
        private static float Roll(Transform part) => part != null ? Mathf.DeltaAngle(0, part.localEulerAngles.z) : 0;
        private static Vector2 HeroFoot(float phase, float move, float duck)
        {
            float u = Mathf.Repeat(phase, Mathf.PI * 2) / (Mathf.PI * 2);
            float stride = 0.27f * move * (1 - duck * 0.7f);
            float x = u < 0.5f ? Mathf.Lerp(stride, -stride, u * 2) : Mathf.Lerp(-stride, stride, Ease((u - 0.5f) * 2));
            float lift = u < 0.5f ? 0 : Mathf.Sin((u - 0.5f) * Mathf.PI * 2) * 0.20f * move;
            return new Vector2(x, 0.151f + lift);
        }
        private static void SolveLimb(Transform upper, Transform lower, Vector3 worldTarget, float a, float b, float bend)
        {
            if (upper == null || lower == null) return;
            Vector2 delta = (Vector2)upper.parent.InverseTransformPoint(worldTarget) - (Vector2)upper.localPosition;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            float knee = Mathf.Acos(Mathf.Clamp((distance * distance - a * a - b * b) / (2 * a * b), -1, 1)) * bend;
            float shoulder = Mathf.Atan2(delta.x, -delta.y) - Mathf.Atan2(b * Mathf.Sin(knee), a + b * Mathf.Cos(knee));
            Angle(upper, shoulder * Mathf.Rad2Deg); Angle(lower, knee * Mathf.Rad2Deg);
        }
    }
}
