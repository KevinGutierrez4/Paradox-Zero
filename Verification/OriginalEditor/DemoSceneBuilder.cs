#if UNITY_EDITOR
// Generator fix: open the destination scene first and resolve prefab assets by path.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OneMoreTime.Editor
{
    public static class DemoSceneBuilder
    {
        internal const string Root = "Assets/OneMoreTime";
        private static string generated;
        private static int groundLayer, playerLayer, enemyLayer, projectileLayer;
        private static Material spriteMaterial;
        private static Sprite solid;
        private static PhysicsMaterial2D physicsMaterial;
        private static Font font;

        [MenuItem("Tools/One More Time/1. Crear demo 2D")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de generar la demo.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Change scenes before creating or retaining prefab assets. A scene switch may
            // unload assets whose only remaining reference is a local managed variable.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            generated = AssetDatabase.GenerateUniqueAssetPath(Root + "/Generated");
            Directory.CreateDirectory(generated + "/Prefabs");
            Directory.CreateDirectory(generated + "/Animations/Player");
            Directory.CreateDirectory(generated + "/Animations/Enemy");
            Directory.CreateDirectory(generated + "/Scenes");
            AssetDatabase.Refresh();
            ImportOriginals();
            groundLayer = Layer("OMT_Ground"); playerLayer = Layer("OMT_Player");
            enemyLayer = Layer("OMT_Enemy"); projectileLayer = Layer("OMT_Projectile");
            Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
            Physics2D.IgnoreLayerCollision(projectileLayer, projectileLayer, true);
            foreach (int layer in new[] { groundLayer, playerLayer, enemyLayer })
                Physics2D.IgnoreLayerCollision(projectileLayer, layer, false);
            Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, false);
            Physics2D.IgnoreLayerCollision(enemyLayer, groundLayer, false);
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);

            solid = MakeSolid();
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("No se encontró un shader de sprites compatible.");
            spriteMaterial = new Material(shader) { name = "SpritesUnlit" };
            AssetDatabase.CreateAsset(spriteMaterial, generated + "/SpritesUnlit.mat");
            physicsMaterial = new PhysicsMaterial2D("SinFriccion") { friction = 0, bounciness = 0 };
            AssetDatabase.CreateAsset(physicsMaterial, generated + "/SinFriccion.physicsMaterial2D");
            DemoRigFactory.Material = spriteMaterial; DemoRigFactory.Solid = solid;
            var playerAnimator = DemoAnimatorFactory.Create(generated + "/Animations/Player", true);
            var enemyAnimator = DemoAnimatorFactory.Create(generated + "/Animations/Enemy", false);
            string playerPrefabPath = Actor<PlayerController>("Player", playerAnimator);
            string[] enemyPrefabPaths =
            {
                Actor<EnemyAI>("Archer", enemyAnimator), Actor<EnemyAI>("Mage", enemyAnimator),
                Actor<EnemyAI>("WhiteWolf", enemyAnimator), Actor<EnemyAI>("BlueWolf", enemyAnimator)
            };
            string bossPrefabPath = Actor<BossAI>("Boss", enemyAnimator);
            string bulletPrefabPath = MakeProjectile();
            var config = ScriptableObject.CreateInstance<RunConfig>();
            AssetDatabase.CreateAsset(config, generated + "/RunConfig.asset");
            AssetDatabase.SaveAssets();
            // Keep paths across asset creation/import. Resolve fresh native objects only
            // when assigning references or instantiating; never retain prefab components.
            config = LoadRequired<RunConfig>(generated + "/RunConfig.asset");
            spriteMaterial = LoadRequired<Material>(generated + "/SpritesUnlit.mat");
            solid = LoadRequired<Sprite>(generated + "/Solid.png");
            physicsMaterial = LoadRequired<PhysicsMaterial2D>(generated + "/SinFriccion.physicsMaterial2D");
            var systems = new GameObject("Systems");
            systems.AddComponent<CollisionRules2D>();
            var game = systems.AddComponent<GameManager>();
            var upgrades = systems.AddComponent<UpgradeManager>();
            var spawner = new GameObject("Enemies").AddComponent<EnemySpawner>();
            var projectiles = new GameObject("Projectiles").AddComponent<ProjectilePool>();
            Ref(projectiles, "projectilePrefab", LoadPrefabComponent<Projectile>(bulletPrefabPath));
            Int(projectiles, "collisionMask", (1 << groundLayer) | (1 << playerLayer) | (1 << enemyLayer));
            var enemyPrefabs = new EnemyAI[enemyPrefabPaths.Length];
            for (int i = 0; i < enemyPrefabs.Length; i++)
                enemyPrefabs[i] = LoadPrefabComponent<EnemyAI>(enemyPrefabPaths[i]);
            RefArray(spawner, "enemyPrefabs", enemyPrefabs);
            Ref(spawner, "bossPrefab", LoadPrefabComponent<BossAI>(bossPrefabPath));
            Int(spawner, "groundMask", 1 << groundLayer);
            var playerAsset = LoadRequired<GameObject>(playerPrefabPath);
            var playerObject = PrefabUtility.InstantiatePrefab(playerAsset, scene) as GameObject;
            if (playerObject == null) throw new InvalidOperationException("No se pudo instanciar el prefab del jugador: " + playerPrefabPath);
            var player = playerObject.GetComponent<PlayerController>();
            if (player == null) throw new InvalidOperationException("El prefab del jugador no contiene PlayerController: " + playerPrefabPath);
            player.name = "Player_Calvo"; player.transform.position = new Vector3(2, 0.04f, 0);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 4.8f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.07f, 0.09f, 0.15f);
            camera.transform.position = new Vector3(8, 3.2f, -10);
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<CameraFollow2D>();
            var environment = new GameObject("Environment");
            float totalLength = config.bossArenaLength + 6f * 6f;
            foreach (var phase in config.phases) totalLength += phase.length;
            Block(environment.transform, "Ground", new Vector2(totalLength / 2, -0.5f), new Vector2(totalLength + 20, 1), new Color(0.45f, 0.54f, 0.61f), groundLayer, true);
            var exit = Block(environment.transform, "PhaseExit", new Vector2(47, 1.2f), new Vector2(0.16f, 2.4f), Color.cyan, 0, false);
            var background = new GameObject("Background");
            background.transform.SetParent(environment.transform, false);
            var backgroundSprite = background.AddComponent<SpriteRenderer>();
            backgroundSprite.sharedMaterial = spriteMaterial; backgroundSprite.sortingOrder = -100;
            background.transform.position = new Vector3(8, 3.2f, 8);
            var parallax = background.AddComponent<ParallaxBackground>();
            Ref(parallax, "targetCamera", camera);
            var director = environment.AddComponent<BackgroundDirector>();
            Ref(director, "background", parallax);
            var backgroundChoices = new Sprite[6];
            int[] order = { 2, 3, 2, 3, 4, 1 };
            for (int i = 0; i < order.Length; i++)
                backgroundChoices[i] = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Fondos/Fondo_" + order[i].ToString("00") + ".png");
            RefArray(director, "phaseBackgrounds", backgroundChoices);
            backgroundSprite.sprite = backgroundChoices[0];
            // Editor preview. The runtime parallax component refits to the actual aspect ratio.
            if (backgroundSprite.sprite != null) background.transform.localScale = Vector3.one * (9.6f / backgroundSprite.sprite.bounds.size.y);

            var ui = MakeUI();
            Ref(game, "config", config); Ref(game, "player", player); Ref(game, "spawner", spawner);
            Ref(game, "upgrades", upgrades); Ref(game, "projectiles", projectiles); Ref(game, "ui", ui);
            Ref(game, "cameraFollow", follow); Ref(game, "backgrounds", director); Ref(game, "phaseExitMarker", exit.transform);
            AssetDatabase.SaveAssets();
            string scenePath = generated + "/Scenes/OneMoreTime_Demo.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = player.gameObject;
            Debug.Log("Demo creada en " + scenePath + ". Activa Input Manager (Old) o Both, reinicia si Unity lo solicita y pulsa Play.");
            DemoValidation.ValidateRules();
        }

        private static void ImportOriginals()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
        }

        private static Sprite MakeSolid()
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels); texture.Apply();
            string path = generated + "/Solid.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 8; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static string Actor<T>(string kind, RuntimeAnimatorController animator) where T : Component
        {
            var actor = new GameObject(kind) { layer = kind == "Player" ? playerLayer : enemyLayer };
            try
            {
                var body = actor.AddComponent<Rigidbody2D>();
                body.gravityScale = 3; body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                var box = actor.AddComponent<BoxCollider2D>();
                bool wolf = kind.Contains("Wolf"); bool boss = kind == "Boss";
                box.size = boss ? new Vector2(2.8f, 3.3f) : wolf ? new Vector2(2.2f, 1.35f) : new Vector2(0.7f, 1.95f);
                box.offset = new Vector2(0, box.size.y / 2);
                box.sharedMaterial = physicsMaterial;
                var health = actor.AddComponent<Health>();
                Int(health, "team", kind == "Player" ? 0 : 1);
                Float(health, "invulnerabilitySeconds", kind == "Player" ? 0.45f : 0);
                actor.AddComponent<Animator>().runtimeAnimatorController = animator;
                var driver = actor.AddComponent<AnimationDriver>();
                var weapon = actor.AddComponent<ProjectileWeapon>();
                var visual = DemoRigFactory.Create(actor, kind);
                Ref(driver, "visual", visual); Ref(weapon, "muzzle", visual.Find("Rig/Muzzle"));
                Float(weapon, "projectileSpeed", kind == "Player" ? 25 : 11);
                if (kind == "Player")
                {
                    actor.AddComponent<PlayerStats>();
                    var controller = actor.AddComponent<PlayerController>();
                    var groundCheck = DemoRigFactory.Child(actor.transform, "GroundCheck", new Vector2(0, 0.04f));
                    Ref(controller, "groundCheck", groundCheck); Int(controller, "groundMask", 1 << groundLayer);
                }
                else if (boss)
                {
                    var ai = actor.AddComponent<BossAI>();
                    Float(ai, "baseHealth", 850); Float(ai, "baseDamage", 10);
                }
                else
                {
                    var ai = actor.AddComponent<EnemyAI>();
                    Bool(ai, "ranged", !wolf); Float(ai, "attackRange", wolf ? 1.5f : 7);
                    Float(ai, "baseSpeed", wolf ? 3.8f : kind == "Mage" ? 1.6f : 2.2f);
                    Float(ai, "baseHealth", wolf ? 26 : kind == "Mage" ? 42 : 32);
                    Float(ai, "baseDamage", wolf ? 11 : 8); Float(ai, "attackInterval", wolf ? 0.8f : 1.2f);
                }
                if (actor.GetComponent<T>() == null)
                    throw new InvalidOperationException("Falta " + typeof(T).Name + " al crear " + kind);
                string path = generated + "/Prefabs/" + kind + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(actor, path, out bool saved);
                if (!saved) throw new InvalidOperationException("No se pudo guardar el prefab: " + path);
                return path;
            }
            finally
            {
                if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        private static string MakeProjectile()
        {
            var obj = new GameObject("Projectile") { layer = projectileLayer };
            try
            {
                var body = obj.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
                var collider = obj.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.radius = 0.5f;
                var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = solid;
                renderer.sharedMaterial = spriteMaterial; renderer.sortingOrder = 20;
                obj.transform.localScale = new Vector3(0.22f, 0.14f, 1);
                obj.AddComponent<Projectile>();
                string path = generated + "/Prefabs/Projectile.prefab";
                PrefabUtility.SaveAsPrefabAsset(obj, path, out bool saved);
                if (!saved) throw new InvalidOperationException("No se pudo guardar el proyectil: " + path);
                return path;
            }
            finally
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("No se pudo cargar el recurso " + typeof(T).Name + ": " + path);
            return asset;
        }

        private static T LoadPrefabComponent<T>(string path) where T : Component
        {
            T component = LoadRequired<GameObject>(path).GetComponent<T>();
            if (component == null) throw new InvalidOperationException("Falta el componente " + typeof(T).Name + " en " + path);
            return component;
        }

        private static GameObject Block(Transform parent, string name, Vector2 position, Vector2 size, Color color, int layer, bool collider)
        {
            var obj = new GameObject(name) { layer = layer };
            obj.transform.SetParent(parent, false); obj.transform.position = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = solid;
            renderer.sharedMaterial = spriteMaterial; renderer.color = color; renderer.sortingOrder = -1;
            if (collider) obj.AddComponent<BoxCollider2D>().sharedMaterial = physicsMaterial;
            return obj;
        }

        private static int Layer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = manager.FindProperty("layers");
            for (int i = 8; i < 32; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = name;
                    manager.ApplyModifiedProperties(); return i;
                }
            throw new InvalidOperationException("No quedan layers libres para " + name);
        }

        internal static void Ref(UnityEngine.Object obj, string field, UnityEngine.Object value)
        { Set(obj, field, property => property.objectReferenceValue = value); }
        internal static void Float(UnityEngine.Object obj, string field, float value)
        { Set(obj, field, property => property.floatValue = value); }
        internal static void Int(UnityEngine.Object obj, string field, int value)
        { Set(obj, field, property => property.intValue = value); }
        internal static void Bool(UnityEngine.Object obj, string field, bool value)
        { Set(obj, field, property => property.boolValue = value); }
        internal static void RefArray(UnityEngine.Object obj, string field, UnityEngine.Object[] values)
        {
            Set(obj, field, property =>
            {
                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            });
        }
        private static void Set(UnityEngine.Object obj, string field, Action<SerializedProperty> change)
        {
            var serialized = new SerializedObject(obj); var property = serialized.FindProperty(field);
            if (property == null) throw new MissingFieldException(obj.GetType().Name, field);
            change(property); serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UIManager MakeUI()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
            var ui = canvasObject.AddComponent<UIManager>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            GameObject menu = Panel(canvasObject.transform, "MainMenu");
            Label(menu.transform, "Title", "ONE MORE TIME", new Vector2(0, 210), new Vector2(1100, 90), 60);
            Label(menu.transform, "Subtitle", "Solo una vez más · 5 fases + boss final", new Vector2(0, 145), new Vector2(1000, 50), 26);
            Ref(ui, "menuRecord", Label(menu.transform, "Best", "Récord: 0", new Vector2(0, 95), new Vector2(800, 40), 24));
            Ref(ui, "playButton", Button(menu.transform, "Play", "JUGAR", new Vector2(0, 15), new Vector2(330, 65)));
            Ref(ui, "optionsButton", Button(menu.transform, "Options", "OPCIONES", new Vector2(0, -70), new Vector2(330, 65)));
            Ref(ui, "quitButton", Button(menu.transform, "Quit", "SALIR", new Vector2(0, -155), new Vector2(330, 65)));
            Label(menu.transform, "Controls", "A/D o flechas: correr · Espacio: saltar · S/↓: agacharse\nJ o clic izquierdo: disparar · Esc: pausa", new Vector2(0, -265), new Vector2(1180, 90), 23);

            GameObject options = Panel(canvasObject.transform, "Options");
            Label(options.transform, "Title", "OPCIONES", new Vector2(0, 180), new Vector2(900, 80), 44);
            Label(options.transform, "VolumeLabel", "Volumen general", new Vector2(0, 70), new Vector2(800, 50), 26);
            Ref(ui, "volumeSlider", Slider(options.transform, "Volume", new Vector2(0, 0), new Vector2(480, 28), true));
            Ref(ui, "optionsBackButton", Button(options.transform, "Back", "VOLVER", new Vector2(0, -150), new Vector2(330, 65)));

            var hud = new GameObject("HUD", typeof(RectTransform)); hud.transform.SetParent(canvasObject.transform, false); Stretch(hud.GetComponent<RectTransform>());
            var top = Panel(hud.transform, "TopBar", new Color(0.025f, 0.04f, 0.09f, 0.88f));
            var topRect = top.GetComponent<RectTransform>(); topRect.anchorMin = new Vector2(0, 1); topRect.anchorMax = Vector2.one;
            topRect.pivot = new Vector2(0.5f, 1); topRect.sizeDelta = new Vector2(0, 138); topRect.anchoredPosition = Vector2.zero;
            Ref(ui, "healthBar", Slider(top.transform, "HealthBar", new Vector2(-420, 30), new Vector2(300, 20), false));
            Ref(ui, "healthText", Label(top.transform, "HealthText", "VIDA", new Vector2(-420, 58), new Vector2(330, 36), 24));
            Ref(ui, "phaseText", Label(top.transform, "Phase", "FASE", new Vector2(0, 45), new Vector2(350, 45), 28));
            Ref(ui, "scoreText", Label(top.transform, "Score", "PUNTOS", new Vector2(425, 45), new Vector2(380, 45), 23));
            Ref(ui, "statsText", Label(top.transform, "Stats", "Stats", new Vector2(0, 1), new Vector2(1150, 35), 23));
            Ref(ui, "progressText", Label(top.transform, "Progress", "Avanza hasta la fisura", new Vector2(0, -38), new Vector2(1200, 35), 20));
            Ref(ui, "bossBar", Slider(hud.transform, "BossBar", new Vector2(0, -275), new Vector2(750, 22), false));
            Ref(ui, "bossText", Label(hud.transform, "BossText", "SERPIENTE", new Vector2(0, -235), new Vector2(1000, 40), 24));

            GameObject pause = Panel(canvasObject.transform, "Pause");
            Label(pause.transform, "Title", "PAUSA", new Vector2(0, 130), new Vector2(900, 80), 48);
            Ref(ui, "resumeButton", Button(pause.transform, "Resume", "CONTINUAR", Vector2.zero, new Vector2(360, 65)));
            Ref(ui, "pauseMenuButton", Button(pause.transform, "Menu", "MENÚ PRINCIPAL", new Vector2(0, -90), new Vector2(360, 65)));

            GameObject upgrade = Panel(canvasObject.transform, "UpgradeSelection");
            Label(upgrade.transform, "Title", "FASE COMPLETADA · ELIGE UNA MEJORA", new Vector2(0, 240), new Vector2(1200, 75), 35);
            Label(upgrade.transform, "Hint", "Las mejoras se acumulan durante este intento. La partida está en pausa.", new Vector2(0, 175), new Vector2(1160, 55), 22);
            var cards = new UpgradeCardUI[3];
            for (int i = 0; i < cards.Length; i++)
            {
                var button = Button(upgrade.transform, "Card_" + (i + 1), "", new Vector2((i - 1) * 390, -20), new Vector2(355, 280));
                button.GetComponentInChildren<Text>().gameObject.SetActive(false);
                cards[i] = button.gameObject.AddComponent<UpgradeCardUI>();
                Ref(cards[i], "title", Label(button.transform, "Title", "Mejora", new Vector2(0, 65), new Vector2(320, 90), 29));
                Ref(cards[i], "description", Label(button.transform, "Description", "+20%", new Vector2(0, -40), new Vector2(315, 130), 24));
            }
            RefArray(ui, "cards", cards);

            GameObject result = Panel(canvasObject.transform, "RunResult");
            Ref(ui, "resultTitle", Label(result.transform, "Title", "SOLO UNA VEZ MÁS", new Vector2(0, 205), new Vector2(1200, 100), 38));
            Ref(ui, "resultDetails", Label(result.transform, "Details", "Resultado", new Vector2(0, 45), new Vector2(1100, 220), 27));
            Ref(ui, "retryButton", Button(result.transform, "Retry", "OTRA PARTIDA", new Vector2(-210, -155), new Vector2(380, 70)));
            Ref(ui, "resultMenuButton", Button(result.transform, "Menu", "MENÚ PRINCIPAL", new Vector2(210, -155), new Vector2(380, 70)));

            Ref(ui, "mainMenu", menu); Ref(ui, "optionsPanel", options); Ref(ui, "hud", hud);
            Ref(ui, "pausePanel", pause); Ref(ui, "upgradePanel", upgrade); Ref(ui, "resultPanel", result);
            foreach (var panel in new[] { options, hud, pause, upgrade, result }) panel.SetActive(false);
            return ui;
        }

        private static GameObject Panel(Transform parent, string name, Color? color = null)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
            Stretch(obj.GetComponent<RectTransform>()); obj.GetComponent<Image>().color = color ?? new Color(0.025f, 0.035f, 0.07f, 0.94f);
            return obj;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Position(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            Position(obj.GetComponent<RectTransform>(), position, size);
            var label = obj.GetComponent<Text>(); label.font = font; label.text = text; label.fontSize = fontSize;
            label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
        private static Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
            Position(obj.GetComponent<RectTransform>(), position, size);
            var image = obj.GetComponent<Image>(); image.color = new Color(0.12f, 0.3f, 0.39f, 1);
            var button = obj.GetComponent<Button>(); button.targetGraphic = image;
            Label(obj.transform, "Label", text, Vector2.zero, size - new Vector2(16, 10), 27);
            return button;
        }
        private static Slider Slider(Transform parent, string name, Vector2 position, Vector2 size, bool interactable)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider)); obj.transform.SetParent(parent, false);
            Position(obj.GetComponent<RectTransform>(), position, size);
            obj.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f); obj.GetComponent<Image>().raycastTarget = interactable;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(obj.transform, false);
            Stretch(fill.GetComponent<RectTransform>()); fill.GetComponent<Image>().color = interactable ? Color.cyan : new Color(0.25f, 0.85f, 0.6f);
            fill.GetComponent<Image>().raycastTarget = false;
            var slider = obj.GetComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.fillRect = fill.GetComponent<RectTransform>(); slider.interactable = interactable;
            if (!interactable) slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }
    }
}
#endif
