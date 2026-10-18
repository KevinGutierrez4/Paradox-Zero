using UnityEngine;

namespace OneMoreTime
{
    /// <summary>
    /// Builds every functional scenery piece from the atlas sprites with its collider and behaviour.
    /// The same code is used by the editor tool that saves the reusable prefabs and, as a fallback, at runtime.
    /// Kinds: IcePlatform, SerpentScalePlatform, StoneCover, WoodenBarricade, BreakableCover, ArcherPost, IceSpikes,
    /// BoneSpikes, VenomVent, JawTrap, IceGeyser, DangerousIce, Crack, UnstablePlatform, MovingPlatform, IcicleTrap,
    /// and "Deco:SpriteId" for purely decorative props (no collider, no gameplay).
    /// </summary>
    public static class ArenaPropFactory
    {
        public static readonly string[] PrefabKinds =
        {
            "IcePlatform", "SerpentScalePlatform", "StoneCover", "WoodenBarricade", "BreakableCover", "ArcherPost",
            "IceSpikes", "BoneSpikes", "VenomVent", "JawTrap", "IceGeyser", "DangerousIce", "Crack", "UnstablePlatform",
            "MovingPlatform", "IcicleTrap"
        };

        private const int PropOrder = -3;

        private static SpriteRenderer Art(GameObject go, ArenaPropLibrary lib, string id, int order, Material material, string name = "Art")
        {
            var child = new GameObject(name);
            child.transform.SetParent(go.transform, false);
            var r = child.AddComponent<SpriteRenderer>();
            r.sprite = lib != null ? lib.Sprite(id) : null;
            r.sortingOrder = order;
            if (material != null) r.sharedMaterial = material;
            return r;
        }

        private static Bounds SpriteBounds(SpriteRenderer r) => r.sprite != null ? r.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);

        public static GameObject Create(string kind, ArenaPropLibrary lib, Material material)
        {
            var go = new GameObject(kind);
            int prop = PropLayers.Prop, ground = PropLayers.Ground;
            if (kind.StartsWith("Deco:"))
            {
                var deco = Art(go, lib, kind.Substring(5), -20, material);
                deco.color = new Color(0.86f, 0.86f, 0.9f);
                return go;
            }
            switch (kind)
            {
                case "IcePlatform": Platform(go, lib, "CrackedIceSlab", material, 0.82f); break;
                case "SerpentScalePlatform": Platform(go, lib, "SerpentScalePlatform", material, 0.85f); break;
                case "ArcherPost":
                    Platform(go, lib, "ArcherPostPlatform", material, 0.8f);
                    go.AddComponent<ArcherPost>();
                    break;
                case "UnstablePlatform":
                {
                    Platform(go, lib, "FloatingIceSmall", material, 0.8f);
                    var t = go.AddComponent<ArenaTrap>();
                    t.Configure(TrapKind.UnstablePlatform, 0, new Vector2(1.8f, 0.6f), new Vector2(0, 0.5f), lib?.Sprite("FloatingIceSmall"), null, null, 0.1f, 0.7f, 0.6f, 2.6f);
                    break;
                }
                case "MovingPlatform":
                {
                    Platform(go, lib, "FloatingIceTiny", material, 0.8f);
                    var rb = go.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
                    go.AddComponent<MovingPlatform>();
                    break;
                }
                case "StoneCover": Cover(go, lib, "StoneCover", material, true, true, false, 1.18f); break;
                case "WoodenBarricade": Cover(go, lib, "WoodenBarricade", material, false, true, false, 1.1f); break;
                case "BreakableCover": Cover(go, lib, "Crate", material, true, true, true, 1.25f); break;
                case "IceSpikes": Trap(go, lib, TrapKind.Spikes, "IceSpikes", null, null, 9, new Vector2(1.5f, 0.75f), new Vector2(0, 0.38f), 2.0f, 0.55f, 1.1f, 0.5f, material); break;
                case "BoneSpikes": Trap(go, lib, TrapKind.Spikes, "BoneSpikes", null, null, 9, new Vector2(1.5f, 0.75f), new Vector2(0, 0.38f), 2.2f, 0.55f, 1.0f, 0.5f, material); break;
                case "VenomVent": Trap(go, lib, TrapKind.VenomVent, "VenomVentSmall", "VenomVent", "VenomVentActive", 4, new Vector2(2.0f, 2.2f), new Vector2(0, 1.2f), 2.6f, 0.9f, 1.8f, 0.8f, material); break;
                case "JawTrap": Trap(go, lib, TrapKind.JawTrap, "JawTrapOpen", "JawTrapSnap", "JawTrapClosed", 12, new Vector2(1.0f, 0.6f), new Vector2(0, 0.3f), 0.1f, 0.16f, 0.25f, 1.8f, material); break;
                case "IceGeyser": Trap(go, lib, TrapKind.IceGeyser, "GeyserIdle", "GeyserIdle", "GeyserActive", 10, new Vector2(0.9f, 3.0f), new Vector2(0, 1.5f), 2.6f, 0.8f, 1.0f, 0.6f, material); break;
                case "DangerousIce":
                {
                    var t = Trap(go, lib, TrapKind.DangerousIce, "CrackedIceSlab", null, null, 6, new Vector2(2.0f, 0.5f), new Vector2(0, 0.2f), 0.1f, 0.9f, 0.3f, 3.0f, material);
                    t.transform.GetChild(0).localScale = new Vector3(0.85f, 0.38f, 1);
                    break;
                }
                case "Crack":
                {
                    var t = Trap(go, lib, TrapKind.Crack, "LowStoneSpikes", null, null, 8, new Vector2(1.5f, 1.6f), new Vector2(0, 0.8f), 2.4f, 0.8f, 0.7f, 0.6f, material);
                    t.transform.GetChild(0).localScale = new Vector3(0.9f, 0.6f, 1);
                    break;
                }
                case "IcicleTrap":
                {
                    var frame = Art(go, lib, "IcicleFrame", PropOrder - 1, material, "Frame");
                    var icicle = new GameObject("Icicle");
                    icicle.transform.SetParent(go.transform, false);
                    icicle.transform.localPosition = new Vector3(0.1f, frame.sprite != null ? frame.sprite.bounds.max.y - 0.18f : 2.2f, 0);
                    var r = Art(icicle, lib, "Icicle", PropOrder, material);
                    var t = icicle.AddComponent<ArenaTrap>();
                    t.Configure(TrapKind.Icicle, 11, new Vector2(0.6f, 0.9f), new Vector2(0, -0.4f), lib?.Sprite("Icicle"), null, null, 0.1f, 0.55f, 1.2f, 3.0f);
                    break;
                }
                default:
                    Debug.LogWarning("Tipo de elemento de escenario desconocido: " + kind);
                    break;
            }
            return go;
        }

