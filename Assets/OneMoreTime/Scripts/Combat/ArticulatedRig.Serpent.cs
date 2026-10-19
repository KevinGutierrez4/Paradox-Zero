using UnityEngine;

namespace OneMoreTime
{
    /// <summary>Pose request for Jörmungandr. All values are 0..1 unless stated.</summary>
    public struct SerpentPoseInput
    {
        public float rise;        // 1 = fully emerged, 0 = sunk by riseDepth
        public float riseDepth;   // rig-local units hidden at rise 0
        public float neckLift;    // raise the neck (presentation / roar)
        public float headTilt;    // degrees, + = snout up (roar leans the head back)
        public float reach;       // neck extension towards the target (bite)
        public float reachDrop;   // how much the extended head dips (-1..1), aims the bite at the player
        public float coil;        // pull the head back (anticipation)
        public float jawOpen;     // articulated jaw opening
        public float attackHead;  // blend to the open-mouth head drawing
        public float roar;        // blend to the roaring head drawing
        public float tremble;     // high-frequency head vibration
        public float bodyWave;    // extra undulation of torso, tail and back
        public float slam;        // torso slams forward/down
        public float eyeGlow;     // glow intensity
        public float time;        // gameplay time used for tremble/flicker
    }

    public interface ISerpentPoseSource
    {
        bool HasSerpentPose { get; }
        SerpentPoseInput SerpentPose { get; }
    }

    public sealed partial class ArticulatedRig
    {
        private ISerpentPoseSource serpentSource;
        private Transform coil;
        private Vector2 neckRest = new Vector2(0.1f, 1.85f);
        private SpriteRenderer neutralHead, neutralJaw, attackHeadSprite, roarHeadSprite, eyeGlow;
        private SpriteRenderer[] bodyArc, bodyFins, neckFill;

        private void BindSerpent()
        {
            if (kind != RigKind.Serpent || rig == null) return;
            serpentSource = GetComponent<ISerpentPoseSource>();
            coil = rig.Find("Coil");
            if (neck != null) neckRest = neck.localPosition;
            neutralHead = head != null ? head.Find("Sprite")?.GetComponent<SpriteRenderer>() : null;
            neutralJaw = jaw != null ? jaw.Find("Sprite")?.GetComponent<SpriteRenderer>() : null;
        }

        /// <summary>Called by BossAI once its runtime parts (alternate heads, back, neck fill, eye glow) exist.</summary>
        public void ConfigureSerpentParts(SpriteRenderer attackHead, SpriteRenderer roarHead, SpriteRenderer eye,
            SpriteRenderer[] back, SpriteRenderer[] fins, SpriteRenderer[] neckSegments)
        {
            attackHeadSprite = attackHead; roarHeadSprite = roarHead; eyeGlow = eye;
            bodyArc = back; bodyFins = fins; neckFill = neckSegments;
            serpentSource = GetComponent<ISerpentPoseSource>();
        }

        public Transform SerpentHead => head;
        public Transform SerpentJaw => jaw;

