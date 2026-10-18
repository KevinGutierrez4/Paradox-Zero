using UnityEngine;

namespace OneMoreTime
{
    public enum RigKind { Player, Archer, Mage, Wolf, Serpent }

    /// <summary>
    /// Poses joints, never sprite scales or the physics root. It is the only runtime writer of the rig
    /// transforms (AnimationDriver disables the Animator). Attack poses read the owner's
    /// <see cref="IAttackClock"/> (enemies) or MeleeWeapon (player), so pose and damage share one clock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ArticulatedRig : MonoBehaviour
    {
        [SerializeField] private RigKind kind;
        private Transform rig, pelvis, torso, head, frontLeg, backLeg, frontKnee, backKnee;
        private Transform frontArm, backArm, frontElbow, backElbow, weapon, tail, jaw, neck, farFront, farBack;
        private Transform farFrontKnee, farBackKnee, frontHand;
        private Vector2 frontHip, backHip, farFrontHip, farBackHip;
        private SpriteRenderer backForearmSprite;
        private int backForearmOrder;
        private MeleeWeapon melee;
        private IAttackClock clock;
        private AnimationDriver driver;
        private float speed, vertical, cycle, crouchBlend, landing, deathTime = -1, rangedTime = -1, rangedDuration = 1, hurt;
        private bool grounded = true, crouching, dead;
        private float signedSpeed;
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
            frontHand = frontElbow?.Find("Hand");
            weapon = frontElbow?.Find("Hand/Weapon");
            tail = rig.Find("Tail"); neck = rig.Find("Neck"); jaw = rig.Find("Neck/Head/Jaw");
            farFront = rig.Find("FarFrontLeg"); farBack = rig.Find("FarBackLeg");
            if (kind == RigKind.Wolf || kind == RigKind.Serpent)
            {
                torso = rig.Find("Torso"); head = rig.Find("Neck/Head");
                frontLeg = rig.Find("FrontLeg"); backLeg = rig.Find("BackLeg");
                frontKnee = frontLeg?.Find("Lower"); backKnee = backLeg?.Find("Lower");
                farFrontKnee = farFront?.Find("Lower"); farBackKnee = farBack?.Find("Lower");
                // Hip anchors are the authored rest positions; feet are planted relative to them.
                frontHip = frontLeg != null ? (Vector2)frontLeg.localPosition : new Vector2(0.52f, 0.65f);
                backHip = backLeg != null ? (Vector2)backLeg.localPosition : new Vector2(-0.67f, 0.65f);
                farFrontHip = farFront != null ? (Vector2)farFront.localPosition : new Vector2(0.4f, 0.65f);
                farBackHip = farBack != null ? (Vector2)farBack.localPosition : new Vector2(-0.55f, 0.65f);
            }
            var forearm = backElbow != null ? backElbow.Find("Sprite") : null;
            backForearmSprite = forearm != null ? forearm.GetComponent<SpriteRenderer>() : null;
            if (backForearmSprite != null) backForearmOrder = backForearmSprite.sortingOrder;
            melee = GetComponent<MeleeWeapon>();
            clock = GetComponent<IAttackClock>();
            driver = GetComponent<AnimationDriver>();
            BindHero();
            BindShield();
            BindSerpent();
        }

        public void Motion(float horizontal, bool onGround, bool crouch, float vy)
        {
            if (!grounded && onGround) landing = Mathf.Clamp01(-vertical / 15f);
            speed = Mathf.Abs(horizontal); signedSpeed = horizontal; grounded = onGround; crouching = crouch; vertical = vy;
        }
        public void Attack(float duration = 0.8f) { if (melee == null && clock == null) { rangedTime = 0; rangedDuration = duration; } }
        public void Hurt() { hurt = 0.18f; }
        public void Die() { dead = true; deathTime = 0; deathGroundY = GetComponent<PlayerController>()?.DeathFloorY ?? transform.position.y; }
        public void ResetPose()
        {
            dead = false; deathTime = rangedTime = -1; cycle = crouchBlend = landing = hurt = speed = signedSpeed = vertical = guardBlend = 0; grounded = true; crouching = false;
            HideHelpers();
        }

