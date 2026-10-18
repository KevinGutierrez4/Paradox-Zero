using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>
    /// Places the functional scenery of each phase on the serpent's back: platforms, covers and traps chosen per
    /// winter (1: bones and stone, 2: ice, 3: venom, icicles and fissures), with difficulty rising from winter 1 to 3.
    /// The boss arena only receives useful cover (two solid covers and three high platforms). Uses the generated
    /// prefabs when they exist (Tools > One More Time > 9) and the same factory otherwise.
    /// </summary>
    public sealed class StageBuilder : MonoBehaviour
    {
        public struct Placement
        {
            public string kind; public float t; public float height; public float travel;
            public Placement(string k, float at, float h = 0, float move = 0) { kind = k; t = at; height = h; travel = move; }
        }

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Bounds> blocked = new List<Bounds>();
        private GameManager game;
        private Material material;
        private ArenaPropLibrary library;
        public IReadOnlyList<GameObject> Props => spawned;
        public int Count => spawned.Count;

        public void Initialize(GameManager manager, Material spriteMaterial)
        {
            game = manager; material = spriteMaterial; library = ArenaPropLibrary.Load();
        }

        public void Clear()
        {
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear(); blocked.Clear();
        }

        /// <summary>True if x is occupied by a solid cover or a trap (enemies never spawn there).</summary>
        public bool IsBlocked(float x, float margin = 0.6f)
        {
            foreach (var b in blocked) if (x > b.min.x - margin && x < b.max.x + margin) return true;
            return false;
        }

        public float FreeX(float x, float min, float max)
        {
            for (int i = 0; i < 12 && IsBlocked(x); i++) x = Mathf.Clamp(x + (i % 2 == 0 ? 1 : -1) * (1.2f + i * 0.6f), min, max);
            return x;
        }

        public static List<Placement> Layout(int phase, bool boss)
        {
            var l = new List<Placement>();
            if (boss)
            {
                // Only what the fight needs: covers against wind/breath and platforms above the low hazards.
                l.Add(new Placement("SerpentScalePlatform", 0.10f, 2.2f));
                l.Add(new Placement("StoneCover", 0.24f));
                l.Add(new Placement("IcePlatform", 0.40f, 2.2f));
                l.Add(new Placement("StoneCover", 0.55f));
                l.Add(new Placement("SerpentScalePlatform", 0.70f, 2.2f));
                return l;
            }
            switch (phase)
            {
                case 1: // Winter 1 · learning: one of each basic element, generous gaps.
                    l.Add(new Placement("Deco:BannerPost", 0.02f));
                    l.Add(new Placement("SerpentScalePlatform", 0.22f, 2.2f));
                    l.Add(new Placement("StoneCover", 0.38f));
                    l.Add(new Placement("BoneSpikes", 0.52f));
                    l.Add(new Placement("Deco:TuskRibs", 0.62f));
                    l.Add(new Placement("SerpentScalePlatform", 0.74f, 2.2f));
                    l.Add(new Placement("Deco:StoneCairn", 0.9f));
                    break;
                case 2:
                    l.Add(new Placement("Deco:TuskFenceDeco", 0.05f));
                    l.Add(new Placement("JawTrap", 0.2f));
                    l.Add(new Placement("ArcherPost", 0.36f, 2.4f));
                    l.Add(new Placement("WoodenBarricade", 0.5f));
                    l.Add(new Placement("BoneSpikes", 0.62f));
                    l.Add(new Placement("IcePlatform", 0.76f, 2.2f));
                    l.Add(new Placement("StoneCover", 0.88f));
                    break;
                case 3: // Winter 2 · ice: geysers, unstable and moving ice.
                    l.Add(new Placement("Deco:IcePillar", 0.03f));
                    l.Add(new Placement("IcePlatform", 0.14f, 2.2f));
                    l.Add(new Placement("IceGeyser", 0.26f));
                    l.Add(new Placement("ArcherPost", 0.38f, 2.4f));
                    l.Add(new Placement("DangerousIce", 0.5f));
                    l.Add(new Placement("UnstablePlatform", 0.6f, 2.2f));
                    l.Add(new Placement("StoneCover", 0.7f));
                    l.Add(new Placement("IceSpikes", 0.8f));
                    l.Add(new Placement("MovingPlatform", 0.9f, 2.2f, 3f));
                    break;
                case 4:
                    l.Add(new Placement("IceGeyser", 0.1f));
                    l.Add(new Placement("ArcherPost", 0.2f, 2.4f));
                    l.Add(new Placement("JawTrap", 0.3f));
                    l.Add(new Placement("MovingPlatform", 0.4f, 2.2f, 3.2f));
                    l.Add(new Placement("BreakableCover", 0.5f));
                    l.Add(new Placement("DangerousIce", 0.58f));
                    l.Add(new Placement("UnstablePlatform", 0.66f, 2.2f));
                    l.Add(new Placement("ArcherPost", 0.76f, 2.4f));
                    l.Add(new Placement("IceSpikes", 0.84f));
                    l.Add(new Placement("IceGeyser", 0.92f));
                    break;
                default: // Winter 3 · Ragnarök: venom, falling icicles, fissures, more archers.
                    l.Add(new Placement("Deco:SkullTotem", 0.02f));
                    l.Add(new Placement("VenomVent", 0.1f));
                    l.Add(new Placement("ArcherPost", 0.18f, 2.4f));
                    l.Add(new Placement("IcicleTrap", 0.27f));
                    l.Add(new Placement("Crack", 0.35f));
                    l.Add(new Placement("BreakableCover", 0.43f));
                    l.Add(new Placement("MovingPlatform", 0.51f, 2.2f, 3.4f));
                    l.Add(new Placement("VenomVent", 0.6f));
                    l.Add(new Placement("ArcherPost", 0.68f, 2.4f));
                    l.Add(new Placement("JawTrap", 0.75f));
                    l.Add(new Placement("IcicleTrap", 0.82f));
                    l.Add(new Placement("StoneCover", 0.88f));
                    l.Add(new Placement("Crack", 0.94f));
                    break;
            }
            return l;
        }

        public void Build(int phase, float start, float end, bool boss)
        {
            Clear();
            if (game == null) return;
            // Keep the arrival area, the tutorial strip of phase 1 and the exit portal clear.
            float from = start + (phase == 1 && !boss ? 16f : 6.5f), to = boss ? start + 26f : end - 6f;
            foreach (var p in Layout(phase, boss))
            {
                float x = Mathf.Lerp(from, to, p.t);
                var go = Spawn(p.kind);
                if (go == null) continue;
                float gy = game.GroundHeight(x);
                // Heights are walkable-surface heights above the floor (all reachable with one jump: apex ≈ 2.8).
                float y = gy - 0.04f;
                var surface = go.GetComponent<OneWayPlatform>();
                if (p.height > 0 && surface != null)
                {
                    var box = go.GetComponent<BoxCollider2D>();
                    y = gy + p.height - (box.offset.y + box.size.y * 0.5f);
                }
                go.transform.position = new Vector3(x, y, 0);
                if (p.kind == "MovingPlatform") go.GetComponent<MovingPlatform>()?.Configure(new Vector2(p.travel, 0), 4.2f);
                spawned.Add(go);
                if (go.GetComponent<ArenaCover>() != null || go.GetComponent<ArenaTrap>() != null && p.height <= 0)
                    blocked.Add(new Bounds(new Vector3(x, gy + 1), new Vector3(2.2f, 2, 1)));
            }
            if (phase == 1 && !boss)
            {
                // Tutorial strip: a small crate to jump over and a slow spike trap to practise the dodge.
                var crate = Spawn("TutorialCrate");
                if (crate != null) { float x = start + 7.5f; crate.transform.position = new Vector3(x, game.GroundHeight(x) - 0.03f, 0); spawned.Add(crate); }
                var spikes = Spawn("TutorialSpikes");
                if (spikes != null) { float x = start + 12.5f; spikes.transform.position = new Vector3(x, game.GroundHeight(x) - 0.03f, 0); spawned.Add(spikes); }
            }
            Physics2D.SyncTransforms();
        }

        private GameObject Spawn(string kind)
        {
            if (kind == "TutorialCrate")
            {
                var crate = ArenaPropFactory.Create("WoodenBarricade", library, material);
                crate.name = "TutorialCrate";
                var art = crate.GetComponentInChildren<SpriteRenderer>();
                if (art != null && library != null) { art.sprite = library.Sprite("CrateSmall"); art.transform.localScale = Vector3.one; }
                var box = crate.GetComponent<BoxCollider2D>();
                if (art != null && art.sprite != null) { var b = art.sprite.bounds; box.size = new Vector2(b.size.x * 0.75f, b.size.y * 0.8f); box.offset = new Vector2(0, box.size.y * 0.5f); }
                return crate;
            }
            if (kind == "TutorialSpikes")
            {
                var spikes = ArenaPropFactory.Create("IceSpikes", library, material);
                spikes.name = "TutorialSpikes";
                return spikes;
            }
            var prefab = library != null ? library.Prefab(kind) : null;
            var go = prefab != null ? Instantiate(prefab) : ArenaPropFactory.Create(kind, library, material);
            go.name = kind;
            foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(true)) if (material != null) r.sharedMaterial = material;
            return go;
        }
    }
}