        private static void Platform(GameObject go, ArenaPropLibrary lib, string sprite, Material material, float widthFraction)
        {
            go.layer = PropLayers.Ground; // enemies (archers on posts) must be able to stand on platforms too
            var r = Art(go, lib, sprite, PropOrder, material);
            var b = SpriteBounds(r);
            // The walkable surface sits slightly below the top edge (snow cap / icicle tips are decoration).
            float top = b.max.y - b.size.y * 0.16f;
            OneWayPlatform.Setup(go, new Vector2(b.size.x * widthFraction, 0.28f), new Vector2(b.center.x, top - 0.14f));
        }

        private static void Cover(GameObject go, ArenaPropLibrary lib, string sprite, Material material, bool breath, bool projectiles, bool breakable, float scale)
        {
            go.layer = PropLayers.Prop;
            var r = Art(go, lib, sprite, PropOrder, material);
            r.transform.localScale = Vector3.one * scale;
            var b = SpriteBounds(r);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(b.size.x * scale * 0.72f, b.size.y * scale * 0.84f);
            box.offset = new Vector2(0, box.size.y * 0.5f);
            var cover = go.AddComponent<ArenaCover>();
            cover.Configure(breath, projectiles, breakable, 30);
        }

        private static ArenaTrap Trap(GameObject go, ArenaPropLibrary lib, TrapKind kind, string idle, string warning, string active, float damage,
            Vector2 area, Vector2 offset, float idleT, float warnT, float activeT, float coolT, Material material)
        {
            Art(go, lib, idle, PropOrder + 1, material);
            var t = go.AddComponent<ArenaTrap>();
            t.Configure(kind, damage, area, offset, lib?.Sprite(idle), warning != null ? lib?.Sprite(warning) : null, active != null ? lib?.Sprite(active) : null, idleT, warnT, activeT, coolT);
            return t;
        }
    }
}
