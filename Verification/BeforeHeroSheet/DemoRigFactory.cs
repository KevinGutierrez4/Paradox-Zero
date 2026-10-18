#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneMoreTime.Editor
{
    internal static class DemoRigFactory
    {
        private const string Art = "Assets/OneMoreTime/Art/Originales/";
        internal static Material Material;
        internal static Sprite Solid;
        internal static string SpriteDirectory;
        private static readonly Dictionary<string, bool[]> MaskCache = new Dictionary<string, bool[]>();
        private static readonly Dictionary<string, Rect> BoundsCache = new Dictionary<string, Rect>();

        internal static Transform Create(GameObject actor, string kind)
        {
            var visual = Child(actor.transform, "Visual", Vector2.zero);
            visual.gameObject.AddComponent<SortingGroup>();
            var rig = Child(visual, "Rig", Vector2.zero);
            bool wolf = kind.Contains("Wolf");
            if (kind == "Boss") Serpent(rig);
            else if (wolf) Wolf(rig, kind == "WhiteWolf");
            else Humanoid(rig, kind);
            var articulated = actor.GetComponent<ArticulatedRig>();
            if (articulated == null) articulated = actor.AddComponent<ArticulatedRig>();
            DemoSceneBuilder.Int(articulated, "kind", kind == "Player" ? 0 : kind == "Archer" ? 1 : kind == "Mage" ? 2 : wolf ? 3 : 4);
            articulated.Bind();
            articulated.EvaluatePose(0, 0, true, 0, 0, -1);
            return visual;
        }

        private static void Humanoid(Transform rig, string kind)
        {
            bool player = kind == "Player", mage = kind == "Mage";
            string folder = player ? "02_guerrero_hacha" : mage ? "04_mago" : "03_guerrero_arco";
            var pelvis = Child(rig, "Pelvis", new Vector2(0, 0.86f));
            HumanLeg(pelvis, "BackLeg", folder, mage ? "pierna_4" : player ? "pierna_4" : "pierna_2", -0.12f, 1);
            HumanLeg(pelvis, "FrontLeg", folder, mage ? "pierna_4" : player ? "pierna_4" : "pierna_2", 0.12f, 5);
            var torso = Child(pelvis, "Torso", Vector2.zero);
            // The front warrior artwork contains an original Qebin badge; preserve it as supplied.
            Image(torso, "Body", folder, mage ? "torso_frente" : player ? "torso_frente" : "torso", new Vector2(0, 0.28f), 0.94f, 4);
            var head = Child(torso, "Head", new Vector2(0.03f, 0.76f));
            Image(head, "Sprite", folder, mage ? "cabeza_casco" : player ? "cabeza_perfil" : "cabeza_humana", new Vector2(0, 0.11f), mage ? 0.56f : 0.48f, 8);
            Child(head, "FacingPoint", new Vector2(0.25f, 0.1f));
            HumanArm(torso, "BackArm", folder, -0.19f, 0, true);
            var arm = HumanArm(torso, "FrontArm", folder, 0.2f, 9, false);
            var hand = Child(arm.Find("Lower"), "Hand", new Vector2(0, -0.32f));
            var weapon = Child(hand, "Weapon", Vector2.zero);
            string art = player ? "hacha_1" : mage ? "baston_2" : "arco";
            // The axe artwork already includes the gripping hand. It replaces the arm's terminal hand.
            Image(weapon, "Sprite", folder, art, player ? new Vector2(0.18f, 0.04f) : mage ? new Vector2(0, 0.2f) : new Vector2(0.02f, 0.02f), player ? 1.04f : mage ? 1.35f : 0.92f, 12);
            var muzzle = Child(weapon, "Muzzle", mage ? new Vector2(0, 0.87f) : new Vector2(0.12f, 0.18f));
            if (!player && !mage) Image(torso, "Quiver", folder, "carcaj", new Vector2(-0.3f, 0.35f), 0.72f, -1);
            if (mage) Image(torso, "Cape", folder, "capa_2", new Vector2(-0.19f, 0.17f), 1.03f, -1);
        }

        private static void HumanLeg(Transform parent, string name, string folder, string file, float x, int order)
        {
            var upper = Child(parent, name, new Vector2(x, 0));
            SliceImage(upper, "Sprite", folder, file, new Rect(0, 0.49f, 1, 0.51f), new Vector2(0.5f, 0.96f), 0.47f, order);
            var lower = Child(upper, "Lower", new Vector2(0, -0.44f));
            SliceImage(lower, "Sprite", folder, file, new Rect(0, 0, 1, 0.54f), new Vector2(0.5f, 0.96f), 0.48f, order + 1);
        }

        private static Transform HumanArm(Transform torso, string name, string folder, float x, int order, bool back)
        {
            var arm = Child(torso, name, new Vector2(x, 0.62f));
            var lower = Child(arm, "Lower", new Vector2(0, -0.29f));
            if (folder == "03_guerrero_arco")
            {
                // Archer sheets provide separate upper arms and forearm/glove pieces.
                JointImage(arm, folder, back ? "brazo_2" : "brazo_1", new Rect(0, 0, 1, 1),
                    new Vector2(back ? 0.30f : 0.70f, 0.83f), new Vector2(back ? 0.60f : 0.40f, 0.17f), 0.29f, order);
                JointImage(lower, folder, back ? "mano_guante_2" : "mano_guante_1", new Rect(0, 0, 1, 1),
                    new Vector2(0.29f, 0.83f), new Vector2(0.65f, 0.19f), 0.32f, order + 1);
            }
            else
            {
                bool warrior = folder == "02_guerrero_hacha";
                string file = warrior ? (back ? "brazo_1" : "brazo_2") : (back ? "brazo_izquierdo_1" : "brazo_derecho_1");
                Vector2 shoulder = back ? new Vector2(0.70f, 0.87f) : new Vector2(0.32f, 0.88f);
                Vector2 elbow = back ? new Vector2(0.40f, 0.52f) : new Vector2(0.51f, 0.53f);
                Vector2 wrist = back ? new Vector2(0.27f, 0.15f) : new Vector2(0.70f, 0.16f);
                if (!warrior)
                {
                    shoulder = back ? new Vector2(0.70f, 0.83f) : new Vector2(0.42f, 0.86f);
                    elbow = back ? new Vector2(0.44f, 0.47f) : new Vector2(0.38f, 0.48f);
                    wrist = back ? new Vector2(0.32f, 0.12f) : new Vector2(0.64f, 0.13f);
                }
                JointImage(arm, folder, file, new Rect(0, elbow.y - 0.04f, 1, 1 - elbow.y + 0.04f), shoulder, elbow, 0.29f, order);
                // The axe supplies the gripping hand; leave the front wrist ready to meet it.
                float bottom = warrior && !back ? 0.14f : 0;
                JointImage(lower, folder, file, new Rect(0, bottom, 1, elbow.y + 0.04f - bottom), elbow, wrist, 0.32f, order + 1);
            }
            return arm;
        }

        private static void JointImage(Transform joint, string folder, string file, Rect region,
            Vector2 start, Vector2 end, float boneLength, int order)
        {
            Rect clean = CleanBounds(Art + folder + "/" + file + ".png");
            Vector2 deltaPixels = Vector2.Scale(end - start, clean.size);
            Vector2 pivot = new Vector2((start.x - region.x) / region.width, (start.y - region.y) / region.height);
            var sprite = Derived(folder, file, region, pivot, "_joint_v2_" + joint.parent.name + "_" + joint.name);
            var image = Child(joint, "Sprite", Vector2.zero);
            var renderer = image.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = Material; renderer.sortingOrder = order;
            // Neutralise the inclination painted into each piece before rotating the actual joint.
            image.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(deltaPixels.x, -deltaPixels.y) * Mathf.Rad2Deg);
            image.localScale = Vector3.one * (boneLength * 100 / deltaPixels.magnitude);
        }

        private static void Wolf(Transform rig, bool white)
        {
            string folder = white ? "01_lobo_blanco" : "05_lobo_azul";
            string front = white ? "pata_delantera_muslo" : "pata_delantera_sup_1";
            string frontLower = white ? "pata_delantera_pierna" : "pata_delantera_inf_1";
            string back = white ? "pata_trasera_muslo" : "pata_trasera_sup_1";
            string backLower = white ? "pata_trasera_pierna" : "pata_trasera_inf_1";
            AnimalLeg(rig, "FarFrontLeg", folder, front, frontLower, new Vector2(0.4f, 0.65f), 0);
            AnimalLeg(rig, "FarBackLeg", folder, back, backLower, new Vector2(-0.55f, 0.65f), 0);
            AnimalLeg(rig, "BackLeg", folder, back, backLower, new Vector2(-0.67f, 0.65f), 5);
            AnimalLeg(rig, "FrontLeg", folder, front, frontLower, new Vector2(0.52f, 0.65f), 6);
            Image(rig, "Torso", folder, "torso", new Vector2(-0.1f, 0.88f), 0.82f, 3);
            Image(rig, "Haunch", folder, "cadera", new Vector2(-0.65f, 0.86f), 0.58f, 4);
            var neck = Child(rig, "Neck", new Vector2(0.55f, 0.92f));
            Image(neck, "Sprite", folder, white ? "cuello_pecho" : "cuello", new Vector2(0.12f, 0.1f), 0.68f, 5);
            var head = Child(neck, "Head", new Vector2(0.4f, 0.31f));
            Child(head, "FacingPoint", new Vector2(0.3f, 0));
            Image(head, "Sprite", folder, white ? "cabeza" : "cabeza_perfil", Vector2.zero, 0.59f, 8);
            var jaw = Child(head, "Jaw", new Vector2(0.13f, -0.15f));
            Image(jaw, "Sprite", folder, "mandibula", new Vector2(0.07f, 0), 0.17f, 9);
            var tail = Child(rig, "Tail", new Vector2(-0.85f, 0.85f));
            Image(tail, "Sprite", folder, "cola_base", new Vector2(-0.22f, 0), 0.48f, 2);
        }

        private static void AnimalLeg(Transform rig, string name, string folder, string upper, string lower, Vector2 position, int order)
        {
            var limb = Child(rig, name, position);
            SliceImage(limb, "Sprite", folder, upper, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.9f), 0.39f, order);
            var knee = Child(limb, "Lower", new Vector2(0, -0.3f));
            SliceImage(knee, "Sprite", folder, lower, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.9f), 0.38f, order + 1);
        }

        private static void Serpent(Transform rig)
        {
            const string folder = "06_serpiente_azul";
            Image(rig, "Torso", folder, "segmento_curva_1", new Vector2(-0.2f, 1.2f), 2.25f, 2);
            Image(rig, "Coil", folder, "segmento_recto_1", new Vector2(-0.2f, 0.5f), 0.9f, 3);
            var neck = Child(rig, "Neck", new Vector2(0.1f, 1.85f));
            var head = Child(neck, "Head", new Vector2(0.35f, 0.55f));
            Child(head, "FacingPoint", new Vector2(0.6f, 0));
            Image(head, "Sprite", folder, "cabeza_neutra", Vector2.zero, 1.65f, 5);
            var jaw = Child(head, "Jaw", new Vector2(0.4f, -0.36f));
            Image(jaw, "Sprite", folder, "mandibula_1", new Vector2(0.18f, 0), 0.4f, 6);
            Child(head, "Muzzle", new Vector2(0.8f, -0.1f));
            var tail = Child(rig, "Tail", new Vector2(-1.15f, 0.5f));
            Image(tail, "Sprite", folder, "cola_aletas", new Vector2(-0.3f, 0), 0.8f, 1);
        }

        private static void Image(Transform parent, string name, string folder, string file, Vector2 position, float height, int order)
        {
            var image = Child(parent, name, position);
            var sprite = Derived(folder, file, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            var renderer = image.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = Material; renderer.sortingOrder = order;
            image.localScale = Vector3.one * (height / sprite.bounds.size.y);
        }
        private static void SliceImage(Transform parent, string name, string folder, string file, Rect region, Vector2 pivot, float height, int order)
        {
            var sprite = Derived(folder, file, region, pivot);
            var image = Child(parent, name, Vector2.zero);
            var renderer = image.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = Material; renderer.sortingOrder = order;
            image.localScale = Vector3.one * (height / sprite.bounds.size.y);
        }

        internal static Sprite Derived(string folder, string file, Rect region, Vector2 pivot, string variant = "")
        {
            string source = Art + folder + "/" + file + ".png";
            string name = folder + "_" + file + "_" + region.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "_" + region.height.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            name += variant;
            bool wolf = folder == "01_lobo_blanco" || folder == "05_lobo_azul";
            if (wolf) name += "_fur_v6";
            string path = SpriteDirectory + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (existing != null) return existing;
            if (!File.Exists(source)) throw new FileNotFoundException(source);
            // Derived assets own a texture copy so the supplied PNGs and their GUIDs stay intact.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = file, filterMode = FilterMode.Point };
            texture.LoadImage(File.ReadAllBytes(source));
            Rect clean = wolf ? WolfSpriteRepair.Prepare(texture, folder, file) : CleanBounds(source);
            var rect = new Rect(clean.x + clean.width * region.x, clean.y + clean.height * region.y, clean.width * region.width, clean.height * region.height);
            // Clear only pixels outside the selected connected piece in this derived copy.
            // This removes crop-guide bridges and adjacent fragments without touching the PNG.
            if (!wolf)
            {
                var mask = MaskCache[source]; var pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) if (!mask[i]) pixels[i].a = 0;
                texture.SetPixels32(pixels); texture.Apply();
            }
            var sprite = Sprite.Create(texture, rect, pivot, 100, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, path);
            AssetDatabase.AddObjectToAsset(texture, sprite);
            EditorUtility.SetDirty(sprite); EditorUtility.SetDirty(texture);
            return sprite;
        }

        // Read a temporary copy; leave original importers, PNGs, GUIDs and sprite references intact.
        private static Rect CleanBounds(string path)
        {
            if (BoundsCache.TryGetValue(path, out var found)) return found;
            var readable = new Texture2D(2, 2); readable.LoadImage(File.ReadAllBytes(path));
            int w = readable.width, h = readable.height; var pixels = readable.GetPixels32();
            var visited = new bool[pixels.Length]; var queue = new int[pixels.Length];
            bool Foreground(int p)
            {
                var c = pixels[p];
                // Cutouts include pale grid/crop-guide lines joining neighbouring pieces.
                return c.a >= 100 && !(c.r >= 225 && c.g >= 225 && c.b >= 225 && Math.Max(c.r, Math.Max(c.g, c.b)) - Math.Min(c.r, Math.Min(c.g, c.b)) < 8);
            }
            int largest = 0; bool[] main = null; Rect result = new Rect(0, 0, w, h);
            for (int start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || !Foreground(start)) continue;
                int read = 0, write = 1, minX = w, minY = h, maxX = 0, maxY = 0; queue[0] = start; visited[start] = true;
                while (read < write)
                {
                    int p = queue[read++], x = p % w, y = p / w;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    if (x > 0) Visit(p - 1); if (x < w - 1) Visit(p + 1); if (y > 0) Visit(p - w); if (y < h - 1) Visit(p + w);
                }
                if (write > largest)
                {
                    largest = write; result = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                    main = new bool[pixels.Length]; for (int i = 0; i < write; i++) main[queue[i]] = true;
                }
                void Visit(int p) { if (!visited[p] && Foreground(p)) { visited[p] = true; queue[write++] = p; } }
            }
            // Fill enclosed light regions, preserving the original white fur and highlights.
            var outside = new bool[pixels.Length]; int readOutside = 0, writeOutside = 0;
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (readOutside < writeOutside)
            {
                int p = queue[readOutside++], x = p % w, y = p / w;
                if (x > 0) Seed(p - 1); if (x < w - 1) Seed(p + 1); if (y > 0) Seed(p - w); if (y < h - 1) Seed(p + w);
            }
            void Seed(int p) { if (!main[p] && !outside[p]) { outside[p] = true; queue[writeOutside++] = p; } }
            for (int i = 0; i < main.Length; i++) if (!outside[i] && pixels[i].a >= 100) main[i] = true;
            UnityEngine.Object.DestroyImmediate(readable); BoundsCache[path] = result; MaskCache[path] = main; return result;
        }
        internal static Transform Child(Transform parent, string name, Vector2 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); child.localPosition = position; return child;
        }
    }
}
#endif
