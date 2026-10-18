#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace OneMoreTime.Editor
{
    internal static class ShieldHeroFactory
    {
        internal const string DirectoryPath = "Assets/OneMoreTime/Art/Originales/GuerreroEscudo";
        private const string Source = DirectoryPath + "/WarriorParts.png";
        private static Texture2D atlas;
        private static Dictionary<string, Sprite> sprites;
        private static bool collecting;
        private sealed class PartSpec { internal Rect rect; internal Vector2 pivot; internal Vector2[] vertices; internal int[] indices; }
        private static readonly Dictionary<string, PartSpec> specs = new Dictionary<string, PartSpec>();
        private static readonly List<Sprite> temporarySprites = new List<Sprite>();
        internal static bool Available => File.Exists(Source);
        internal static void Prepare()
        {
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Source);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple; importer.spritePixelsPerUnit = 100; importer.isReadable = true;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Source);
            specs.Clear(); collecting = true;
            var sample = new GameObject("MeasureShieldParts");
            try { Create(sample.transform); }
            finally { UnityEngine.Object.DestroyImmediate(sample); foreach (var sprite in temporarySprites) UnityEngine.Object.DestroyImmediate(sprite); temporarySprites.Clear(); collecting = false; }
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(s => s.name, s => s.spriteID);
            var rects = specs.Select(pair => new SpriteRect { name = pair.Key, rect = pair.Value.rect, pivot = pair.Value.pivot / pair.Value.rect.size, alignment = SpriteAlignment.Custom, spriteID = previous.TryGetValue(pair.Key, out var id) ? id : GUID.Generate() }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            var meshes = provider.GetDataProvider<ISpriteMeshDataProvider>();
            foreach (var rect in rects)
            {
                var spec = specs[rect.name];
                meshes.SetVertices(rect.spriteID, spec.vertices.Select(p => new Vertex2DMetaData { position = p + spec.pivot }).ToArray());
                meshes.SetIndices(rect.spriteID, spec.indices);
                meshes.SetEdges(rect.spriteID, new Vector2Int[0]);
            }
            provider.Apply(); importer.SaveAndReimport();
            sprites = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<Sprite>().ToDictionary(s => s.name);
            if (sprites["SkinJoint"].vertices.Length != 18) throw new InvalidOperationException("Unity did not preserve the measured joint geometry.");
        }
        internal static void Create(Transform rig)
        {
            if (!collecting && sprites == null) Prepare();
            var pelvis = Child(rig, "Pelvis", new Vector2(0, 0.83f));
            Leg(pelvis, "BackLeg", false, -0.18f, 1);
            Leg(pelvis, "FrontLeg", true, 0.18f, 4);
            var torso = Child(pelvis, "Torso", Vector2.zero);
            Part(torso, "Body", "Torso", new Rect(305, 27, 285, 282), new Vector2(447, 279), 0.74f / 276, 6);
            var cloth = Child(pelvis, "Cloth", Vector2.zero);
            Part(cloth, "Sprite", "Pelvis", new Rect(605, 59, 272, 245), new Vector2(743, 112), 0.55f / 266, 7);
            var head = Child(torso, "Head", new Vector2(-0.015f, 0.59f));
            Part(head, "Sprite", "Head", new Rect(60, 76, 186, 191), new Vector2(130, 229), 0.47f / 185, 9);
            Child(head, "FacingPoint", new Vector2(0.28f, 0.10f));
            var axeHand = Arm(torso, "FrontArm", false, new Vector2(-0.28f, 0.42f), 10);
            var axe = Child(axeHand, "AxeSocket", new Vector2(0, -0.11f));
            axe.localRotation = Quaternion.Euler(0, 0, -120);
            var axeImage = Part(axe, "Sprite", "Axe", new Rect(900, 586, 270, 284), new Vector2(981, 739), 0.67f / 278, 13);
            axeImage.GetComponent<SpriteRenderer>().sortingOrder = 170;
            axeImage.localRotation = Quaternion.Euler(0, 0, 25);
            Child(axe, "Grip", Vector2.zero); Child(axe, "BladeTip", new Vector2(0.23f, 0.29f));
            var shieldHand = Arm(torso, "BackArm", true, new Vector2(0.28f, 0.42f), 3);
            var shield = Child(shieldHand, "ShieldSocket", new Vector2(0, -0.11f));
            var shieldImage = Part(shield, "Sprite", "Shield", new Rect(1204, 585, 270, 279), new Vector2(1340, 726), 0.72f / 273, 15);
            shieldImage.localPosition = new Vector2(0.03f, 0.17f);
            Child(shield, "Grip", Vector2.zero);
            var swipe = Child(rig, "AxeSwipe", Vector2.zero).gameObject.AddComponent<AxeSwipe>(); swipe.Initialize(DemoRigFactory.Material);
            swipe.GetComponent<SpriteRenderer>().sortingOrder = 125;
        }
        private static Transform Arm(Transform torso, string name, bool shield, Vector2 shoulder, int order)
        {
            var upper = Child(torso, name, shoulder);
            SkinJoint(upper, 0.070f, order * 10 + 2);
            if (shield) Bone(upper, "ShieldUpperArm", new Rect(935, 65, 226, 221), new Vector2(973, 106), new Vector2(1118, 249), 0.30f, order);
            else Bone(upper, "AxeUpperArm", new Rect(58, 336, 202, 220), new Vector2(220, 366), new Vector2(93, 514), 0.30f, order);
            var lower = Child(upper, "Lower", new Vector2(0, -0.30f));
            SkinJoint(lower, 0.052f, (order + 1) * 10 - 1);
            if (shield) Bone(lower, "ShieldForearm", new Rect(1248, 59, 227, 237), new Vector2(1290, 98), new Vector2(1446, 265), 0.28f, order + 1);
            else Bone(lower, "AxeForearm", new Rect(354, 344, 200, 219), new Vector2(389, 374), new Vector2(522, 530), 0.28f, order + 1);
            var hand = Child(lower, "Hand", new Vector2(0, -0.28f));
            SkinJoint(hand, 0.037f, (order + 1) * 10 + 1);
            if (shield) Bone(hand, "ShieldHand", new Rect(1554, 127, 164, 127), new Vector2(1580, 190), new Vector2(1654, 193), 0.11f, 12);
            else Bone(hand, "AxeHand", new Rect(662, 413, 165, 133), new Vector2(692, 474), new Vector2(770, 480), 0.11f, 14);
            if (!shield) hand.Find("Sprite").GetComponent<SpriteRenderer>().sortingOrder = 180;
            return hand;
        }
        private static void Leg(Transform pelvis, string name, bool near, float x, int order)
        {
            var upper = Child(pelvis, name, new Vector2(x, -0.08f));
            SkinJoint(upper, 0.067f, order * 10 - 1);
            if (near) Bone(upper, "NearThigh", new Rect(934, 321, 169, 248), new Vector2(1053, 353), new Vector2(978, 529), 0.40f, order);
            else Bone(upper, "FarThigh", new Rect(79, 593, 174, 252), new Vector2(120, 630), new Vector2(207, 810), 0.40f, order);
            var knee = Child(upper, "Lower", new Vector2(0, -0.40f));
            SkinJoint(knee, 0.064f, (order + 1) * 10 - 1);
            if (near) Bone(knee, "NearShin", new Rect(1255, 316, 150, 253), new Vector2(1305, 339), new Vector2(1371, 534), 0.31f, order + 1);
            else Bone(knee, "FarShin", new Rect(365, 585, 145, 271), new Vector2(425, 617), new Vector2(468, 818), 0.31f, order + 1);
            var ankle = Child(knee, "Foot", new Vector2(0, -0.31f));
            SkinJoint(ankle, 0.052f, (order + 1) * 10 - 1, true);
            // These boot variants already include a calf; use only their foot below the ankle,
            // so the supplied separate shin is not duplicated inside a complete second boot.
            if (near) Part(ankle, "Sprite", "NearBootFoot", new Rect(1542, 480, 200, 97), new Vector2(1605, 505), 0.31f / 206, order + 1);
            else Part(ankle, "Sprite", "FarBootFoot", new Rect(657, 752, 183, 93), new Vector2(716, 776), 0.31f / 206, order + 1);
        }
        private static void Bone(Transform joint, string key, Rect topRect, Vector2 start, Vector2 end, float length, int order)
        {
            Vector2 delta = new Vector2(end.x - start.x, start.y - end.y);
            var image = Part(joint, "Sprite", key, topRect, start, length / delta.magnitude, order);
            image.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg);
            bool hand = key.EndsWith("Hand");
            // Clip only the assembly end caps in sprite geometry. Texture pixels stay untouched.
            // Small skin bridges overlap the cut at elbows/knees without duplicate black ovals.
            float begin = key.Contains("Shin") ? 0.035f : 0.023f;
            float finish = hand ? -0.1f : key.Contains("Thigh") || key.Contains("Shin") ? 0.040f : 0.024f;
            ClipCaps(image.GetComponent<SpriteRenderer>().sprite, topRect, start, delta,
                begin * delta.magnitude / length, finish * delta.magnitude / length);
        }
        private static void ClipCaps(Sprite sprite, Rect rect, Vector2 pivot, Vector2 delta, float begin, float finish)
        {
            if (!collecting) return;
            var polygon = new List<Vector2> { new Vector2(rect.xMin - pivot.x, pivot.y - rect.yMax), new Vector2(rect.xMax - pivot.x, pivot.y - rect.yMax), new Vector2(rect.xMax - pivot.x, pivot.y - rect.yMin), new Vector2(rect.xMin - pivot.x, pivot.y - rect.yMin) };
            Vector2 axis = delta.normalized;
            polygon = Clip(polygon, axis, begin, true);
            polygon = Clip(polygon, axis, delta.magnitude - finish, false);
            var spec = specs[sprite.name]; spec.vertices = polygon.ToArray();
            var indices = new List<int>();
            for (int i = 1; i + 1 < polygon.Count; i++) { indices.Add(0); indices.Add(i); indices.Add(i + 1); }
            spec.indices = indices.ToArray();
        }
        private static List<Vector2> Clip(List<Vector2> input, Vector2 axis, float plane, bool above)
        {
            var output = new List<Vector2>();
            for (int i = 0; i < input.Count; i++)
            {
                Vector2 a = input[i], b = input[(i + 1) % input.Count];
                float da = Vector2.Dot(a, axis) - plane, db = Vector2.Dot(b, axis) - plane;
                bool insideA = above ? da >= 0 : da <= 0, insideB = above ? db >= 0 : db <= 0;
                if (insideA) output.Add(a);
                if (insideA != insideB) output.Add(Vector2.LerpUnclamped(a, b, da / (da - db)));
            }
            return output;
        }
        private static void SkinJoint(Transform parent, float radius, int order, bool boot = false)
        {
            var image = Part(parent, "JointOverlap", boot ? "BootJoint" : "SkinJoint", boot ? new Rect(1320, 452, 4, 4) : new Rect(482, 139, 4, 4), boot ? new Vector2(1322, 454) : new Vector2(484, 141), radius / 2, 0);
            var draw = image.GetComponent<SpriteRenderer>(); draw.sortingOrder = order;
            if (!collecting) return;
            var spec = specs[draw.sprite.name]; spec.vertices = new Vector2[18]; spec.indices = new int[48];
            for (int i = 0; i <= 16; i++) spec.vertices[i + 1] = new Vector2(Mathf.Cos(i * Mathf.PI / 8), Mathf.Sin(i * Mathf.PI / 8)) * 2;
            for (int i = 0; i < 16; i++) { spec.indices[i * 3] = 0; spec.indices[i * 3 + 1] = i + 1; spec.indices[i * 3 + 2] = i + 2; }
        }
        private static Transform Part(Transform parent, string name, string key, Rect topRect, Vector2 topPivot, float unitsPerPixel, int order)
        {
            var rect = new Rect(topRect.x, atlas.height - topRect.yMax, topRect.width, topRect.height);
            var pixelPivot = new Vector2(topPivot.x - topRect.x, topRect.yMax - topPivot.y);
            Sprite sprite;
            if (collecting)
            {
                specs[key] = new PartSpec { rect = rect, pivot = pixelPivot, vertices = new[] { -pixelPivot, new Vector2(rect.width, 0) - pixelPivot, rect.size - pixelPivot, new Vector2(0, rect.height) - pixelPivot }, indices = new[] { 0, 1, 2, 0, 2, 3 } };
                sprite = Sprite.Create(atlas, rect, pixelPivot / rect.size, 100, 0, SpriteMeshType.FullRect); sprite.name = key; temporarySprites.Add(sprite);
            }
            else sprite = sprites[key];
            var image = Child(parent, name, Vector2.zero); image.localScale = Vector3.one * (unitsPerPixel * 100);
            var draw = image.gameObject.AddComponent<SpriteRenderer>(); draw.sprite = sprite; draw.sharedMaterial = DemoRigFactory.Material; draw.sortingOrder = order * 10;
            return image;
        }
        private static Transform Child(Transform parent, string name, Vector2 position) => DemoRigFactory.Child(parent, name, position);
    }
}
#endif
