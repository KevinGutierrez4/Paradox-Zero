using UnityEngine;

namespace OneMoreTime
{
    public enum RigKind { Player, Archer, Mage, Wolf, Serpent }

    /// <summary>Poses joints, never sprite scales or the physics root. Shared by preview clips and gameplay.</summary>
    [DisallowMultipleComponent]
    public sealed class ArticulatedRig : MonoBehaviour
    {
        [SerializeField] private RigKind kind;
        private Transform rig, pelvis, torso, head, frontLeg, backLeg, frontKnee, backKnee;
        private Transform frontArm, backArm, frontElbow, backElbow, weapon, tail, jaw, neck, farFront, farBack;
        private MeleeWeapon melee;
        private float speed, vertical, cycle, crouchBlend, landing, deathTime = -1, rangedTime = -1, rangedDuration = 1, hurt;
        private bool grounded = true, crouching, dead;
        public RigKind Kind => kind;

        private void Awake() { Bind(); }
        public void Bind()
        {
            rig = transform.Find("Visual/Rig");
            if (rig == null) return;
            pelvis = rig.Find("Pelvis");
            torso = rig.Find("Pelvis/Torso"); head = rig.Find("Pelvis/Torso/Head");
            frontLeg = rig.Find("Pelvis/FrontLeg"); backLeg = rig.Find("Pelvis/BackLeg");
            frontKnee = frontLeg?.Find("Lower"); backKnee = backLeg?.Find("Lower");
            frontArm = torso?.Find("FrontArm"); backArm = torso?.Find("BackArm");
            frontElbow = frontArm?.Find("Lower"); backElbow = backArm?.Find("Lower");
            weapon = frontElbow?.Find("Hand/Weapon");
            tail = rig.Find("Tail"); neck = rig.Find("Neck"); jaw = rig.Find("Neck/Head/Jaw");
            farFront = rig.Find("FarFrontLeg"); farBack = rig.Find("FarBackLeg");
            if (kind == RigKind.Wolf || kind == RigKind.Serpent)
            {
                torso = rig.Find("Torso"); head = rig.Find("Neck/Head");
                frontLeg = rig.Find("FrontLeg"); backLeg = rig.Find("BackLeg");
                frontKnee = frontLeg?.Find("Lower"); backKnee = backLeg?.Find("Lower");
            }
            melee = GetComponent<MeleeWeapon>();
        }

        public void Motion(float horizontal, bool onGround, bool crouch, float vy)
        {
            if (!grounded && onGround) landing = Mathf.Clamp01(-vertical / 15f);
            speed = Mathf.Abs(horizontal); grounded = onGround; crouching = crouch; vertical = vy;
        }
        public void Attack(float duration = 0.8f) { if (melee == null) { rangedTime = 0; rangedDuration = duration; } }
        public void Hurt() { hurt = 0.18f; }
        public void Die() { dead = true; deathTime = 0; }
        public void ResetPose()
        { dead = false; deathTime = rangedTime = -1; cycle = crouchBlend = landing = hurt = speed = vertical = 0; grounded = true; crouching = false; }

        private void LateUpdate()
        {
            if (rig == null) return;
            float dt = dead ? Time.unscaledDeltaTime : Time.deltaTime;
            cycle += dt * (speed > 0.1f ? speed * 1.7f : 1.5f);
            crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1 : 0, dt * 10);
            landing = Mathf.MoveTowards(landing, 0, dt * 7); hurt = Mathf.Max(0, hurt - dt);
            if (dead) deathTime += dt;
            if (rangedTime >= 0) { rangedTime += dt; if (rangedTime > rangedDuration) rangedTime = -1; }
            float attack = melee != null && melee.IsAttacking ? melee.Progress : rangedTime >= 0 ? rangedTime / rangedDuration : -1;
            EvaluatePose(cycle, speed, grounded, crouchBlend, vertical, attack, dead ? deathTime : -1, landing, hurt);
        }

