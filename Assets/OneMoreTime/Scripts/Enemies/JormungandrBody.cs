using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneMoreTime
{
    /// <summary>Sprites of one Jörmungandr phase (cut from the phase atlases in Art/JormungandrPhases).</summary>
    [Serializable]
    public sealed class JormungandrPhaseArt
    {
        public Sprite skull, jaw, mouth, headOpen, headRoar, headBite, segment, tail, cone;
        public Sprite[] fins = new Sprite[0];
    }

    /// <summary>
    /// Visual body of Jörmungandr. The serpent never walks, flies or floats: the arena floor is its back.
    /// Only the head, the independent jaw, the visible neck (rising from a snow mound) and the scale plates
    /// embedded in the ground move. Every region can morph between the three phase drawings independently
    /// (body plates → neck → head, in that order) so damage visibly transforms the creature.
    /// Coordinates are "art space": +x points from the neck base towards the player.
    /// </summary>
    public sealed class JormungandrBody : MonoBehaviour
    {
        public struct Pose
        {
            /// <summary>Head joint relative to the neck base (art space, units).</summary>
            public Vector2 head;
            public float tilt, jaw, open, roar, snarl, tremble, glow, rise;
            /// <summary>0 = relaxed curve, 1 = neck pulled tight (contraction).</summary>
            public float contract;
        }

        private sealed class Morph
        {
            public SpriteRenderer a, b;
            public Transform t;
            public float value = 1; // phase as float: 1..3
            public float alpha = 1;
            public Color tint = Color.white;
            public Color baseTint = Color.white;
        }

        [SerializeField] private JormungandrPhaseArt[] phases = new JormungandrPhaseArt[3];
        [SerializeField] private Sprite moundSprite;
        [SerializeField] private Sprite moundSideSprite;
        [Tooltip("Lomos de escamas del propio escenario (la espalda de la serpiente que asoma de la nieve).")]
        [SerializeField] private Sprite[] plateSprites = new Sprite[0];
        [SerializeField] private Color[] plateTints = { new Color(0.86f, 0.93f, 1f), new Color(0.7f, 0.8f, 1f), new Color(1f, 0.62f, 0.6f) };
        [Tooltip("Desplazamientos medidos en el atlas (unidades, espacio de la cabeza).")]
        [SerializeField] private Vector2 jawHinge = new Vector2(1.097f, 0.153f);
        [SerializeField] private Vector2 eyeOffset = new Vector2(1.736f, 0.625f);
        [SerializeField] private Vector2 mouthOffset = new Vector2(2.54f, 0.04f);
        [SerializeField, Range(0, 40)] private float maxJawAngle = 17f;
        [SerializeField, Min(0.3f)] private float segmentSpacing = 0.62f;
        [SerializeField, Min(4)] private int maxSegments = 18;
        [SerializeField, Min(2)] private int bodyPlates = 8;

        private Transform root, neckRoot, head, jawPivot, plateRoot;
        private readonly List<Morph> segments = new List<Morph>();
        private readonly List<Morph> fins = new List<Morph>();
        private readonly List<Morph> plates = new List<Morph>();
        private readonly List<float> plateX = new List<float>();
        private readonly List<float> plateLift = new List<float>();
        private Morph skull, jaw, mouth, headOpen, headRoar, headBite, mound, moundSide, tail;
        private SpriteRenderer eyeGlow;
        private SortingGroup group;
        private Func<float, float> ground;
        private float baseX, flash;
        private int facing = -1;
        private bool built;
        public Pose pose;
        public bool Built => built;
        public float BaseX => baseX;
        public int Facing => facing;
        public int PlateCount => plates.Count;
        public Transform Head => head;
        public float NeckMorph { get; private set; } = 1;
        public float HeadMorph { get; private set; } = 1;
        public bool HasArt => phases != null && phases.Length >= 3 && phases[0] != null && phases[0].skull != null;

        public Vector2 HeadWorld => head != null ? (Vector2)head.position : (Vector2)transform.position;
        public Vector2 MouthWorld => head != null ? (Vector2)head.TransformPoint(mouthOffset) : HeadWorld;
        public Vector2 EyeWorld => head != null ? (Vector2)head.TransformPoint(eyeOffset) : HeadWorld;
        /// <summary>World position of an art-space point relative to the neck base.</summary>
        public Vector2 ArtToWorld(Vector2 art) => root != null ? (Vector2)root.TransformPoint(art) : (Vector2)transform.position + art;
        public float PlateWorldX(int i) => plateX[Mathf.Clamp(i, 0, plateX.Count - 1)];

        public static Pose Rest(int stage)
        {
            return new Pose { head = new Vector2(2.7f, 4.7f + 0.15f * (stage - 1)), tilt = 0, glow = 0.45f + 0.25f * (stage - 1), rise = 1 };
        }

        /// <summary>Creates every renderer. The base sits behind a snow mound at <paramref name="neckBaseX"/>.</summary>
        public void Build(Func<float, float> groundAt, float neckBaseX, int faceDirection, float arenaStart, float frontX, Material material)
        {
            ground = groundAt; baseX = neckBaseX; facing = faceDirection < 0 ? -1 : 1;
            if (built) Clear();
            built = true;
            root = new GameObject("JormungandrBody").transform;
            root.position = new Vector3(baseX, ground(baseX), 0);
            root.localScale = new Vector3(facing, 1, 1);
            group = root.gameObject.AddComponent<SortingGroup>(); group.sortingOrder = -5;
            neckRoot = new GameObject("Neck").transform; neckRoot.SetParent(root, false);
            for (int i = 0; i < maxSegments; i++)
            {
                segments.Add(MakeMorph("Segment_" + i, neckRoot, 20 + (maxSegments - i), material));
                fins.Add(MakeMorph("Fin_" + i, neckRoot, 2 + i % 3, material));
            }
            head = new GameObject("Head").transform; head.SetParent(root, false);
            mouth = MakeMorph("Mouth", head, 60, material);
            jawPivot = new GameObject("JawPivot").transform; jawPivot.SetParent(head, false); jawPivot.localPosition = jawHinge;
            jaw = MakeMorph("Jaw", jawPivot, 61, material);
            skull = MakeMorph("Skull", head, 62, material);
            headBite = MakeMorph("HeadBite", head, 64, material);
            headOpen = MakeMorph("HeadOpen", head, 65, material);
            headRoar = MakeMorph("HeadRoar", head, 66, material);
            eyeGlow = PixelFx.CreateRenderer("EyeGlow", head, PixelFx.SoftDot, 140, new Color(1f, 0.35f, 0.15f, 0));
            eyeGlow.transform.localPosition = eyeOffset; eyeGlow.transform.localScale = Vector3.one * 0.9f;
            // Snow mound in front of the neck base: the neck emerges from the ground, never from thin air.
            mound = MakeMorph("Mound", root, 120, material); mound.t.localPosition = new Vector3(0.1f, -0.25f, 0);
            moundSide = MakeMorph("MoundSide", root, 121, material); moundSide.t.localPosition = new Vector3(-1.6f, -0.3f, 0);
            mound.a.sprite = moundSprite; moundSide.a.sprite = moundSideSprite != null ? moundSideSprite : moundSprite;
            mound.t.localScale = new Vector3(1.05f, 1, 1); moundSide.t.localScale = new Vector3(-0.8f, 0.8f, 1);
            // Body plates: the serpent's back pokes out of the arena floor between the boss and the player.
            plateRoot = new GameObject("JormungandrBackPlates").transform;
            var plateGroup = plateRoot.gameObject.AddComponent<SortingGroup>(); plateGroup.sortingOrder = -8;
            float from = frontX - 1.4f, to = arenaStart + 2.4f;
            for (int i = 0; i < bodyPlates; i++)
            {
                float x = Mathf.Lerp(from, to, bodyPlates == 1 ? 0 : (float)i / (bodyPlates - 1));
                var m = MakeMorph("Plate_" + i, plateRoot, 10 + i % 2, material);
                float sc = plateSprites != null && plateSprites.Length > 0 ? 0.62f + 0.1f * ((i * 5) % 3) : 0.62f + 0.1f * ((i * 5) % 3);
                m.t.localScale = new Vector3((i % 2 == 0 ? 1 : -1) * sc, sc, 1);
                plates.Add(m); plateX.Add(x); plateLift.Add(0);
            }
            tail = MakeMorph("TailTip", plateRoot, 12, material);
            tail.t.localScale = new Vector3(facing * 0.75f, 0.75f, 1);
            SetPhaseImmediate(1);
            pose = Rest(1); pose.rise = 0;
            Apply();
        }

        private static Morph MakeMorph(string name, Transform parent, int order, Material material)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false);
            var a = PixelFx.CreateRenderer("A", t, null, order * 2, Color.white);
            var b = PixelFx.CreateRenderer("B", t, null, order * 2 + 1, new Color(1, 1, 1, 0));
            if (material != null) { a.sharedMaterial = material; b.sharedMaterial = material; }
            return new Morph { a = a, b = b, t = t };
        }

        public void Clear()
        {
            if (root != null) Destroy(root.gameObject);
            if (plateRoot != null) Destroy(plateRoot.gameObject);
            segments.Clear(); fins.Clear(); plates.Clear(); plateX.Clear(); plateLift.Clear();
            built = false;
        }

        private void OnDestroy() { Clear(); }

        // ------------------------------------------------------------------ Morphing
        private JormungandrPhaseArt Art(int phase) => phases[Mathf.Clamp(phase - 1, 0, phases.Length - 1)];

        public Sprite FinFor(int phase) => HasArt ? Fin(Art(phase), 4) : null;
        /// <summary>Back hump used by the scale-wave attacks (the serpent's back rising out of the floor).</summary>
        public Sprite WaveSprite => plateSprites != null && plateSprites.Length > 0 ? plateSprites[plateSprites.Length - 1] : FinFor(1);
        public Color WaveTint(int phase) => PlateTint(phase);
        public Sprite ConeFor(int phase) => HasArt ? (Art(Mathf.Max(2, phase)).cone) : null;

        private static Sprite Fin(JormungandrPhaseArt art, int index)
            => art.fins != null && art.fins.Length > 0 ? art.fins[Mathf.Clamp(index, 0, art.fins.Length - 1)] : null;

        private void SetMorph(Morph m, Func<JormungandrPhaseArt, Sprite> pick, float value)
        {
            if (m == null) return;
            m.value = Mathf.Clamp(value, 1, 3);
            int lo = Mathf.FloorToInt(m.value), hi = Mathf.Min(3, lo + 1);
            float k = m.value - lo;
            m.a.sprite = pick(Art(lo));
            m.b.sprite = pick(Art(hi));
            Paint(m);
            m.b.color = Tint(m, k * m.alpha);
            m.b.enabled = k > 0.001f && m.b.sprite != null;
        }

        private Color Tint(Morph m, float alpha)
        {
            Color c = Color.Lerp(m.tint * m.baseTint, new Color(1f, 0.45f, 0.4f), flash);
            c.a *= alpha; return c;
        }

        private void Paint(Morph m)
        {
            m.a.color = Tint(m, m.alpha);
            m.a.enabled = m.alpha > 0.001f && m.a.sprite != null;
        }

        public void SetPhaseImmediate(int phase)
        {
            for (int i = 0; i < plates.Count; i++) SetPlateMorph(i, phase);
            SetNeckMorph(phase); SetHeadMorph(phase);
            if (tail != null) SetMorph(tail, a => a.tail, phase);
            SetMorph(mound, a => moundSprite, 1); SetMorph(moundSide, a => moundSideSprite != null ? moundSideSprite : moundSprite, 1);
        }

        private Color PlateTint(float value)
        {
            if (plateTints == null || plateTints.Length < 3) return Color.white;
            value = Mathf.Clamp(value, 1, 3);
            int lo = Mathf.FloorToInt(value), hi = Mathf.Min(3, lo + 1);
            return Color.Lerp(plateTints[lo - 1], plateTints[hi - 1], value - lo);
        }

        public void SetPlateMorph(int index, float value)
        {
            if (index < 0 || index >= plates.Count) return;
            if (plateSprites != null && plateSprites.Length > 0)
            {
                // The body regions are the serpent's own back in the scenery; each phase recolours it (ice → cracked → blood).
                var m = plates[index];
                m.value = Mathf.Clamp(value, 1, 3);
                m.a.sprite = plateSprites[index % plateSprites.Length];
                m.b.sprite = null; m.b.enabled = false;
                m.baseTint = PlateTint(value);
                Paint(m);
                if (index == plates.Count - 1 && tail != null) SetMorph(tail, a => a.tail, value);
                return;
            }
            int f = 3 + index % 3;
            SetMorph(plates[index], a => Fin(a, f), value);
            if (index == plates.Count - 1 && tail != null) SetMorph(tail, a => a.tail, value);
        }

        public float PlateMorph(int index) => index >= 0 && index < plates.Count ? plates[index].value : 1;

        public void SetNeckMorph(float value)
        {
            NeckMorph = value;
            for (int i = 0; i < segments.Count; i++)
            {
                SetMorph(segments[i], a => a.segment, value);
                int f = 5 - Mathf.Min(5, i / 2);
                SetMorph(fins[i], a => Fin(a, f), value);
            }
        }

        /// <summary>Morph only the neck segments up to <paramref name="fraction"/> of the length (base first).</summary>
        public void SetNeckMorphProgress(int fromPhase, float fraction)
        {
            int n = Mathf.Max(1, segments.Count);
            for (int i = 0; i < segments.Count; i++)
            {
                float k = Mathf.Clamp01(fraction * n - i);
                SetMorph(segments[i], a => a.segment, fromPhase + k);
                int f = 5 - Mathf.Min(5, i / 2);
                SetMorph(fins[i], a => Fin(a, f), fromPhase + k);
            }
            NeckMorph = fromPhase + Mathf.Clamp01(fraction);
        }

        public void SetHeadMorph(float value)
        {
            HeadMorph = value;
            SetMorph(skull, a => a.skull, value); SetMorph(jaw, a => a.jaw, value); SetMorph(mouth, a => a.mouth, value);
            SetMorph(headOpen, a => a.headOpen, value); SetMorph(headRoar, a => a.headRoar, value); SetMorph(headBite, a => a.headBite, value);
        }

        public void SetPlateLift(int index, float lift) { if (index >= 0 && index < plateLift.Count) plateLift[index] = lift; }

        public void SetPlateTint(Color c) { foreach (var p in plates) p.tint = c; if (tail != null) tail.tint = c; }
        public Vector3 PlateTop(int i) => plates[Mathf.Clamp(i, 0, plates.Count - 1)].a.bounds.max;
        public void SetPlateTint(int index, Color c) { if (index >= 0 && index < plates.Count) plates[index].tint = c; }
        public void SetBodyTint(Color c)
        {
            foreach (var s in segments) s.tint = c;
            foreach (var f in fins) f.tint = c;
            foreach (var m in new[] { skull, jaw, mouth, headOpen, headRoar, headBite }) if (m != null) m.tint = c;
        }

        public void Flash() { flash = 1; }

        // ------------------------------------------------------------------ Frame
        private void LateUpdate()
        {
            if (!built) return;
            flash = Mathf.MoveTowards(flash, 0, Time.deltaTime * 6f);
            Apply();
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1 - t;
            return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        }

        private void Apply()
        {
            float time = Time.time;
            float rise = Mathf.Clamp01(pose.rise);
            // Emergence: the whole neck slides up out of the mound (never above it before it is revealed).
            Vector2 sink = new Vector2(0, -(1 - rise) * 7.5f);
            Vector2 shake = pose.tremble > 0 ? new Vector2(Mathf.Sin(time * 61f), Mathf.Cos(time * 53f)) * 0.06f * pose.tremble : Vector2.zero;
            Vector2 headJoint = pose.head + sink + shake;
            Vector2 p0 = new Vector2(0, -1.6f) + sink * 0.2f;
            float slack = 1 - Mathf.Clamp01(pose.contract);
            Vector2 p1 = new Vector2(-0.9f * slack + 0.2f, 2.0f + 0.4f * pose.contract) + sink * 0.6f;
            Vector2 p2 = new Vector2(headJoint.x - 2.3f * (0.6f + 0.4f * slack), headJoint.y + 0.45f * slack);
            // Length → number of visible rings, so stretching never opens gaps.
            float length = 0; Vector2 prev = p0;
            for (int i = 1; i <= 16; i++) { var q = Bezier(p0, p1, p2, headJoint, i / 16f); length += Vector2.Distance(prev, q); prev = q; }
            int count = Mathf.Clamp(Mathf.CeilToInt(length / segmentSpacing) + 1, 3, segments.Count);
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i]; var f = fins[i];
                bool on = i < count;
                s.t.gameObject.SetActive(on); f.t.gameObject.SetActive(on && i < count - 1);
                if (!on) continue;
                float t = (float)i / (count - 1) * 0.965f;
                Vector2 p = Bezier(p0, p1, p2, headJoint, t), q = Bezier(p0, p1, p2, headJoint, Mathf.Min(1, t + 0.01f));
                float ang = Mathf.Atan2(q.y - p.y, q.x - p.x) * Mathf.Rad2Deg;
                float scale = 1f - 0.18f * t;
                s.t.localPosition = p; s.t.localRotation = Quaternion.Euler(0, 0, ang); s.t.localScale = Vector3.one * scale;
                Vector2 normal = new Vector2(-Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad));
                float sway = Mathf.Sin(time * 2.2f + i * 0.7f) * 4f;
                f.t.localPosition = p + normal * 0.48f * scale;
                f.t.localRotation = Quaternion.Euler(0, 0, ang - 48f + sway);
                f.t.localScale = Vector3.one * scale * 0.72f;
            }
            float breathe = Mathf.Sin(time * 1.6f) * 1.5f;
            head.localPosition = headJoint;
            head.localRotation = Quaternion.Euler(0, 0, pose.tilt + breathe * (1 - pose.roar));
            jawPivot.localRotation = Quaternion.Euler(0, 0, -maxJawAngle * Mathf.Clamp01(pose.jaw));
            mouth.alpha = Mathf.Clamp01(pose.jaw * 4f);
            // Drawn expressions: the strongest one is opaque, the closed skull+jaw rig underneath.
            float open = Mathf.Clamp01(pose.open), roar = Mathf.Clamp01(pose.roar), snarl = Mathf.Clamp01(pose.snarl);
            float strongest = Mathf.Max(open, Mathf.Max(roar, snarl));
            bool rigVisible = strongest < 0.999f;
            skull.alpha = rigVisible ? 1 : 0; jaw.alpha = rigVisible ? 1 : 0; if (!rigVisible) mouth.alpha = 0;
            headOpen.alpha = open; headRoar.alpha = roar; headBite.alpha = snarl;
            foreach (var m in new[] { skull, jaw, mouth, headOpen, headRoar, headBite })
            {
                Paint(m);
                float k = m.value - Mathf.Floor(m.value);
                m.b.color = Tint(m, k * m.alpha); m.b.enabled = k > 0.001f && m.alpha > 0.001f && m.b.sprite != null;
            }
            eyeGlow.color = new Color(1f, 0.35f, 0.15f, Mathf.Clamp01(pose.glow) * (0.55f + 0.15f * Mathf.Sin(time * 5f)) * rise);
            foreach (var s in segments) RefreshColor(s);
            foreach (var f in fins) RefreshColor(f);
            // Back plates: half buried in the floor; "lift" raises them out of the ground (scale wave, contractions).
            for (int i = 0; i < plates.Count; i++)
            {
                float x = plateX[i];
                float lift = plateLift[i];
                bool humps = plateSprites != null && plateSprites.Length > 0;
                plates[i].t.position = new Vector3(x, ground(x) + (humps ? -0.12f : -0.55f) + lift, 0);
                // A slow breathing swell: the back is alive, but it never travels.
                float swell = humps ? 1f + 0.025f * Mathf.Sin(time * 1.1f + i * 0.9f) : 1f;
                var baseScale = plates[i].t.localScale;
                plates[i].t.localScale = new Vector3(baseScale.x, Mathf.Abs(baseScale.x) * swell, 1);
                plates[i].t.localRotation = humps ? Quaternion.identity : Quaternion.Euler(0, 0, facing * (8f + 6f * Mathf.Sin(time * 1.3f + i)) * (1 - lift * 0.3f));
                RefreshColor(plates[i]);
            }
            if (tail != null && plateX.Count > 0)
            {
                float tx = plateX[plateX.Count - 1] + facing * 1.8f;
                tail.t.position = new Vector3(tx, ground(tx) - 0.35f, 0);
                tail.t.localRotation = Quaternion.Euler(0, 0, facing * (24f + 5f * Mathf.Sin(time * 1.7f)));
                RefreshColor(tail);
            }
            RefreshColor(mound); RefreshColor(moundSide);
        }

        private void RefreshColor(Morph m)
        {
            if (m == null) return;
            Paint(m);
            float k = m.value - Mathf.Floor(m.value);
            m.b.color = Tint(m, k * m.alpha);
        }

        /// <summary>Defeat: the head crashes onto the floor and the neck sinks back under the snow.</summary>
        public IEnumerator Defeat()
        {
            var from = pose; float t = 0;
            while (t < 1.6f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0, 1, t / 1.6f);
                pose.head = Vector2.Lerp(from.head, new Vector2(4.2f, 0.9f), k);
                pose.tilt = Mathf.Lerp(from.tilt, -18f, k);
                pose.open = 0; pose.roar = 0; pose.snarl = 0; pose.jaw = Mathf.Lerp(from.jaw, 0.6f, k);
                pose.glow = Mathf.Lerp(from.glow, 0, k); pose.contract = 0;
                Apply();
                yield return null;
            }
            t = 0;
            while (t < 1.4f)
            {
                t += Time.unscaledDeltaTime;
                pose.rise = 1 - Mathf.SmoothStep(0, 0.35f, t / 1.4f);
                Apply();
                yield return null;
            }
        }
    }
}