        private void SerpentPose(float phase, float attack, float death)
        {
            if (serpentSource == null || !serpentSource.HasSerpentPose)
            {
                // Editor previews / no AI: the original idle sway.
                Angle(torso, Mathf.Sin(phase * 0.65f) * 4); Angle(neck, Mathf.Sin(phase * 0.65f - 0.5f) * 6);
                Angle(head, Mathf.Sin(phase * 0.65f - 1) * -5); Angle(tail, Mathf.Sin(phase * 0.65f - 1.5f) * 15);
                Angle(jaw, attack >= 0 ? -38 * Mathf.Sin(attack * Mathf.PI) : -3);
                Angle(rig, death >= 0 ? Mathf.Lerp(0, -70, Mathf.Clamp01(death / 0.6f)) : 0);
                return;
            }
            var p = serpentSource.SerpentPose;
            if (death >= 0)
            {
                // Defeat: the head droops, the jaw hangs open and the colossus sinks back into the abyss.
                float d = Ease(death / 2.4f);
                p.rise = Mathf.Min(p.rise, 1 - d); p.riseDepth = Mathf.Max(p.riseDepth, 5.5f);
                p.headTilt = Mathf.Lerp(p.headTilt, -28, Ease(death / 0.8f)); p.jawOpen = Mathf.Max(p.jawOpen, 0.45f);
                p.reach = p.coil = p.slam = 0; p.attackHead = 0; p.roar = 0; p.eyeGlow *= 1 - Ease(death / 0.9f);
                p.bodyWave = Mathf.Max(p.bodyWave, 0.6f * (1 - d));
            }
            float wave = p.bodyWave;
            float sway = Mathf.Sin(phase * 0.65f);
            SetPosition(rig, 0, -p.riseDepth * (1 - Mathf.Clamp01(p.rise)) - 0.25f * p.slam);
            Angle(rig, -7 * p.slam + 2 * sway * wave);
            Angle(torso, sway * 4 * (1 + wave) - p.coil * 6);
            Angle(coil, Mathf.Sin(phase * 0.65f + 1.2f) * 2.5f * wave);
            if (neck != null)
            {
                neck.localPosition = new Vector3(neckRest.x + p.reach * 1.5f - p.coil * 0.35f,
                    neckRest.y + p.neckLift * 0.3f - p.reach * (0.5f + 0.35f * p.reachDrop), 0);
                Angle(neck, Mathf.Sin(phase * 0.65f - 0.5f) * 6 * (1 + wave) + p.neckLift * 10 - p.reach * (22 + 10 * p.reachDrop) + p.coil * 14);
            }
            float tremble = p.tremble * Mathf.Sin(p.time * 60f) * 3f;
            Angle(head, p.headTilt - Mathf.Sin(phase * 0.65f - 1) * 5 + tremble + p.reach * 16);
            Angle(jaw, -3 - 45 * Mathf.Clamp01(p.jawOpen));
            Angle(tail, Mathf.Sin(phase * 0.65f - 1.5f) * 15 * (1 + wave) - 7.5f);

            // Drawing swaps: brief, eased crossfades so two heads are never visible half-and-half for long.
            float roar = Ease(Mathf.Clamp01((p.roar - 0.3f) / 0.4f));
            float open = Ease(Mathf.Clamp01((p.attackHead - 0.3f) / 0.4f));
            float neutralAlpha = 1 - Mathf.Max(roar, open);
            float fade = driver != null ? driver.Fade : 1;
            SetAlpha(neutralHead, neutralAlpha * fade); SetAlpha(neutralJaw, neutralAlpha * fade);
            SetAlpha(attackHeadSprite, open * (1 - roar) * fade);
            SetAlpha(roarHeadSprite, roar * fade);
            if (roarHeadSprite != null) Angle(roarHeadSprite.transform, -p.headTilt * 0.5f);
            if (eyeGlow != null)
            {
                float flicker = 0.85f + 0.15f * Mathf.Sin(p.time * 23f);
                eyeGlow.enabled = p.eyeGlow > 0.01f;
                eyeGlow.transform.localScale = Vector3.one * (0.35f + 0.5f * p.eyeGlow) * flicker;
                SetAlpha(eyeGlow, Mathf.Clamp01(p.eyeGlow) * 0.85f * fade);
            }
            PoseBack(phase, wave, fade);
            PoseNeckFill(fade);
        }

        private void PoseBack(float phase, float wave, float fade)
        {
            if (bodyArc == null || bodyArc.Length < 2) return;
            int n = bodyArc.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = BackPoint(Mathf.Max(0, i - 1), n, phase, wave, out _);
                Vector2 c = BackPoint(i, n, phase, wave, out float hump);
                Vector2 b = BackPoint(Mathf.Min(n - 1, i + 1), n, phase, wave, out _);
                var t = bodyArc[i].transform;
                t.localPosition = new Vector3(c.x, c.y, 0);
                t.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg + 180);
                // Segments "dive" below the surface when the hump goes negative.
                float alpha = Ease(Mathf.Clamp01((hump + 0.25f) / 0.45f)) * fade;
                SetAlpha(bodyArc[i], alpha);
                if (bodyFins != null && i < bodyFins.Length) SetAlpha(bodyFins[i], alpha);
            }
        }

        private static Vector2 BackPoint(int i, int n, float phase, float wave, out float hump)
        {
            float s = (float)i / (n - 1);
            hump = Mathf.Sin(s * Mathf.PI * 1.6f + 0.4f - phase * 0.9f);
            return new Vector2(-1.2f - s * 5f, 0.25f + Mathf.Max(0, hump) * (0.9f + 0.5f * wave));
        }

        private void PoseNeckFill(float fade)
        {
            if (neckFill == null || neckFill.Length == 0 || head == null) return;
            Vector2 a = new Vector2(0, 1.65f);
            Vector2 b = rig.InverseTransformPoint(head.TransformPoint(new Vector3(-0.15f, -0.35f, 0)));
            Vector2 d = b - a; float length = d.magnitude;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float alpha = Ease(Mathf.Clamp01((length - 0.62f) / 0.13f)) * fade; // hidden at rest (≈0.5), shown once the neck stretches
            for (int i = 0; i < neckFill.Length; i++)
            {
                float s = (i + 0.5f) / neckFill.Length;
                var t = neckFill[i].transform;
                t.localPosition = (Vector3)(a + d * s);
                t.localRotation = Quaternion.Euler(0, 0, angle);
                SetAlpha(neckFill[i], alpha);
            }
        }

        private static void SetAlpha(SpriteRenderer r, float a)
        {
            if (r == null) return;
            var c = r.color; c.a = Mathf.Clamp01(a); r.color = c;
            r.enabled = a > 0.003f;
        }

    }
}