        // Deterministic sampling is also used to bake all editor AnimationClips.
        public void EvaluatePose(float phase, float motion, bool onGround, float crouch, float vy, float attack, float death = -1, float land = 0, float damage = 0)
        {
            if (rig == null) Bind();
            if (rig == null) return;
            if (kind == RigKind.Wolf) { WolfPose(phase, motion, attack, death); return; }
            if (kind == RigKind.Serpent) { SerpentPose(phase, attack, death); return; }
            float moving = Mathf.Clamp01(motion / 2f);
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.045f * moving;
            float hipY = 0.86f - 0.33f * crouch - land * 0.1f + bob;
            SetPosition(pelvis, 0, hipY);
            Angle(rig, death >= 0 ? Mathf.Lerp(0, -88, Mathf.SmoothStep(0, 1, death / 0.55f)) : 0);
            SetPosition(rig, 0, death >= 0 ? -0.22f * Mathf.Clamp01(death / 0.55f) : 0);
            Angle(pelvis, Mathf.Sin(phase) * 2f * moving);
            float lean = -6 * moving - 55 * crouch + land * 8;
            if (damage > 0) lean += 18 * damage / 0.18f;
            float breath = Mathf.Sin(phase * 0.8f) * 1.2f;
            float swing = attack < 0 ? 0 : Swing(attack);
            Angle(torso, lean + breath + (attack >= 0 && kind == RigKind.Player ? Mathf.Lerp(12, -18, swing) : 0));
            Angle(head, -lean * 0.4f - breath);
            if (onGround)
            {
                Leg(frontLeg, frontKnee, new Vector2(0.12f, hipY), Foot(phase, moving, crouch) + Vector2.right * 0.17f, 0.44f, 0.44f);
                Leg(backLeg, backKnee, new Vector2(-0.12f, hipY), Foot(phase + Mathf.PI, moving, crouch) - Vector2.right * 0.17f, 0.44f, 0.44f);
            }
            else
            {
                Angle(frontLeg, vy > 1 ? -35 : vy < -1 ? 18 : -18); Angle(frontKnee, vy > 1 ? 75 : 28);
                Angle(backLeg, vy > 1 ? 25 : -28); Angle(backKnee, vy > 1 ? 95 : 45);
            }
            float arm = Mathf.Sin(phase) * 18 * moving;
            Angle(backArm, -arm - 8 + crouch * 45); Angle(backElbow, 25 + crouch * 20);
            Angle(frontArm, 12 + arm * 0.3f + crouch * 60); Angle(frontElbow, 65 - arm * 0.25f);
            Angle(weapon, kind == RigKind.Player ? -20 : 0);
            if (attack >= 0 && kind == RigKind.Player)
            {
                // 0-.30 anticipation, .30-.50 cut, .50-1 recovery: same clock as MeleeWeapon.
                float shoulder = attack < 0.3f ? Mathf.Lerp(15, -155, Ease(attack / 0.3f)) :
                    attack < 0.5f ? Mathf.Lerp(-155, 55, Ease((attack - 0.3f) / 0.2f)) : Mathf.Lerp(55, 15, Ease((attack - 0.5f) / 0.5f));
                Angle(frontArm, shoulder); Angle(frontElbow, Mathf.Lerp(95, 15, swing) + crouch * 85 * swing);
                Angle(backArm, shoulder * 0.45f - 15); Angle(backElbow, 65 - swing * 35);
                Angle(weapon, Mathf.Lerp(-30, 12, swing));
            }
            else if (kind == RigKind.Archer)
            {
                Angle(frontArm, 65); Angle(frontElbow, 20); Angle(weapon, -85);
                Angle(backArm, attack >= 0 ? Mathf.Lerp(30, -30, Mathf.Sin(attack * Mathf.PI)) : 15);
                Angle(backElbow, attack >= 0 ? 80 + Mathf.Sin(attack * Mathf.PI) * 50 : 65);
            }
            else if (kind == RigKind.Mage)
            {
                float cast = attack >= 0 ? Mathf.Sin(attack * Mathf.PI) : 0;
                Angle(frontArm, 15 + 50 * cast);
                Angle(frontElbow, 45 - 25 * cast); Angle(weapon, -60 - 85 * cast);
                Angle(backArm, attack >= 0 ? -35 * Mathf.Sin(attack * Mathf.PI) : -10);
                Angle(backElbow, 35);
            }
            if (death >= 0) { Angle(frontArm, 45); Angle(frontElbow, 10); Angle(backArm, -45); Angle(head, -25); }
        }

