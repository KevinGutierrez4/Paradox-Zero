from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoAnimatorFactory.cs')
s=p.read_text(encoding='utf-8-sig').replace('Create(string directory, bool player)','Create(string directory, string kind)').replace('var clip = Clip(name, player);','var clip = Clip(name, kind);')
a=s.index('        private static AnimationClip Clip(')
s=s[:a]+'''        private static AnimationClip Clip(string state, string kind)
        {
            bool attack = state == "Attack" || state == "CrouchAttack" || state == "Special";
            bool crouch = state == "Crouch" || state == "CrouchAttack";
            float length = attack ? (kind == "Player" ? 0.625f : 0.8f) : state == "Die" ? 0.65f : state == "Walk" ? 0.65f : 2f;
            var clip = new AnimationClip { name = kind + "_" + state, frameRate = 60 };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = state == "Idle" || state == "Walk" || state == "Crouch";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var sample = new GameObject("PoseBake");
            try
            {
                var visual = DemoRigFactory.Create(sample, kind);
                var pose = sample.GetComponent<ArticulatedRig>();
                var joints = visual.GetComponentsInChildren<Transform>();
                var curves = new Dictionary<Transform, AnimationCurve[]>();
                foreach (var joint in joints)
                {
                    if (joint == visual || joint.GetComponent<SpriteRenderer>() != null) continue;
                    curves[joint] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                }
                int samples = Mathf.CeilToInt(length * 60);
                for (int i = 0; i <= samples; i++)
                {
                    float t = length * i / samples, normalized = (float)i / samples;
                    pose.EvaluatePose(normalized * Mathf.PI * 2, state == "Walk" ? 7.5f : 0,
                        state != "Jump", crouch ? 1 : 0, state == "Jump" ? Mathf.Lerp(12, -12, normalized) : 0,
                        attack ? normalized : -1, state == "Die" ? t : -1);
                    foreach (var pair in curves)
                    {
                        pair.Value[0].AddKey(t, pair.Key.localPosition.x);
                        pair.Value[1].AddKey(t, pair.Key.localPosition.y);
                        pair.Value[2].AddKey(t, Mathf.DeltaAngle(0, pair.Key.localEulerAngles.z));
                    }
                }
                foreach (var pair in curves)
                {
                    string path = AnimationUtility.CalculateTransformPath(pair.Key, sample.transform);
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", pair.Value[0]);
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", pair.Value[1]);
                    clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", pair.Value[2]);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
            return clip;
        }
    }
}
#endif
'''
p.write_text(s,encoding='utf-8')
p=Path('Assets/OneMoreTime/Editor/DemoSceneBuilder.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('Root + "/Generated"','Root + "/Generated_Hacha"')
s=s.replace('Directory.CreateDirectory(generated + "/Animations/Player");\n            Directory.CreateDirectory(generated + "/Animations/Enemy");','Directory.CreateDirectory(generated + "/Sprites");\n            foreach (string kind in new[] { "Player", "Archer", "Mage", "WhiteWolf", "BlueWolf", "Boss" })\n                Directory.CreateDirectory(generated + "/Animations/" + kind);')
s=s.replace('DemoRigFactory.Material = spriteMaterial; DemoRigFactory.Solid = solid;','DemoRigFactory.Material = spriteMaterial; DemoRigFactory.Solid = solid;\n            DemoRigFactory.SpriteDirectory = generated + "/Sprites";')
a=s.index('            var playerAnimator =');b=s.index('            string bulletPrefabPath',a)
s=s[:a]+'''            string playerPrefabPath = Actor<PlayerController>("Player", DemoAnimatorFactory.Create(generated + "/Animations/Player", "Player"));
            string[] enemyPrefabPaths = new string[4];
            string[] kinds = { "Archer", "Mage", "WhiteWolf", "BlueWolf" };
            for (int i = 0; i < kinds.Length; i++)
                enemyPrefabPaths[i] = Actor<EnemyAI>(kinds[i], DemoAnimatorFactory.Create(generated + "/Animations/" + kinds[i], kinds[i]));
            string bossPrefabPath = Actor<BossAI>("Boss", DemoAnimatorFactory.Create(generated + "/Animations/Boss", "Boss"));
'''+s[b:]
a=s.index('            float totalLength =');b=s.index('            var ui = MakeUI();',a)
s=s[:a]+'''            var exit = Block(environment.transform, "PhaseExit", new Vector2(47, 1.2f), new Vector2(0.16f, 2.4f), Color.cyan, 0, false);
            var background = new GameObject("SerpentBack_Surface") { layer = groundLayer };
            background.transform.SetParent(environment.transform, false);
            var backgroundSprite = background.AddComponent<SpriteRenderer>();
            backgroundSprite.sharedMaterial = spriteMaterial; backgroundSprite.sortingOrder = -100;
            background.AddComponent<EdgeCollider2D>().sharedMaterial = physicsMaterial;
            var surface = background.AddComponent<SerpentSurface>();
            ConfigureProfiles(surface);
            surface.Configure(1, 0, config.phases[0].length);
            var director = environment.AddComponent<BackgroundDirector>();
            Ref(director, "surface", surface);
            player.transform.position = new Vector3(2, surface.HeightAt(2) + 0.06f, 0);
'''+s[b:]
s=s.replace('EditorSceneManager.SaveScene(scene, scenePath);','if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("No se pudo guardar " + scenePath);\n            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };')
a=s.index('                var weapon = actor.AddComponent<ProjectileWeapon>();');b=s.index('                if (kind == "Player")',a)
s=s[:a]+'''                var visual = DemoRigFactory.Create(actor, kind);
                Ref(driver, "visual", visual);
                if (kind == "Player" || wolf)
                {
                    var melee = actor.AddComponent<MeleeWeapon>();
                    Int(melee, "targetMask", 1 << (kind == "Player" ? enemyLayer : playerLayer));
                    Int(melee, "obstacleMask", 1 << groundLayer);
                    if (wolf)
                    {
                        Set(melee, "center", p => p.vector2Value = new Vector2(1.15f, 0.95f));
                        Set(melee, "size", p => p.vector2Value = new Vector2(1.1f, 1.05f));
                    }
                }
                else
                {
                    var weapon = actor.AddComponent<ProjectileWeapon>();
                    Ref(weapon, "muzzle", boss ? visual.Find("Rig/Neck/Head/Muzzle") : visual.Find("Rig/Pelvis/Torso/FrontArm/Lower/Hand/Weapon/Muzzle"));
                    Float(weapon, "projectileSpeed", 11);
                    Bool(weapon, "arrow", kind == "Archer");
                }
'''+s[b:]
s=s.replace('"J o clic izquierdo: disparar','"J o clic izquierdo: atacar') if False else s.replace('J o clic izquierdo: disparar','J o clic izquierdo: hacha cuerpo a cuerpo')
# Exact traced contours at 5% intervals, from the body rim; decorative dorsal spikes stay passable.
pos=s.index('        private static void ImportOriginals()')
s=s[:pos]+'''        private static void ConfigureProfiles(SerpentSurface surface)
        {
            float[][] top =
            {
                new[] { .59f,.62f,.67f,.70f,.74f,.77f,.80f,.81f,.82f,.83f,.82f,.81f,.80f,.79f,.78f,.78f,.76f,.75f,.74f,.75f,.77f },
                new[] { .57f,.60f,.62f,.63f,.65f,.67f,.69f,.71f,.72f,.73f,.74f,.74f,.74f,.73f,.72f,.71f,.70f,.68f,.67f,.67f,.68f },
                new[] { .56f,.59f,.61f,.63f,.66f,.69f,.71f,.73f,.75f,.77f,.78f,.79f,.80f,.81f,.82f,.81f,.80f,.78f,.77f,.77f,.77f },
                new[] { .60f,.62f,.65f,.67f,.70f,.72f,.74f,.75f,.76f,.77f,.78f,.79f,.79f,.78f,.77f,.75f,.73f,.70f,.67f,.65f,.64f }
            };
            int[] order = { 2, 3, 2, 3, 4, 1 };
            var serialized = new SerializedObject(surface); var profiles = serialized.FindProperty("phaseProfiles"); profiles.arraySize = 6;
            for (int i = 0; i < order.Length; i++)
            {
                var p = profiles.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("image").objectReferenceValue = LoadRequired<Sprite>(Root + "/Art/Fondos/Fondo_" + order[i].ToString("00") + ".png");
                var points = p.FindPropertyRelative("points"); points.arraySize = top[order[i] - 1].Length;
                for (int j = 0; j < points.arraySize; j++)
                    points.GetArrayElementAtIndex(j).vector2Value = new Vector2((float)j / (points.arraySize - 1), 1 - top[order[i] - 1][j]);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

'''+s[pos:]
p.write_text(s,encoding='utf-8')
# axe rate / vocabulary
p=Path('Assets/OneMoreTime/Scripts/Progression/UpgradeDefinition.cs');s=p.read_text(encoding='utf-8-sig').replace('// Shots per second. A larger value means faster shooting.','// Attacks per second; scales windup, impact window and recovery together.').replace('fireRate = 7f','fireRate = 1.6f').replace('0.25f, 25f','0.5f, 4f').replace('Munición temporal','Filo afilado').replace('Gatillo veloz','Brazos veloces').replace('disparos por segundo','ataques por segundo').replace('Arsenal pesado','Furia del hacha');p.write_text(s,encoding='utf-8')
p=Path('Assets/OneMoreTime/Scripts/UI/UIManager.cs');s=p.read_text(encoding='utf-8-sig').replace('Daño {stats.damage:0.#}','Daño/golpe {stats.damage:0.#}').replace('Cadencia {stats.fireRate:0.#}/s','Ataques {stats.fireRate:0.#}/s');p.write_text(s,encoding='utf-8')
p=Path('Assets/OneMoreTime/Editor/DemoValidation.cs');s=p.read_text(encoding='utf-8-sig').replace('Require(player.GetComponent<ProjectileWeapon>(), "muzzle");','Check(player.GetComponent<MeleeWeapon>() != null && player.GetComponent<ProjectileWeapon>() == null, "El jugador debe usar exclusivamente el hacha cuerpo a cuerpo.");\n            Check(player.GetComponent<ArticulatedRig>() != null, "Falta el rig articulado.");').replace('copy.fireRate == 25f','copy.fireRate == 4f');p.write_text(s,encoding='utf-8')
