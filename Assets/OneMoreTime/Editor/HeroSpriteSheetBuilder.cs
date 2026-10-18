#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace OneMoreTime.Editor
{
    public static class HeroSpriteSheetBuilder
    {
        private const int Cell = 384, Frames = 12;
        private static readonly string[] Rows = { "Idle", "Walk", "Crouch", "Attack", "Jump", "Fall", "Block", "Die" };
        private static readonly float[] Durations = { 1.2f, 0.80f, 1.2f, 0.625f, 0.4f, 0.6f, 1.2f, 0.9f };
        private const string Output = "Assets/OneMoreTime/Art/HeroShieldV4";

        [MenuItem("Tools/One More Time/7. Rehacer animaciones y exportar sprite sheet")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Sal de Play para exportar.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ShieldHeroFactory.Prepare();
            string scene = EditorBuildSettings.scenes[0].path;
            string root = scene.Substring(0, scene.LastIndexOf("/Scenes/", StringComparison.Ordinal));
            string canonical = "Assets/OneMoreTime/Generated_Hacha_Final";
            if (root != canonical && File.Exists(canonical + "/Prefabs/Player.prefab")) RebuildHero(canonical);
            RebuildHero(root);
            Directory.CreateDirectory(Output); Directory.CreateDirectory("Verification/ShieldHeroV4");
            Export(AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Prefabs/Player.prefab"));
            ImportAndAnimate();
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(scene);
            DemoValidation.ValidateAll();
            DemoVisualValidation.RenderPoses();
            File.WriteAllText("Verification/shield-hero-pass.txt", "PASS: 96 transparent frames, 384x384 grid, 96 PPU, fixed feet pivot (0.5,0.1875), eight clips, attached axe/shield sockets and no clipped frames.\nActive scene: " + scene + "\nUpdated canonical and active player prefabs.");
            Debug.Log("OMT_HERO_SHEET_PASS");
        }

        private static void RebuildHero(string root)
        {
            DemoRigFactory.SpriteDirectory = root + "/Sprites";
            DemoRigFactory.Material = AssetDatabase.LoadAssetAtPath<Material>(root + "/SpritesUnlit.mat");
            string prefabPath = root + "/Prefabs/Player.prefab";
            var actor = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (actor.GetComponent<PlayerGuard>() == null) actor.AddComponent<PlayerGuard>();
                var old = actor.transform.Find("Visual"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var visual = DemoRigFactory.Create(actor, "Player");
                DemoSceneBuilder.Ref(actor.GetComponent<AnimationDriver>(), "visual", visual);
                actor.GetComponent<Animator>().enabled = false;
                PrefabUtility.SaveAsPrefabAsset(actor, prefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException("No se pudo guardar el protagonista.");
            }
            finally { PrefabUtility.UnloadPrefabContents(actor); }
            DemoAnimatorFactory.RefreshHero(root);
            var savedHero = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (savedHero.GetComponent<Animator>().enabled || savedHero.GetComponent<PlayerGuard>() == null)
                throw new InvalidOperationException("Guard/pose ownership did not persist in player prefab.");
            foreach (string name in Rows)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "/Animations/Player/" + name + ".anim");
                if (clip == null) throw new InvalidOperationException("Missing hero clip: " + name);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (savedHero.transform.Find(binding.path) == null || binding.propertyName.Contains("Scale"))
                        throw new InvalidOperationException("Invalid hero animation binding: " + binding.path + ":" + binding.propertyName);
            }
        }

        private static void Export(GameObject prefab)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("HeroSheetCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = preview;
            camera.orthographic = true; camera.orthographicSize = 2;
            camera.transform.position = new Vector3(0, 1.25f, -10); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear; camera.allowHDR = false; camera.allowMSAA = false;
            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, filterMode = FilterMode.Point };
            camera.targetTexture = target;
            var hero = UnityEngine.Object.Instantiate(prefab); hero.transform.position = Vector3.zero;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hero, preview);
            hero.GetComponent<Animator>().enabled = false;
            var pose = hero.GetComponent<ArticulatedRig>(); pose.Bind();
            NeutralRigPreview.Capture(hero, camera);
            var sheet = new Texture2D(Cell * Frames, Cell * Rows.Length, TextureFormat.RGBA32, false);
            var frame = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            var checks = new List<string>();
            try
            {
                for (int row = 0; row < Rows.Length; row++) for (int col = 0; col < Frames; col++)
                {
                    float t = (float)col / Frames, phase = t * Mathf.PI * 2;
                    pose.EvaluatePose(phase, row == 1 ? 7.5f : 0, row < 4 || row >= 6, row == 2 ? 1 : 0,
                        row == 4 ? Mathf.Lerp(12, 1, t) : row == 5 ? Mathf.Lerp(-2, -14, t) : 0,
                        row == 3 ? (float)col / (Frames - 1) : -1, row == 7 ? 0.9f * col / (Frames - 1) : -1, 0, 0, row == 6 ? 1 : 0);
                    var skeleton = hero.transform.Find("Visual/Rig");
                    var axeHand = skeleton.Find("Pelvis/Torso/FrontArm/Lower/Hand");
                    var shieldHand = skeleton.Find("Pelvis/Torso/BackArm/Lower/Hand");
                    float frontError = Vector3.Distance(axeHand.TransformPoint(new Vector3(0, -0.11f)), axeHand.Find("AxeSocket").position);
                    float rearError = Vector3.Distance(shieldHand.TransformPoint(new Vector3(0, -0.11f)), shieldHand.Find("ShieldSocket").position);
                    if (frontError > 0.055f || rearError > 0.055f)
                        throw new InvalidOperationException($"Grip unreachable {Rows[row]} {col}: front {frontError}, rear {rearError}");
                    camera.Render(); camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    frame.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0); frame.Apply(); RenderTexture.active = previous;
                    var pixels = frame.GetPixels32();
                    for (int edge = 0; edge < Cell; edge++)
                        if (pixels[edge].a != 0 || pixels[(Cell - 1) * Cell + edge].a != 0 || pixels[edge * Cell].a != 0 || pixels[edge * Cell + Cell - 1].a != 0)
                            throw new InvalidOperationException("Nontransparent or clipped frame: " + Rows[row] + " " + col);
                    if (!pixels.Any(c => c.a > 0)) throw new InvalidOperationException("Empty frame.");
                    sheet.SetPixels32(col * Cell, (Rows.Length - 1 - row) * Cell, Cell, Cell, pixels);
                    File.WriteAllBytes($"Verification/ShieldHeroV4/{Rows[row]}_{col:00}.png", frame.EncodeToPNG());
                    checks.Add($"PASS {Rows[row]} {col:00}: grip errors {frontError:0.0000}/{rearError:0.0000}; transparent borders.");
                }
                sheet.Apply(); File.WriteAllBytes(Output + "/Hero_Shield_96.png", sheet.EncodeToPNG());
                File.WriteAllLines("Verification/shield-hero-checks.txt", checks);
            }
            finally
            {
                camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(frame); UnityEngine.Object.DestroyImmediate(sheet);
                UnityEngine.Object.DestroyImmediate(hero); EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void ImportAndAnimate()
        {
            string path = Output + "/Hero_Shield_96.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 96; importer.maxTextureSize = 8192; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(s => s.name, s => s.spriteID);
            var rects = new List<SpriteRect>();
            for (int row = 0; row < Rows.Length; row++) for (int col = 0; col < Frames; col++)
            {
                string name = Rows[row] + "_" + col.ToString("00");
                rects.Add(new SpriteRect { name = name, rect = new Rect(col * Cell, (Rows.Length - 1 - row) * Cell, Cell, Cell),
                    pivot = new Vector2(0.5f, 0.1875f), alignment = SpriteAlignment.Custom,
                    spriteID = previous.TryGetValue(name, out var id) ? id : GUID.Generate() });
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            if (sprites.Count != Frames * Rows.Length) throw new InvalidOperationException("Sprite slicing failed.");
            var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importedTexture.width != Cell * Frames || importedTexture.height != Cell * Rows.Length || sprites.Values.Any(s => s.rect.width != Cell || s.rect.height != Cell))
                throw new InvalidOperationException("Unity resized the grid during import.");
            string directory = Output + "/Animations"; Directory.CreateDirectory(directory); AssetDatabase.Refresh();
            string controllerPath = directory + "/HeroSheet.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (var state in machine.states) machine.RemoveState(state.state);
            var states = new Dictionary<string, AnimatorState>();
            for (int row = 0; row < Rows.Length; row++)
            {
                var clip = new AnimationClip { name = Rows[row], frameRate = Frames / Durations[row] };
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = row < 3 || row == 6;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                var keys = new ObjectReferenceKeyframe[Frames + 1];
                for (int i = 0; i <= Frames; i++) keys[i] = new ObjectReferenceKeyframe { time = i * Durations[row] / Frames, value = sprites[Rows[row] + "_" + (i == Frames ? (row < 3 || row == 6 ? 0 : Frames - 1) : i).ToString("00")] };
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Visual", typeof(SpriteRenderer), "m_Sprite"), keys);
                string clipPath = directory + "/" + Rows[row] + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (existing == null) AssetDatabase.CreateAsset(clip, clipPath);
                else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
                var state = machine.AddState(Rows[row]); state.motion = clip; state.writeDefaultValues = false; states[Rows[row]] = state;
            }
            machine.defaultState = states["Idle"];
            SetupPreviewTransitions(controller, states);
            var preview = new GameObject("HeroSheetPreview", typeof(Animator));
            preview.GetComponent<Animator>().runtimeAnimatorController = controller;
            var visual = new GameObject("Visual", typeof(SpriteRenderer)); visual.transform.SetParent(preview.transform, false);
            var draw = visual.GetComponent<SpriteRenderer>(); draw.sprite = sprites["Idle_00"]; draw.sharedMaterial = DemoRigFactory.Material;
            PrefabUtility.SaveAsPrefabAsset(preview, Output + "/HeroSheetPreview.prefab"); UnityEngine.Object.DestroyImmediate(preview);
            File.WriteAllText(Output + "/README.md", "# Guerrero con hacha y escudo: 96 frames\n\nPNG RGBA 4608x3072. Cuadrícula de 12 columnas y 8 filas, celdas 384x384 sin margen. Filas: Idle, Walk, Crouch, Attack, Jump, Fall, Block, Die. Fondo transparente, mirada a la derecha. Pivote (0.5,0.1875), 96 PPU, Point, sin mipmaps ni compresión. Incluye 96 sprites, ocho clips y HeroSheetPreview.prefab.\n\nDuraciones: Idle/Crouch/Block 1.2s, Walk 0.8s, Attack 0.625s, Jump 0.4s, Fall 0.6s, Die 0.9s. Idle/Walk/Crouch/Block forman bucles; los demás mantienen su último frame. Die es terminal. El controlador de previsualización usa Speed, Grounded, Crouch, VerticalSpeed, Attack, Blocking y Dead. Voltea Visual.localScale.x para invertir la dirección.\n\nEn la escena jugable, ArticulatedRig controla las articulaciones y Animator queda desactivado para evitar escrituras simultáneas. Los clips de transforms corregidos quedan disponibles como referencia. El movimiento y los colliders continúan en la raíz. Los estados de aire siguen la velocidad vertical real. El hachazo se sincroniza con MeleeWeapon: carga 0–30%, golpe 30–50%, seguimiento 50–66% y recuperación. Una sola mano sostiene el hacha; la otra conserva el escudo. Cada objeto es hijo de su muñeca. Las escalas de los segmentos son uniformes y constantes.\n\nControles: A/D o flechas para moverse, espacio para saltar, S/abajo para agacharse, J/clic izquierdo para atacar. Mantén K/clic derecho para bloquear: reduce 65% el daño enemigo, permite caminar al 45% de velocidad e impide atacar. Soltar, pausar o morir cancela el bloqueo. El daño ambiental no se bloquea. Se conservan empuje al golpear, curación por bajas, mejoras y enemigos.\n\nLas piezas proceden de WarriorParts.png sin modificar el original. Los pivotes se midieron sobre el contenido visible. Se usan muslo, pantorrilla y pie separados; no se duplica una bota completa como otro segmento. Las vistas Neutral.png y NeutralJoints.png y los frames de cada acción se guardan en Verification/ShieldHeroV4.\n");
        }

        private static void SetupPreviewTransitions(AnimatorController controller, Dictionary<string, AnimatorState> states)
        {
            if (controller.parameters.Length == 0)
            {
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
                controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("Blocking", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
                var ground = controller.parameters.First(p => p.name == "Grounded"); ground.defaultBool = true;
                var parameters = controller.parameters; for (int i = 0; i < parameters.Length; i++) if (parameters[i].name == "Grounded") parameters[i] = ground;
                controller.parameters = parameters;
            }
            foreach (var from in states) foreach (var to in states)
            {
                if (from.Key == to.Key || to.Key == "Attack" || to.Key == "Die" || from.Key == "Die") continue;
                var tr = from.Value.AddTransition(to.Value); tr.duration = 0; tr.hasExitTime = from.Key == "Attack"; tr.exitTime = 1;
                tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                tr.AddCondition(to.Key == "Block" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Blocking");
                if (to.Key == "Block") continue;
                if (to.Key == "Jump" || to.Key == "Fall")
                { tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); tr.AddCondition(to.Key == "Jump" ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0, "VerticalSpeed"); }
                else
                {
                    tr.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); tr.AddCondition(to.Key == "Crouch" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Crouch");
                    if (to.Key != "Crouch") tr.AddCondition(to.Key == "Walk" ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0.1f, "Speed");
                }
            }
            var attack = controller.layers[0].stateMachine.AddAnyStateTransition(states["Attack"]);
            attack.hasExitTime = false; attack.duration = 0; attack.canTransitionToSelf = false; attack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            attack.AddCondition(AnimatorConditionMode.IfNot, 0, "Blocking"); attack.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var death = controller.layers[0].stateMachine.AddAnyStateTransition(states["Die"]);
            death.hasExitTime = false; death.duration = 0; death.canTransitionToSelf = false; death.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            EditorUtility.SetDirty(controller);
        }
    }
}
#endif