        private static Vector2 Foot(float phase, float moving, float crouch)
        {
            float stride = 0.24f * moving * (1 - crouch * 0.6f);
            // Stance travels front -> back relative to the moving body; the return lifts the foot.
            return new Vector2(Mathf.Cos(phase) * stride, 0.035f + Mathf.Max(0, -Mathf.Sin(phase)) * 0.19f * moving);
        }
        private static void Leg(Transform upper, Transform lower, Vector2 hip, Vector2 foot, float a, float b)
        {
            Vector2 delta = foot - hip;
            float distance = Mathf.Clamp(delta.magnitude, 0.05f, a + b - 0.001f);
            float knee = Mathf.Acos(Mathf.Clamp((distance * distance - a * a - b * b) / (2 * a * b), -1, 1)) * Mathf.Rad2Deg;
            float shoulder = Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg - Mathf.Atan2(b * Mathf.Sin(knee * Mathf.Deg2Rad), a + b * Mathf.Cos(knee * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
            Angle(upper, shoulder); Angle(lower, knee);
        }
        private void WolfPose(float phase, float motion, float attack, float death)
        {
            float run = Mathf.Clamp01(motion / 2); float gait = Mathf.Sin(phase) * 28 * run;
            Angle(frontLeg, gait); Angle(frontKnee, Mathf.Max(0, -gait) * 1.2f);
            Angle(backLeg, -gait); Angle(backKnee, Mathf.Max(0, gait) * 1.4f);
            Angle(farFront, -gait); Angle(farFront?.Find("Lower"), Mathf.Max(0, gait));
            Angle(farBack, gait); Angle(farBack?.Find("Lower"), Mathf.Max(0, -gait));
            SetPosition(rig, 0, Mathf.Abs(Mathf.Sin(phase)) * 0.045f * run);
            Angle(torso, Mathf.Sin(phase * 2) * 2 * run); Angle(tail, Mathf.Sin(phase - 0.6f) * 12);
            float snap = attack < 0 ? 0 : Mathf.Sin(Mathf.Clamp01((attack - 0.18f) / 0.65f) * Mathf.PI);
            Angle(neck, 10 * snap); Angle(head, -18 * snap); Angle(jaw, -32 * snap);
            if (death >= 0) { Angle(rig, Mathf.Lerp(0, -85, Mathf.Clamp01(death / 0.5f))); SetPosition(rig, 0, -0.28f * Mathf.Clamp01(death / 0.5f)); }
            else Angle(rig, 0);
        }
        private void SerpentPose(float phase, float attack, float death)
        {
            Angle(torso, Mathf.Sin(phase * 0.65f) * 4); Angle(neck, Mathf.Sin(phase * 0.65f - 0.5f) * 6);
            Angle(head, Mathf.Sin(phase * 0.65f - 1) * -5); Angle(tail, Mathf.Sin(phase * 0.65f - 1.5f) * 15);
            Angle(jaw, attack >= 0 ? -38 * Mathf.Sin(attack * Mathf.PI) : -3);
            Angle(rig, death >= 0 ? Mathf.Lerp(0, -70, Mathf.Clamp01(death / 0.6f)) : 0);
        }
        private static float Swing(float t) => t < 0.3f ? 0 : t < 0.5f ? Ease((t - 0.3f) / 0.2f) : 1 - Ease((t - 0.5f) / 0.5f);
        private static float Ease(float t) => Mathf.SmoothStep(0, 1, t);
        private static void Angle(Transform part, float angle) { if (part != null) part.localRotation = Quaternion.Euler(0, 0, angle); }
        private static void SetPosition(Transform part, float x, float y) { if (part != null) part.localPosition = new Vector3(x, y, 0); }
    }
}