        // Feet must travel backwards exactly as fast as the body moves forwards while planted:
        // stance covers 2 * stride in half a cycle (π rad), so ω = π·v / (2·stride).
        private static float StrideFor(float v, float maxStride) => Mathf.Max(0.06f, maxStride * Mathf.Clamp01(v / 1.4f));
        private float CycleRate()
        {
            if (HasShieldRig) return speed > 0.1f ? speed * Mathf.PI / 3 : Mathf.PI * 2 / 1.2f;
            if (kind == RigKind.Player) return speed > 0.1f ? 5 + speed * 0.85f : 2.6f;
            if (kind == RigKind.Wolf) return speed > 0.1f ? Mathf.PI * speed / (2 * StrideFor(speed, WolfStride)) : 1.5f;
            if (kind == RigKind.Serpent) return 1.5f;
            return speed > 0.1f ? Mathf.PI * speed / (2 * StrideFor(speed, HumanStride)) : 1.5f;
        }

        private void LateUpdate()
        {
            if (rig == null) return;
            float dt = dead ? Time.unscaledDeltaTime : Time.deltaTime;
            // Walking backwards relative to the drawing (keep-away, locked facing) runs the cycle in reverse,
            // so planted feet still move opposite to the body instead of sliding.
            float cycleDirection = driver != null && speed > 0.1f && Mathf.Sign(signedSpeed) != driver.Facing ? -1 : 1;
            cycle += dt * CycleRate() * cycleDirection;
            guardBlend = Mathf.MoveTowards(guardBlend, guard != null && guard.IsBlocking ? 1 : 0, dt * 12);
            crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1 : 0, dt * 10);
            landing = Mathf.MoveTowards(landing, 0, dt * 7); hurt = Mathf.Max(0, hurt - dt);
            if (dead) deathTime += dt;
            if (rangedTime >= 0) { rangedTime += dt; if (rangedTime > rangedDuration) rangedTime = -1; }
            float attack = melee != null && melee.IsAttacking ? melee.Progress : rangedTime >= 0 ? rangedTime / rangedDuration : -1;
            if (clock != null && !dead && kind != RigKind.Player)
            {
                live = true; liveStage = clock.Stage; liveProgress = clock.StageProgress; liveAim = clock.AimAngle; liveVariant = clock.Variant;
            }
            else live = false;
            EvaluatePose(cycle, speed, grounded, crouchBlend, vertical, attack, dead ? deathTime : -1, landing, hurt);
            live = false;
            UpdateHelpers();
        }

        // Live clock values (runtime). When absent, EvaluatePose maps the normalised attack time onto
        // the same three stages so the editor clip baker keeps producing matching previews.
        private bool live;
        private AttackStage liveStage;
        private float liveProgress, liveAim;
        private int liveVariant;

        private void StageOf(float attack, out AttackStage stage, out float progress, out float aim, out int variant)
        {
            if (live) { stage = liveStage; progress = liveProgress; aim = liveAim; variant = liveVariant; return; }
            aim = 0; variant = 0;
            if (attack < 0) { stage = AttackStage.None; progress = 0; return; }
            if (attack < 0.6f) { stage = AttackStage.Anticipation; progress = attack / 0.6f; }
            else if (attack < 0.72f) { stage = AttackStage.Active; progress = (attack - 0.6f) / 0.12f; }
            else { stage = AttackStage.Recovery; progress = (attack - 0.72f) / 0.28f; }
        }

        // Deterministic sampling is also used to bake all editor AnimationClips.
        public void EvaluatePose(float phase, float motion, bool onGround, float crouch, float vy, float attack, float death = -1, float land = 0, float damage = 0, float block = -1)
        {
            if (rig == null) Bind();
            if (rig == null) return;
            StageOf(attack, out var stage, out float progress, out float aim, out int variant);
            if (kind == RigKind.Wolf) { WolfPose(phase, motion, stage, progress, variant, death, damage); return; }
            if (kind == RigKind.Serpent) { SerpentPose(phase, attack, death); return; }
            if (kind == RigKind.Player && HasShieldRig) { ShieldPose(phase, motion, onGround, crouch, vy, attack, death, land, damage, block); return; }
            if (kind == RigKind.Player && heroGrip != null) { HeroPose(phase, motion, onGround, crouch, vy, attack, death, land, damage); return; }
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
                float stride = StrideFor(motion, HumanStride) * (moving > 0.01f ? 1 : 0) * (1 - crouch * 0.6f);
                Leg(frontLeg, frontKnee, new Vector2(0.12f, hipY), Foot(phase, stride, moving) + Vector2.right * 0.17f, 0.44f, 0.44f);
                Leg(backLeg, backKnee, new Vector2(-0.12f, hipY), Foot(phase + Mathf.PI, stride, moving) - Vector2.right * 0.17f, 0.44f, 0.44f);
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
            else if (kind == RigKind.Archer) ArcherArms(stage, progress, aim, lean);
            else if (kind == RigKind.Mage) MageArms(stage, progress, aim, lean);
            if (death >= 0) { Angle(frontArm, 45); Angle(frontElbow, 10); Angle(backArm, -45); Angle(head, -25); }
        }

        private const float HumanStride = 0.22f;
        private const float WolfStride = 0.24f;

        /// <summary>Stance moves the foot linearly (no sliding), the return arc lifts and eases it forward.</summary>
        private static Vector2 Foot(float phase, float stride, float moving)
        {
            float u = Mathf.Repeat(phase, Mathf.PI * 2) / (Mathf.PI * 2);
            float x = u < 0.5f ? Mathf.Lerp(stride, -stride, u * 2) : Mathf.Lerp(-stride, stride, Ease((u - 0.5f) * 2));
            float lift = u < 0.5f ? 0 : Mathf.Sin((u - 0.5f) * Mathf.PI * 2) * 0.17f * moving;
            return new Vector2(x, 0.035f + lift);
        }
        private static void Leg(Transform upper, Transform lower, Vector2 hip, Vector2 foot, float a, float b)
        {
            Vector2 delta = foot - hip;
            float distance = Mathf.Clamp(delta.magnitude, 0.05f, a + b - 0.001f);
            float knee = Mathf.Acos(Mathf.Clamp((distance * distance - a * a - b * b) / (2 * a * b), -1, 1)) * Mathf.Rad2Deg;
            float shoulder = Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg - Mathf.Atan2(b * Mathf.Sin(knee * Mathf.Deg2Rad), a + b * Mathf.Cos(knee * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
            Angle(upper, shoulder); Angle(lower, knee);
        }

        // ---------------------------------------------------------------- Archer
        // Anticipation 0-.35 raise the bow onto the aim line, .35-.80 draw the string to the face,
        // .80-1 hold and aim. Active = release (the arrow is spawned on its first frame). Recovery lowers.
        private float archerDraw = -1;
        private void ArcherArms(AttackStage stage, float p, float aim, float lean)
        {
            float raise = 0, draw = 0, release = 0;
            if (stage == AttackStage.Anticipation) { raise = Ease(p / 0.35f); draw = Ease((p - 0.35f) / 0.45f); }
            else if (stage == AttackStage.Active) { raise = 1; draw = 1; release = Ease(p / 0.5f); }
            else if (stage == AttackStage.Recovery) { raise = 1 - Ease(p); draw = 1; release = 1 - Ease(p / 0.5f); }
            float hold = stage == AttackStage.Anticipation ? Ease((p - 0.8f) / 0.2f) : 0;
            float tremble = hold * Mathf.Sin(Time.time * 40f) * 0.6f;
            if (raise <= 0.001f || torso == null)
            {
                Angle(frontArm, 65); Angle(frontElbow, 20); Angle(weapon, -85); Angle(backArm, 15); Angle(backElbow, 65);
                archerDraw = -1; SetBackForearmFront(false);
                return;
            }
            float a = (aim + tremble) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 shoulder = frontArm != null ? (Vector2)frontArm.localPosition : new Vector2(0.2f, 0.62f);
            Vector2 grip = shoulder + dir * 0.55f;
            SolveLimb(frontArm, frontElbow, torso.TransformPoint(grip), 0.29f, 0.32f, -1);
            Angle(frontArm, Mathf.LerpAngle(65, Roll(frontArm), raise));
            Angle(frontElbow, Mathf.LerpAngle(20, Roll(frontElbow), raise));
            // The bow art is painted leaning ~20° forward; keep the stave square to the aim line.
            float chain = Roll(pelvis) + lean + Roll(frontArm) + Roll(frontElbow);
            Angle(weapon, Mathf.LerpAngle(-85, aim + 20 - chain, raise));
            Vector2 perpendicular = new Vector2(-dir.y, dir.x);
            Vector2 anchor = grip - dir * (0.48f + 0.08f * release) + perpendicular * (0.06f + 0.03f * release);
            Vector2 hand = Vector2.Lerp(grip - dir * 0.08f, anchor, stage == AttackStage.Recovery ? 1 : draw);
            SolveLimb(backArm, backElbow, torso.TransformPoint(hand), 0.29f, 0.30f, 1);
            Angle(backArm, Mathf.LerpAngle(15, Roll(backArm), raise));
            Angle(backElbow, Mathf.LerpAngle(65, Roll(backElbow), raise));
            // While drawing, the string hand crosses in front of the chest.
            SetBackForearmFront(draw > 0.05f && raise > 0.5f);
            archerDraw = stage == AttackStage.Anticipation && p >= 0.35f ? draw : -1;
        }
        private void SetBackForearmFront(bool front)
        {
            if (backForearmSprite != null) backForearmSprite.sortingOrder = front ? 7 : backForearmOrder;
        }

        // ---------------------------------------------------------------- Mage
        // Anticipation 0-.30 raise the staff, .30-1 channel (tremble + growing glow at the staff head).
        // Active = thrust towards the target (projectile leaves the staff head). Recovery returns.
        private float mageCharge = -1;
        private void MageArms(AttackStage stage, float p, float aim, float lean)
        {
            float up = 0, channel = 0, thrust = 0;
            if (stage == AttackStage.Anticipation) { up = Ease(p / 0.3f); channel = Ease((p - 0.3f) / 0.7f); }
            else if (stage == AttackStage.Active) { up = 1 - Ease(p / 0.6f); thrust = Ease(p / 0.35f); channel = 1; }
            else if (stage == AttackStage.Recovery) { thrust = 1 - Ease(p); }
            float tremble = stage == AttackStage.Anticipation ? Mathf.Sin(Time.time * 55f) * 1.5f * channel : 0;
            float extraLean = -5 * up + 6 * thrust;
            Angle(torso, Roll(torso) + extraLean); Angle(head, Roll(head) - extraLean * 0.5f);
            float fa = Mathf.LerpAngle(Mathf.LerpAngle(15, 150, up), 95, thrust);
            float fl = Mathf.LerpAngle(Mathf.LerpAngle(45, -25, up), -5, thrust);
            Angle(frontArm, fa); Angle(frontElbow, fl);
            float chain = Roll(pelvis) + Roll(torso) + fa + fl;
            float restWorld = Roll(pelvis) + Roll(torso) + 15 + 45 - 60;
            float staffWorld = Mathf.LerpAngle(Mathf.LerpAngle(restWorld, -8 + tremble, up), -70 + aim, thrust);
            Angle(weapon, staffWorld - chain);
            float palm = Mathf.Max(up, channel * 0.8f);
            Angle(backArm, Mathf.LerpAngle(Mathf.LerpAngle(-10, 80, palm), 40, thrust));
            Angle(backElbow, Mathf.LerpAngle(Mathf.LerpAngle(35, -20, palm), 25, thrust));
            mageCharge = stage == AttackStage.Anticipation && p >= 0.3f ? channel : stage == AttackStage.Active && p < 0.25f ? 1 - p * 4 : -1;
        }

        // ---------------------------------------------------------------- Wolves
        // Feet are planted with 2-bone IK on the ground line, so the body can crouch, lunge and
        // leap without paws sliding or sinking. Stages come from the wolf's attack clock.
        private void WolfPose(float phase, float motion, AttackStage stage, float p, int variant, float death, float damage)
        {
            float run = Mathf.Clamp01(motion / 2f);
            float crouch = 0, lunge = 0, back = 0, lift = 0, pitch = 0, jawAngle = -3, neckAngle = 0, headAngle = 0;
            bool blue = variant == 1;
            if (stage == AttackStage.Anticipation)
            {
                float e = Ease(p); crouch = e * (blue ? 0.75f : 1f);
                jawAngle = Mathf.Lerp(-3, -14, e); neckAngle = Mathf.Lerp(0, -10, e); headAngle = Mathf.Lerp(0, 12, e);
                if (blue) back = e;
            }
            else if (stage == AttackStage.Active)
            {
                float e = Ease(p / 0.4f);
                crouch = (1 - e) * (blue ? 0.75f : 1f); lunge = e; back = blue ? 1 - e : 0;
                // Jaw opens wide during the lunge and snaps shut: the snap is the damage frame.
                jawAngle = p < 0.5f ? Mathf.Lerp(-14, -40, Ease(p / 0.4f)) : Mathf.Lerp(-40, -2, Ease((p - 0.5f) / 0.25f));
                neckAngle = Mathf.Lerp(-10, -18, e); headAngle = Mathf.Lerp(12, -8, e);
                if (blue) { lift = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI) * 0.22f; pitch = Mathf.Lerp(7, -5, Ease(p)); }
                else pitch = -4 * e;
            }
            else if (stage == AttackStage.Recovery)
            {
                float e = Ease(p); lunge = 1 - e; jawAngle = -2 - e;
                neckAngle = Mathf.Lerp(-18, 0, e); headAngle = Mathf.Lerp(-8, 0, e); pitch = Mathf.Lerp(blue ? -5 : -4, 0, e);
            }
            if (damage > 0)
            {
                float h = damage / 0.18f; pitch += 7 * h; neckAngle += 14 * h; headAngle -= 6 * h; crouch = Mathf.Max(crouch, 0.35f * h);
            }
            float settle = death >= 0 ? Ease(death / 0.45f) : 0;
            float rx = lunge * 0.14f - back * 0.12f;
            float ry = Mathf.Abs(Mathf.Sin(phase)) * 0.03f * run - crouch * 0.13f + lift - 0.30f * settle;
            SetPosition(rig, rx, ry); Angle(rig, pitch - 6 * settle);
            float stride = StrideFor(motion, WolfStride) * (run > 0.01f ? 1 : 0);
            WolfLeg(frontLeg, frontKnee, frontHip, phase, stride, run, 1, lunge, lift, settle);
            WolfLeg(farBack, farBackKnee, farBackHip, phase, stride, run, -1, lunge, lift, settle);
            WolfLeg(backLeg, backKnee, backHip, phase + Mathf.PI, stride, run, -1, lunge, lift, settle);
            WolfLeg(farFront, farFrontKnee, farFrontHip, phase + Mathf.PI, stride, run, 1, lunge, lift, settle);
            Angle(neck, death >= 0 ? Mathf.Lerp(neckAngle, -28, settle) : neckAngle);
            Angle(head, death >= 0 ? Mathf.Lerp(headAngle, 10, settle) : headAngle);
            Angle(jaw, death >= 0 ? -10 : jawAngle);
            Angle(tail, death >= 0 ? Mathf.Lerp(-3.4f, -25, settle) : Mathf.Sin(phase - 0.6f) * (4 + 8 * run) - crouch * 14 + lunge * 10 - 3.4f);
        }

        private void WolfLeg(Transform upper, Transform lower, Vector2 hip, float phase, float stride, float run, int front, float lunge, float lift, float settle)
        {
            if (upper == null || lower == null || rig.parent == null) return;
            float u = Mathf.Repeat(phase, Mathf.PI * 2) / (Mathf.PI * 2);
            float fx = u < 0.5f ? Mathf.Lerp(stride, -stride, u * 2) : Mathf.Lerp(-stride, stride, Ease((u - 0.5f) * 2));
            float fy = u < 0.5f ? 0 : Mathf.Sin((u - 0.5f) * Mathf.PI * 2) * 0.14f * run;
            bool airborne = lift > 0.05f;
            float reach = airborne ? 0 : (front > 0 ? 0.30f : -0.22f) * lunge;
            if (airborne) fy += lift * 0.9f + (front > 0 ? 0.08f : 0.02f);
            Vector2 target = new Vector2(hip.x + fx + reach, 0.015f + fy);
            if (settle > 0) target = Vector2.Lerp(target, new Vector2(hip.x + (front > 0 ? 0.25f : -0.18f), 0.015f), settle);
            // Ground space is the Visual transform: the rig itself moves when the body crouches or lunges.
            SolveLimb(upper, lower, rig.parent.TransformPoint(target), 0.30f, 0.34f, front > 0 ? 1 : -1);
        }

        // ---------------------------------------------------------------- Runtime helpers
        private SpriteRenderer nockedArrow, chargeGlow;
        private ProjectileWeapon rangedWeapon;

        /// <summary>Arrow drawn on the string between the drawing hand and the bow. Hidden on release.</summary>
        public void ConfigureNockedArrow(Sprite arrowSprite)
        {
            if (arrowSprite == null || nockedArrow != null) return;
            nockedArrow = PixelFx.CreateRenderer("RT_NockedArrow", transform, arrowSprite, 1, Color.white);
            nockedArrow.transform.localScale = Vector3.one * (0.72f / Mathf.Max(0.01f, arrowSprite.bounds.size.y));
            nockedArrow.enabled = false;
        }

        /// <summary>Energy gathered at the staff head while the mage channels.</summary>
        public void ConfigureChargeGlow(Sprite glowSprite)
        {
            if (glowSprite == null || chargeGlow != null) return;
            chargeGlow = PixelFx.CreateRenderer("RT_ChargeGlow", transform, glowSprite, 2, new Color(0.65f, 1f, 0.7f, 0.9f));
            chargeGlow.enabled = false;
            rangedWeapon = GetComponent<ProjectileWeapon>();
        }

        public float MageCharge => mageCharge;
        public bool ArrowNocked => archerDraw >= 0;

        private void HideHelpers()
        {
            archerDraw = mageCharge = -1;
            if (nockedArrow != null) nockedArrow.enabled = false;
            if (chargeGlow != null) chargeGlow.enabled = false;
            SetBackForearmFront(false);
        }

        private void UpdateHelpers()
        {
            float fade = driver != null ? driver.Fade : 1;
            if (nockedArrow != null)
            {
                bool show = !dead && archerDraw >= 0 && backElbow != null && frontHand != null;
                nockedArrow.enabled = show;
                if (show)
                {
                    Vector2 hand = backElbow.TransformPoint(new Vector3(0, -0.30f, 0));
                    Vector2 grip = frontHand.position;
                    Vector2 dir = grip - hand;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right * (driver != null ? driver.Facing : 1);
                    dir.Normalize();
                    const float length = 0.72f;
                    nockedArrow.transform.position = (Vector3)(hand + dir * length * 0.5f);
                    nockedArrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90);
                    var c = nockedArrow.color; c.a = fade; nockedArrow.color = c;
                }
            }
            if (chargeGlow != null)
            {
                bool show = !dead && mageCharge > 0;
                chargeGlow.enabled = show;
                if (show)
                {
                    Vector2 at = rangedWeapon != null ? rangedWeapon.MuzzlePosition : (Vector2)transform.position + Vector2.up * 2;
                    float pulse = 1 + Mathf.Sin(Time.time * 18f) * 0.12f;
                    float size = Mathf.Lerp(0.12f, 0.42f, mageCharge) * pulse;
                    chargeGlow.transform.position = at;
                    chargeGlow.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, chargeGlow.sprite.bounds.size.y));
                    var c = chargeGlow.color; c.a = Mathf.Lerp(0.35f, 0.95f, mageCharge) * fade; chargeGlow.color = c;
                }
            }
        }

        private static float Swing(float t) => t < 0.3f ? 0 : t < 0.5f ? Ease((t - 0.3f) / 0.2f) : 1 - Ease((t - 0.5f) / 0.5f);
        private static float Ease(float t) => Mathf.SmoothStep(0, 1, t);
        private static void Angle(Transform part, float angle) { if (part != null) part.localRotation = Quaternion.Euler(0, 0, angle); }
        private static void SetPosition(Transform part, float x, float y) { if (part != null) part.localPosition = new Vector3(x, y, 0); }
    }
}
