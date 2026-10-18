from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoRigFactory.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('var articulated = actor.AddComponent<ArticulatedRig>();','var articulated = actor.GetComponent<ArticulatedRig>();\n            if (articulated == null) articulated = actor.AddComponent<ArticulatedRig>();')
s=s.replace('''            HumanArm(torso, "BackArm", folder, player ? "brazo_2" : mage ? "brazo_derecho_1" : "brazo_4", -0.19f, 0);
            var arm = HumanArm(torso, "FrontArm", folder, player ? "brazo_2" : mage ? "brazo_derecho_1" : "brazo_4", 0.2f, 9);''','''            Child(head, "FacingPoint", new Vector2(0.25f, 0.1f));
            HumanArm(torso, "BackArm", folder, -0.19f, 0, true);
            var arm = HumanArm(torso, "FrontArm", folder, 0.2f, 9, false);''')
a=s.index('        private static Transform HumanArm(');b=s.index('        private static void Wolf(',a)
s=s[:a]+'''        private static Transform HumanArm(Transform torso, string name, string folder, float x, int order, bool back)
        {
            var arm = Child(torso, name, new Vector2(x, 0.62f));
            var lower = Child(arm, "Lower", new Vector2(0, -0.29f));
            if (folder == "03_guerrero_arco")
            {
                // Archer sheets provide separate upper arms and forearm/glove pieces.
                JointImage(arm, folder, back ? "brazo_2" : "brazo_1", new Rect(0, 0, 1, 1),
                    new Vector2(0.70f, 0.83f), new Vector2(0.40f, 0.17f), 0.29f, order);
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

'''+s[b:]
s=s.replace('internal static Sprite Derived(string folder, string file, Rect region, Vector2 pivot)','internal static Sprite Derived(string folder, string file, Rect region, Vector2 pivot, string variant = "")')
s=s.replace('string path = SpriteDirectory + "/" + name + ".asset";','name += variant;\n            string path = SpriteDirectory + "/" + name + ".asset";')
# Add world-facing markers to quadrupeds and boss too.
s=s.replace('var head = Child(neck, "Head", new Vector2(0.4f, 0.31f));','var head = Child(neck, "Head", new Vector2(0.4f, 0.31f));\n            Child(head, "FacingPoint", new Vector2(0.3f, 0));')
s=s.replace('var head = Child(neck, "Head", new Vector2(0.35f, 0.55f));','var head = Child(neck, "Head", new Vector2(0.35f, 0.55f));\n            Child(head, "FacingPoint", new Vector2(0.6f, 0));')
p.write_text(s,encoding='utf-8')
