#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreTime.Editor
{
    [InitializeOnLoad]
    public static class DemoPlayValidation
    {
        static DemoPlayValidation() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Tools/One More Time/5. Probar combate y recorrido automatico")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Sal de Play antes de probar.");
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
            SessionState.SetBool("OMT.Acceptance", true);
            EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool("OMT.Acceptance", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("AcceptanceRunner").AddComponent<DemoAcceptanceRunner>();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool("OMT.Acceptance", false);
                if (Application.isBatchMode) EditorApplication.Exit(File.Exists("Verification/play-pass.txt") ? 0 : 1);
            }
        }
    }

    public sealed class DemoAcceptanceRunner : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>();
        private GameManager game;
        private PlayerController player;
        private Rigidbody2D body;
        private MeleeWeapon melee;
        private int errors;
        private void OnEnable() { Application.logMessageReceived += Log; }
        private void OnDisable() { Application.logMessageReceived -= Log; }
        private void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private IEnumerator Start()
        {
            var test = Tests();
            while (true)
            {
                object next;
                try { if (!test.MoveNext()) break; next = test.Current; }
                catch (Exception e) { Debug.LogException(e); checks.Add("FAIL: " + e); Finish(false); yield break; }
                yield return next;
            }
            Finish(errors == 0);
        }
        private void Finish(bool passed)
        {
            File.WriteAllLines("Verification/play-results.txt", checks);
            if (passed) File.WriteAllText("Verification/play-pass.txt", "PASS Unity " + Application.unityVersion + " Play Mode; " + checks.Count + " checks; no runtime errors.");
            Debug.Log(passed ? "OMT_PLAY_PASS" : "OMT_PLAY_FAIL");
            EditorApplication.isPlaying = false;
        }
        private void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks.Add("PASS: " + message); Debug.Log("OMT_CHECK " + message); }
        private static void Set(object obj, string name, object value) { Field(obj.GetType(), name).SetValue(obj, value); }
        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (field != null) return field; }
            throw new MissingFieldException(name);
        }
        private static void Call(object obj, string name, params object[] args)
        { obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args); }
        private static T Ref<T>(UnityEngine.Object obj, string name) where T : UnityEngine.Object
        { return new SerializedObject(obj).FindProperty(name).objectReferenceValue as T; }
        private void Place(float x)
        { body.position = new Vector2(x, game.GroundHeight(x) + 0.05f); body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
        private Health Dummy(string name, Vector2 offset, Team team = Team.Enemy)
        {
            var obj = new GameObject(name) { layer = LayerMask.NameToLayer(team == Team.Player ? "OMT_Player" : "OMT_Enemy") };
            obj.transform.position = (Vector2)player.transform.position + offset;
            obj.AddComponent<BoxCollider2D>().size = new Vector2(0.3f, 0.6f);
            var hp = obj.AddComponent<Health>(); Set(hp, "invulnerabilitySeconds", 0f); hp.ResetHealth(100, team); return hp;
        }
        private void CaptureFrame(string name)
        {
            var camera = Camera.main; var previousTarget = camera.targetTexture;
            var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
            camera.Render(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes("Verification/" + name + ".png", image.EncodeToPNG());
            Destroy(image); RenderTexture.active = previous; camera.targetTexture = previousTarget;
            target.Release(); Destroy(target);
        }
        private void CaptureUIFrame(string name, UIManager ui)
        {
            var canvas = ui.GetComponent<Canvas>();
            var mode = canvas.renderMode; var camera = canvas.worldCamera; float distance = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); CaptureFrame(name);
            canvas.renderMode = mode; canvas.worldCamera = camera; canvas.planeDistance = distance;
        }
        private IEnumerator Tests()
        {
            if (File.Exists("Verification/play-pass.txt")) File.Delete("Verification/play-pass.txt");
            yield return null; yield return null;
            game = FindFirstObjectByType<GameManager>(); player = game.Player; body = player.GetComponent<Rigidbody2D>(); melee = player.GetComponent<MeleeWeapon>();
            Set(game, "persistRecords", false);
            TutorialController.Suppressed = true; GameManager.SuppressFocusPause = true;
            int storedRecord = PlayerPrefs.GetInt("OMT.BestScore.v1", 0);
            var ui = FindFirstObjectByType<UIManager>();
            Check(game.State == RunState.MainMenu, "Scene starts at main menu");
            Ref<Button>(ui, "optionsButton").onClick.Invoke(); Check(Ref<GameObject>(ui, "optionsPanel").activeSelf, "Options button works");
            Ref<Button>(ui, "optionsBackButton").onClick.Invoke();
            Ref<Button>(ui, "playButton").onClick.Invoke();
            Check(game.IsPlaying && game.Phase == 1, "Play button starts phase one");
            Check(player.GetComponent<ProjectileWeapon>() == null, "Player has no projectile weapon");
            {
                // P6: interactive tutorial (forced steps here; real input completes them in play).
                var tut = game.Tutorial; tut.Persist = false; tut.Begin();
                Check(tut.Active && tut.Current == TutorialController.Step.Move, "P6: tutorial starts with the movement step (A/D)");
                var steps = new List<TutorialController.Step>();
                for (int i = 0; i < 5; i++) { steps.Add(tut.Current); tut.ForceAdvance(); }
                Check(!tut.Active && string.Join(",", steps) == "Move,Jump,Attack,Defend,Dodge", "P6: tutorial steps move → jump → attack → defend → dodge, then it ends");
                tut.Begin(); tut.Skip(); Check(!tut.Active, "P6: tutorial can be skipped");
                tut.OnPhaseBegun(2); Check(!tut.Active, "P6: tutorial never starts outside phase one");
                Check(PlayerController.DodgeKey == KeyCode.L, "P6: dodge is mapped to L (and Left Shift)");
            }
            player.enabled = false; game.Spawner.enabled = false;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            string directionScene = EditorBuildSettings.scenes[0].path;
            string directionRoot = directionScene.Substring(0, directionScene.LastIndexOf("/Scenes/", StringComparison.Ordinal));
            foreach (string kind in new[] { "Player", "Archer", "Mage", "WhiteWolf", "BlueWolf", "Boss" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(directionRoot + "/Prefabs/" + kind + ".prefab");
                var instance = Instantiate(prefab, new Vector3(1000, 0, 0), Quaternion.identity);
                instance.GetComponent<Rigidbody2D>().simulated = false;
                var input = instance.GetComponent<PlayerController>(); if (input != null) input.enabled = false;
                var face = instance.GetComponent<AnimationDriver>();
                var head = instance.transform.Find(kind == "Player" || kind == "Archer" || kind == "Mage" ? "Visual/Rig/Pelvis/Torso/Head" : "Visual/Rig/Neck/Head");
                var nose = head.Find("FacingPoint");
                foreach (int parentSign in new[] { 1, -1 })
                {
                    instance.transform.localScale = new Vector3(parentSign, 1, 1);
                    foreach (int sign in new[] { 1, -1 })
                    {
                        face.SetMotion(sign * 3, true, false, 0);
                        yield return null; yield return null;
                        Check(face.Facing == sign && (nose.position.x - head.position.x) * sign > 0.01f,
                            kind + " drawing faces movement " + sign + " with parent scale " + parentSign + " after Animator evaluation");
                        if (kind == "Player")
                        {
                            var torso = instance.transform.Find("Visual/Rig/Pelvis/Torso");
                            var axeHand = torso.Find("FrontArm/Lower/Hand");
                            var shieldHand = torso.Find("BackArm/Lower/Hand");
                            var axe = axeHand.Find("AxeSocket");
                            var shield = shieldHand.Find("ShieldSocket");
                            float frontGripError = axe == null ? 100 : Vector3.Distance(axeHand.TransformPoint(0, -0.11f, 0), axe.position);
                            float rearGripError = shield == null ? 100 : Vector3.Distance(shieldHand.TransformPoint(0, -0.11f, 0), shield.position);
                            Check(frontGripError < 0.055f && rearGripError < 0.055f,
                                "Axe and shield follow their own wrists facing " + sign + " under parent scale " + parentSign + $" ({frontGripError:0.0000}/{rearGripError:0.0000})");
                            Check(!instance.GetComponent<Animator>().enabled, "Live hero has a single procedural pose writer");
                        }
                    }
                }
                if (kind == "Player" || kind == "Archer" || kind == "Mage")
                {
                    var rig = instance.GetComponent<ArticulatedRig>();
                    instance.transform.localScale = Vector3.one; face.Face(1);
                    var skeleton = instance.transform.Find("Visual/Rig");
                    var knee = skeleton.Find("Pelvis/FrontLeg/Lower");
                    rig.EvaluatePose(Mathf.PI / 2, 7.5f, true, 0, 0, -1);
                    float shinLength = kind == "Player" ? 0.31f : 0.44f;
                    float stanceY = skeleton.InverseTransformPoint(knee.TransformPoint(new Vector3(0, -shinLength, 0))).y;
                    rig.EvaluatePose(Mathf.PI * 1.5f, 7.5f, true, 0, 0, -1);
                    float swingY = skeleton.InverseTransformPoint(knee.TransformPoint(new Vector3(0, -shinLength, 0))).y;
                    Check(stanceY < (kind == "Player" ? 0.14f : 0.10f) && swingY > stanceY + 0.1f, kind + " plants the backwards-moving foot and lifts the forwards return");
                    foreach (string armName in new[] { "FrontArm", "BackArm" })
                    {
                        var shoulder = skeleton.Find("Pelvis/Torso/" + armName);
                        foreach (var joint in new[] { shoulder, shoulder.Find("Lower") })
                        {
                            var image = joint.Find("Sprite");
                            Check(image != null && Vector2.Distance(image.localPosition, Vector2.zero) < 0.001f && Mathf.Abs(Mathf.DeltaAngle(0, image.localEulerAngles.z)) > 1,
                                kind + " " + armName + " " + joint.name + " artwork pivots at the joint with original inclination corrected");
                        }
                    }
                }
                Destroy(instance); yield return null;
            }
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            player.GetComponent<AnimationDriver>().Face(1);
            yield return new WaitForSeconds(0.25f);
            float initialY = body.position.y;
            // Exercise the actual movement method with deterministic input, without OS key injection.
            for (int i = 0; i < 25; i++) { Set(player, "horizontal", 1f); Call(player, "FixedUpdate"); yield return new WaitForFixedUpdate(); }
            Check(body.position.x > 4, "Run follows serpent slope");
            Set(player, "horizontal", 0f); Set(player, "bufferedJump", 0.12f); Call(player, "FixedUpdate");
            Check(body.linearVelocity.y > 10, "Buffered grounded jump launches Rigidbody2D");
            for (int i = 0; i < 65; i++) { Call(player, "FixedUpdate"); yield return new WaitForFixedUpdate(); }
            Check(Mathf.Abs(body.position.y - game.GroundHeight(body.position.x)) < 0.15f, "Jump falls and lands on actual surface");
            Set(player, "crouchRequested", true); Call(player, "FixedUpdate");
            var hitbox = player.GetComponent<BoxCollider2D>();
            Check(hitbox.size.y < 1.5f && Mathf.Abs(hitbox.offset.y - hitbox.size.y * 0.5f) < 0.01f, "Crouch collider reduces height with fixed feet");
            var roof = new GameObject("AcceptanceRoof") { layer = LayerMask.NameToLayer("OMT_Ground") };
            roof.transform.position = player.transform.position + Vector3.up * 1.68f; roof.AddComponent<BoxCollider2D>().size = new Vector2(3, 0.2f);
            Physics2D.SyncTransforms(); Set(player, "crouchRequested", false); Call(player, "FixedUpdate");
            Check(hitbox.size.y < 1.5f, "Low ceiling prevents standing");
            Destroy(roof); yield return null; Call(player, "FixedUpdate");
            Check(hitbox.size.y > 1.9f, "Standing resumes with clearance");
            var guard = player.GetComponent<PlayerGuard>();
            Check(guard != null, "Player prefab contains shield guard");
            player.Health.ResetHealth(100, Team.Player); Set(player.Health, "invulnerabilitySeconds", 0f);
            guard.SetBlocking(true);
            Check(guard.IsBlocking && !melee.RequestAttack(12, 1.6f), "Held shield prevents attacking");
            player.Health.TakeDamage(new DamageInfo(20, Team.Enemy));
            Check(Mathf.Abs(player.Health.Current - 93) < 0.001f, "Shield reduces enemy damage by 65 percent");
            Set(player, "horizontal", 1f); Call(player, "FixedUpdate");
            Check(Mathf.Abs(body.linearVelocity.x - player.Stats.Current.moveSpeed * 0.45f) < 0.001f, "Shield permits slower walking");
            player.Health.DamageTakenMultiplier = 0.85f;
            player.Health.TakeDamage(new DamageInfo(20, Team.Enemy));
            Check(Mathf.Abs(player.Health.Current - 87.05f) < 0.001f, "Shield composes with permanent armor");
            game.TogglePause();
            Check(!guard.IsBlocking && player.Health.GuardDamageMultiplier == 1, "Pause releases shield and temporary protection");
            game.TogglePause();
            player.Health.TakeDamage(new DamageInfo(20, Team.Enemy));
            Check(Mathf.Abs(player.Health.Current - 70.05f) < 0.001f, "Releasing shield restores normal armored damage");
            guard.SetBlocking(true); player.Health.TakeDamage(new DamageInfo(10, Team.Environment));
            Check(Mathf.Abs(player.Health.Current - 61.55f) < 0.001f, "Shield does not protect against environmental damage");
            guard.SetBlocking(false); Set(player, "horizontal", 0f);
            player.Health.ResetHealth(100, Team.Player);
            // Isolate hit tests from downhill sliding while the input controller is disabled.
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            var driver = player.GetComponent<AnimationDriver>(); driver.Face(1);
            var a = Dummy("MultipleColliders", new Vector2(0.8f, 1)); a.gameObject.AddComponent<CircleCollider2D>().radius = 0.35f;
            var b = Dummy("SecondEnemy", new Vector2(1.2f, 1));
            var friend = Dummy("Ally", new Vector2(0.7f, 0.9f), Team.Player);
            Physics2D.SyncTransforms();
            Check(melee.RequestAttack(12, 1.6f), "Melee swing starts"); driver.Face(-1);
            Check(driver.Facing == 1, "Facing remains locked during swing");
            yield return new WaitForSeconds(0.1f); Check(a.Current == 100, "Anticipation deals no damage");
            yield return new WaitForSeconds(0.18f); CaptureFrame("axe-play-mode");
            yield return new WaitForSeconds(0.42f);
            Check(a.Current == 88 && b.Current == 88, "One damage per enemy per swing despite multiple colliders; sweep hits multiple enemies");
            Check(friend.Current == 100 && player.Health.Current == 100, "Melee does not damage self or allies");
            Check(FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0, "Axe generates no projectiles");
            Destroy(a.gameObject); Destroy(b.gameObject); Destroy(friend.gameObject); yield return null;
            driver.Face(-1); var left = Dummy("LeftEnemy", new Vector2(-0.8f, 1)); Physics2D.SyncTransforms(); melee.RequestAttack(12, 1.6f);
            yield return new WaitForSeconds(0.7f); Check(left.Current == 88, "Left-facing axe deals damage"); Destroy(left.gameObject); yield return null;
            driver.Face(1); var blocked = Dummy("Blocked", new Vector2(1.1f, 1));
            var wall = new GameObject("AcceptanceWall") { layer = LayerMask.NameToLayer("OMT_Ground") };
            wall.transform.position = player.transform.position + new Vector3(0.55f, 1, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(0.12f, 2);
            Physics2D.SyncTransforms(); melee.RequestAttack(12, 1.6f); yield return new WaitForSeconds(0.7f);
            Check(blocked.Current == 100, "Wall blocks melee hit"); Destroy(wall); Destroy(blocked.gameObject); yield return null;
            var paused = Dummy("PauseTarget", new Vector2(0.8f, 1)); Physics2D.SyncTransforms(); melee.RequestAttack(12, 1.6f);
            yield return new WaitForSeconds(0.06f); game.TogglePause();
            Check(!melee.IsAttacking && !melee.RequestAttack(12, 1.6f), "Pause cancels swing and rejects attacks");
            yield return new WaitForSecondsRealtime(0.15f); game.TogglePause(); yield return new WaitForSeconds(0.7f);
            Check(paused.Current == 100, "Resume does not replay cancelled damage"); Destroy(paused.gameObject); yield return null;
            // Rapid rate must still cross the hit window exactly once.
            var fast = Dummy("FastTarget", new Vector2(0.8f, 1)); Physics2D.SyncTransforms(); melee.RequestAttack(12, 4f);
            yield return new WaitForSeconds(0.29f); Check(fast.Current == 88, "Maximum axe cadence preserves single hit window"); Destroy(fast.gameObject); yield return null;
            driver.Face(1);
            var buffered = Dummy("BufferedCombo", new Vector2(0.8f, 1)); Physics2D.SyncTransforms();
            melee.RequestAttack(12, 1.6f); yield return new WaitForSeconds(0.51f); melee.RequestAttack(12, 1.6f);
            yield return new WaitForSeconds(0.63f);
            Check(buffered.Current == 76, "Late input buffer chains exactly one further swing");
            melee.Cancel(); Destroy(buffered.gameObject); yield return null;
            var visual = player.transform.Find("Visual");
            var scales = new Dictionary<Transform, Vector3>();
            foreach (var part in visual.GetComponentsInChildren<Transform>()) scales[part] = part.localScale;
            var poseRig = player.GetComponent<ArticulatedRig>();
            poseRig.EvaluatePose(1, 7.5f, true, 1, 0, 0.43f);
            foreach (var part in scales) Check(part.Key.localScale == part.Value, "Articulated crouch keeps scale of " + part.Key.name);
            Place(game.PhaseStart + 4);
            string scenePath = EditorBuildSettings.scenes[0].path;
            string generated = scenePath.Substring(0, scenePath.LastIndexOf("/Scenes/", StringComparison.Ordinal));
            var pool = FindFirstObjectByType<ProjectilePool>();
            foreach (string kind in new[] { "Archer", "Mage" })
            {
                var rangedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(generated + "/Prefabs/" + kind + ".prefab");
                float x = body.position.x + 5;
                var enemy = Instantiate(rangedPrefab, new Vector3(x, game.GroundHeight(x) + 0.04f, 0), Quaternion.identity).GetComponent<EnemyAI>();
                enemy.Initialize(game, player, pool, 1, LayerMask.GetMask("OMT_Ground"));
                bool found = false; float deadline = Time.time + 1.8f;
                while (!found && Time.time < deadline)
                {
                    foreach (var shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                    {
                        string art = shot.GetComponent<SpriteRenderer>().sprite.name;
                        if (kind == "Archer" ? art.Contains("flecha") : art.Contains("magia")) found = true;
                    }
                    yield return new WaitForFixedUpdate();
                }
                if (!found)
                {
                    var weapon = enemy.GetComponent<ProjectileWeapon>();
                    Debug.Log("RANGED_DIAGNOSTIC " + kind + " state=" + game.State + " alive=" + enemy.IsAlive + " playerDead=" + player.Health.IsDead + " enemy=" + enemy.transform.position + " target=" + player.AimPosition + " muzzle=" + weapon.MuzzlePosition
                        + " preparing=" + Field(enemy.GetType(), "preparing").GetValue(enemy) + " next=" + Field(enemy.GetType(), "nextAttack").GetValue(enemy) + " now=" + Time.time
                        + " floor=" + Physics2D.Linecast(weapon.MuzzlePosition, player.AimPosition, LayerMask.GetMask("OMT_Ground")).collider);
                    CaptureFrame("ranged-diagnostic");
                }
                Check(found, kind + " emits its own projectile artwork after preparing attack");
                Destroy(enemy.gameObject); pool.ClearActive(); yield return null;
            }
            var wolfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(generated + "/Prefabs/WhiteWolf.prefab");
            float wolfX = body.position.x + 1.7f;
            var wolfActor = Instantiate(wolfPrefab, new Vector3(wolfX, game.GroundHeight(wolfX) + 0.04f, 0), Quaternion.identity).GetComponent<EnemyAI>();
            wolfActor.Initialize(game, player, pool, 1, LayerMask.GetMask("OMT_Ground"));
            Set(player.Health, "invulnerabilitySeconds", 0f); float hpBefore = player.Health.Current;
            yield return new WaitForSeconds(1.1f);
            Check(player.Health.Current < hpBefore && player.Health.Current >= hpBefore - 11, "Wolf bite applies one damage per swing without projectiles");
            Check(FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0, "Wolf creates no projectiles");
            Check(player.LastKnockback.x < -2f && player.LastKnockback.y < 3.5f, "P2: wolf bite knocks the player back horizontally away from the wolf: " + player.LastKnockback);
            Destroy(wolfActor.gameObject); player.Health.ResetHealth(100, Team.Player); yield return null;
            // P1: player and enemy bodies never collide physically.
            Check(Physics2D.GetIgnoreLayerCollision(LayerMask.NameToLayer("OMT_Player"), LayerMask.NameToLayer("OMT_Enemy")), "P1: player/enemy physics collisions are ignored");
            Check(!Physics2D.GetIgnoreLayerCollision(LayerMask.NameToLayer("OMT_Enemy"), LayerMask.NameToLayer("OMT_Ground")), "P1: enemies keep terrain collisions");
            {
                Place(game.PhaseStart + 9);
                float wx = body.position.x;
                var under = Instantiate(wolfPrefab, new Vector3(wx, game.GroundHeight(wx) + 0.04f, 0), Quaternion.identity).GetComponent<EnemyAI>();
                under.Initialize(game, player, pool, 1, LayerMask.GetMask("OMT_Ground"));
                under.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
                Set(player.Health, "invulnerabilitySeconds", 10f); player.Health.TakeDamage(new DamageInfo(1, Team.Enemy));
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.position = new Vector2(wx, game.GroundHeight(wx) + 2.2f); body.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.7f);
                Check(body.position.y - game.GroundHeight(body.position.x) < 0.35f, "P1: player falls through a wolf to the ground instead of standing on it: " + (body.position.y - game.GroundHeight(body.position.x)).ToString("0.00"));
                Check(Mathf.Abs(under.transform.position.y - game.GroundHeight(under.transform.position.x)) < 0.35f, "P1: wolf stays on the terrain");
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                Destroy(under.gameObject); player.Health.ResetHealth(100, Team.Player); Set(player.Health, "invulnerabilitySeconds", 0f); yield return null;
            }
            {
                // P6: dodge = short dash with invulnerability frames, then normal vulnerability again.
                Place(game.PhaseStart + 20); player.GetComponent<AnimationDriver>().Face(1);
                player.enabled = true; body.constraints = RigidbodyConstraints2D.FreezeRotation;
                float dodgeStart = body.position.x; int dodges = player.DodgeCount;
                Set(player, "dodgeRequested", true);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                bool invulnerable = player.Health.DodgeInvulnerable;
                float hp = player.Health.Current; player.Health.TakeDamage(new DamageInfo(10, Team.Enemy));
                Check(invulnerable && player.Health.Current == hp && player.DodgeCount == dodges + 1, "P6: dodge grants invulnerability frames");
                yield return new WaitForSeconds(0.45f);
                Check(body.position.x - dodgeStart > 1.2f && !player.Health.DodgeInvulnerable, "P6: dodge dashes forward and invulnerability ends: " + (body.position.x - dodgeStart).ToString("0.00"));
                player.enabled = false; body.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            // Use live AI and real axe hits so steering cannot conceal a missing push.
            var impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(generated + "/Prefabs/Archer.prefab");
            var heroDriver = player.GetComponent<AnimationDriver>();
            foreach (int sign in new[] { 1, -1 })
            {
                Place(game.PhaseStart + 8); heroDriver.Face(sign);
                float x = body.position.x + sign;
                var target = Instantiate(impactPrefab, new Vector3(x, game.GroundHeight(x) + 0.04f, 0), Quaternion.identity).GetComponent<EnemyAI>();
                target.Initialize(game, player, pool, 1, LayerMask.GetMask("OMT_Ground"));
                target.Health.ResetHealth(100, Team.Enemy);
                yield return new WaitForSeconds(0.12f);
                float beforeX = target.transform.position.x;
                Check(melee.RequestAttack(12, 1.6f), "Axe starts impact test facing " + sign);
                yield return new WaitForSeconds(0.26f);
                float pushed = (target.transform.position.x - beforeX) * sign;
                Check(target.Health.Current == 88 && pushed > 0.05f && pushed < 0.7f,
                    "Axe pushes live enemy AI a short distance backwards facing " + sign + ": " + pushed.ToString("0.00"));
                yield return new WaitForSeconds(0.45f);
                // The archer now steps back to keep a firing distance, so only residual knockback (3 u/s) is a failure.
                Check(Mathf.Abs(target.GetComponent<Rigidbody2D>().linearVelocity.x) < 2f, "Enemy resumes its own steering after short push (no residual knockback)");
                Destroy(target.gameObject); pool.ClearActive(); yield return null;
            }
            Place(game.PhaseStart + 8); heroDriver.Face(1);
            player.Health.ResetHealth(100, Team.Player); Set(player.Health, "invulnerabilitySeconds", 0f);
            player.Health.TakeDamage(new DamageInfo(30, Team.Enemy));
            float killX = body.position.x + 1;
            var killTarget = Instantiate(impactPrefab, new Vector3(killX, game.GroundHeight(killX) + 0.04f, 0), Quaternion.identity).GetComponent<EnemyAI>();
            killTarget.Initialize(game, player, pool, 1, LayerMask.GetMask("OMT_Ground"));
            killTarget.Health.ResetHealth(10, Team.Enemy); Call(game.Spawner, "Track", killTarget);
            melee.RequestAttack(12, 1.6f); yield return new WaitForSeconds(0.3f);
            Check(killTarget.Health.IsDead && player.Health.Current == 75, "Actual axe kill restores exactly 5 player HP");
            killTarget.Health.Kill();
            Check(player.Health.Current == 75, "Same enemy death cannot heal twice");
            yield return new WaitForSeconds(0.4f);
            var capped = Instantiate(impactPrefab, new Vector3(killX + 5, game.GroundHeight(killX + 5) + 0.04f, 0), Quaternion.identity).GetComponent<EnemyAI>();
            capped.Initialize(game, player, pool, 1, LayerMask.GetMask("OMT_Ground")); Call(game.Spawner, "Track", capped);
            player.Health.ResetHealth(100, Team.Player); player.Health.TakeDamage(new DamageInfo(2, Team.Enemy));
            capped.Health.TakeDamage(new DamageInfo(1000, Team.Player));
            Check(player.Health.Current == 100, "Kill healing is capped at maximum player HP");
            var environmental = Instantiate(impactPrefab, new Vector3(killX + 5, game.GroundHeight(killX + 5) + 0.04f, 0), Quaternion.identity).GetComponent<EnemyAI>();
            environmental.Initialize(game, player, pool, 1, LayerMask.GetMask("OMT_Ground")); Call(game.Spawner, "Track", environmental);
            player.Health.ResetHealth(100, Team.Player); player.Health.TakeDamage(new DamageInfo(30, Team.Enemy));
            environmental.Health.Kill();
            Check(player.Health.Current == 70, "Environmental death does not award player kill healing");
            player.Health.ResetHealth(100, Team.Player); pool.ClearActive(); yield return new WaitForSeconds(0.55f);
            int applied = 0;
            Action<UpgradeDefinition> observe = ignored => applied++;
            player.Stats.UpgradeApplied += observe;
            var feedback = player.GetComponent<UpgradeFeedback>();
            foreach (var upgrade in UpgradeDeck.Catalog)
            {
                player.Stats.ResetStats(new StatBlock());
                player.Health.TakeDamage(new DamageInfo(30, Team.Enemy));
                int beforeApplied = applied;
                player.Stats.ApplyUpgrade(upgrade);
                Check(applied == beforeApplied + 1 && feedback.IsActive && feedback.IsPact == upgrade.HasTradeoff,
                    upgrade.Id + " applies once and starts matching visual feedback");
                Check(player.Stats.Current.Get(upgrade.Stat) != new StatBlock().Get(upgrade.Stat), upgrade.Id + " changes its own distinct benefit");
                Check(Ref<Text>(ui, "statsText").text.Contains("Cura/baja"), "HUD displays kill healing and resistance");
                Check(player.Stats.Current.Get(upgrade.TradeoffStat) != new StatBlock().Get(upgrade.TradeoffStat) || upgrade.TradeoffStat == upgrade.Stat,
                    upgrade.Id + " really applies its DEBUFF too");
                Check(UpgradeDefinition.Playable(player.Stats.Current), upgrade.Id + " leaves the hero playable");
                if (upgrade.Id == "midgard") Check(Mathf.Abs(player.Health.Current - 95) < 0.001f && Mathf.Abs(player.Health.Maximum - 125) < 0.001f
                    && Mathf.Abs(player.Stats.Current.moveSpeed - 6.9f) < 0.001f, "Resistencia de Midgard: +25% max HP (heals only the added capacity) and -8% speed");
                if (upgrade.Id == "berserker")
                {
                    float hp = player.Health.Current;
                    player.Health.TakeDamage(new DamageInfo(20, Team.Enemy));
                    Check(Mathf.Abs(player.Health.Current - (hp - 22.4f)) < 0.01f && Mathf.Abs(player.Stats.Current.damage - 15f) < 0.001f,
                        "Furia del Berserker: +25% damage and really takes 12% more damage");
                }
                if (upgrade.Id == "stone")
                {
                    float hp = player.Health.Current;
                    player.Health.TakeDamage(new DamageInfo(20, Team.Enemy));
                    Check(Mathf.Abs(player.Health.Current - (hp - 17)) < 0.001f && Mathf.Abs(player.Stats.Current.moveSpeed - 6.75f) < 0.001f,
                        "Stone pact actually reduces incoming damage 15 percent and movement 10 percent");
                }
                if (upgrade.Id == "vampire")
                {
                    float hp = player.Health.Current;
                    var victim = Instantiate(impactPrefab, new Vector3(killX + 6, game.GroundHeight(killX + 6) + 0.04f, 0), Quaternion.identity).GetComponent<EnemyAI>();
                    victim.Initialize(game, player, pool, 1, LayerMask.GetMask("OMT_Ground")); Call(game.Spawner, "Track", victim);
                    victim.Health.TakeDamage(new DamageInfo(1000, Team.Player));
                    Check(player.Health.Current == hp + 7 && player.Health.Maximum == 90, "Vampire pact increases real kill healing to 7 with 90 maximum HP");
                }
                yield return new WaitForSecondsRealtime(0.12f);
                CaptureUIFrame("upgrade-" + upgrade.Id, ui);
            }
            player.Stats.UpgradeApplied -= observe;
            Time.timeScale = 0;
            var effectRoot = player.transform.Find("UpgradeRing");
            var beforeEffect = effectRoot.position; player.transform.position += Vector3.right * 2;
            Check(Vector3.Distance(effectRoot.position - beforeEffect, Vector3.right * 2) < 0.001f, "Upgrade effect follows phase relocation of player root");
            yield return new WaitForSecondsRealtime(1.2f);
            Check(!feedback.IsActive && !effectRoot.GetComponent<SpriteRenderer>().enabled, "Feedback finishes even while time is paused");
            Time.timeScale = 1;
            player.Stats.ResetStats(new StatBlock());
            Check(player.Stats.Current.killHealing == 5 && player.Health.DamageTakenMultiplier == 1 && !feedback.IsActive, "Reset removes all pact benefits, costs and particles");
            int ground = LayerMask.GetMask("OMT_Ground");
            for (int phase = 1; phase <= 6; phase++)
            {
                game.Surface.Configure(phase, game.PhaseStart, game.PhaseEnd);
                for (int i = 0; i <= 20; i++)
                {
                    float x = Mathf.Lerp(game.PhaseStart, game.PhaseEnd, i / 20f);
                    // Platforms share the ground layer: sample the serpent surface collider itself.
                    RaycastHit2D ray = default; var surfaceCollider = game.Surface.GetComponent<Collider2D>();
                    foreach (var h in Physics2D.RaycastAll(new Vector2(x, game.Surface.MaximumY + 5), Vector2.down, 30, ground)) if (h.collider == surfaceCollider) { ray = h; break; }
                    Check(ray.collider != null && Mathf.Abs(ray.point.y - game.GroundHeight(x)) < 0.03f, "Surface sampling matches collider phase " + phase + " point " + i);
                }
            }
            game.Surface.Configure(1, game.PhaseStart, game.PhaseEnd); Place(game.PhaseEnd - 2);
            player.Health.ResetHealth(10000, Team.Player); Set(player.Health, "invulnerabilitySeconds", 9999f);
            game.Spawner.enabled = true;
            for (int phase = 1; phase <= 5; phase++)
            {
                Check(!game.Portals.ExitOpen && !game.Portals.RequestEnter(), "Portal stays closed and unusable before objectives, phase " + phase);
                // P7: every winter receives its own functional scenery.
                var traps = FindObjectsByType<ArenaTrap>(FindObjectsSortMode.None);
                Check(game.Stage.Count >= 6 && FindObjectsByType<OneWayPlatform>(FindObjectsSortMode.None).Length >= 1 && (phase == 1 || traps.Length >= 1),
                    "P7: phase " + phase + " has platforms, covers and traps (" + game.Stage.Count + " props, " + traps.Length + " traps)");
                foreach (var t in traps) Check(!t.Dangerous || t.State == TrapState.Active, "P7: trap " + t.Kind + " only hurts in its active state");
                if (phase == 2)
                {
                    // P8: an archer anchored on an elevated post stays there, never falls, and the post is reachable by jumping.
                    Check(ArcherPost.All.Count >= 1, "P8: winter one has an elevated archer post");
                    var post = ArcherPost.All[0];
                    float postHeight = post.TopY - game.GroundHeight(post.CenterX);
                    Check(postHeight > 1.5f && postHeight < 2.75f, "P8: archer post is elevated but reachable with one jump: " + postHeight.ToString("0.00"));
                    string archerScene = EditorBuildSettings.scenes[0].path;
                    var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(archerScene.Substring(0, archerScene.LastIndexOf("/Scenes/", StringComparison.Ordinal)) + "/Prefabs/Archer.prefab");
                    var postArcher = Instantiate(archerPrefab, new Vector3(post.CenterX, post.TopY + 0.05f, 0), Quaternion.identity).GetComponent<EnemyAI>();
                    postArcher.Initialize(game, player, FindFirstObjectByType<ProjectilePool>(), 2, LayerMask.GetMask("OMT_Ground"));
                    postArcher.AnchorTo(post);
                    Place(post.CenterX - 6f);
                    yield return new WaitForSeconds(3f);
                    Check(Mathf.Abs(postArcher.transform.position.y - post.TopY) < 0.35f && postArcher.transform.position.x >= post.MinX - 0.2f && postArcher.transform.position.x <= post.MaxX + 0.2f,
                        "P8: archer stays on its post while engaging (no falling, no walking off)");
                    postArcher.Health.Kill(); yield return new WaitForSeconds(0.2f);
                    FindFirstObjectByType<ProjectilePool>().ClearActive();
                }
                float hpAtStart = player.Health.Current, damageAtStart = player.Stats.Current.damage;
                Place(game.PhaseEnd - 1.3f);
                float until = Time.realtimeSinceStartup + 20; Time.timeScale = 10;
                while (game.State == RunState.Playing && Time.realtimeSinceStartup < until)
                {
                    Place(game.PhaseEnd - 1.1f);
                    foreach (var enemy in FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
                    {
                        if (!enemy.IsAlive) continue;
                        var anchoredArcher = enemy as EnemyAI;
                        if (anchoredArcher != null && anchoredArcher.Post != null)
                            Check(Mathf.Abs(enemy.transform.position.y - anchoredArcher.Post.TopY) < 0.35f, "P8: archer stands on its elevated post, phase " + phase);
                        else
                            Check(Mathf.Abs(enemy.transform.position.y - game.GroundHeight(enemy.transform.position.x)) < 0.3f, "Enemy spawns on surface phase " + phase);
                        bool wolf = enemy.name.Contains("Wolf");
                        Check(!wolf || (enemy.GetComponent<MeleeWeapon>() != null && enemy.GetComponent<ProjectileWeapon>() == null), "Wolf prefab uses melee only");
                        enemy.Health.Kill();
                    }
                    if (game.Portals.ExitOpen) game.Portals.RequestEnter();
                    yield return null;
                }
                Check(game.Portals.Exit.State == PortalState.Transition && !game.Portals.RequestEnter(), "Portal entered once; no duplicated transition, phase " + phase);
                Check(Mathf.Abs(player.Health.Current - hpAtStart) < 0.001f && Mathf.Abs(player.Stats.Current.damage - damageAtStart) < 0.001f, "Portal travel keeps health and stats, phase " + phase);
                Check(game.State == RunState.ChoosingUpgrade && game.Phase == phase, "Phase " + phase + " clears into paused upgrade choice");
                if (phase == 1)
                {
                    var notice = (CanvasGroup)Field(typeof(UIManager), "upgradeNotice").GetValue(ui);
                    Check(!notice.gameObject.activeSelf, "Previous upgrade notice never overlaps the next choice");
                    CaptureUIFrame("upgrade-cards", ui);
                    foreach (var card in Ref<GameObject>(ui, "upgradePanel").GetComponentsInChildren<UpgradeCardUI>())
                    {
                        var text = Ref<Text>(card, "description");
                        Check(text.preferredHeight <= text.rectTransform.rect.height, "Upgrade description fits card without clipping");
                        Check(text.text.Contains("→"), "Card shows effective before and after values");
                        Check(text.text.Contains("BUFF") && text.text.Contains("DEBUFF") && text.supportRichText, "P5: card shows name, description, BUFF and DEBUFF in colour");
                    }
                }
                var offered = (UpgradeDefinition[])Field(typeof(UpgradeManager), "choices").GetValue(FindFirstObjectByType<UpgradeManager>());
                var chosen = offered[0]; var statsBefore = player.Stats.Current.Copy();
                game.SelectUpgrade(0); game.SelectUpgrade(1);
                float b0 = statsBefore.Get(chosen.Stat), b1 = player.Stats.Current.Get(chosen.Stat);
                bool buffed = chosen.Stat == StatKind.DamageTaken ? b1 < b0 : b1 > b0;
                Check(game.Phase == phase + 1 && game.IsPlaying && buffed && UpgradeDefinition.Playable(player.Stats.Current),
                    "Single choice advances once, applies its BUFF and keeps the hero playable (" + chosen.Id + ")");
                // In the boss arena the presentation intentionally holds control; elsewhere control returns at once.
                Check(!game.Portals.ExitOpen && (phase + 1 == 6 ? player.ControlLocked : !player.ControlLocked) && body.position.x < game.PhaseStart + 2.5f
                    && Mathf.Abs(body.position.y - game.GroundHeight(body.position.x)) < 0.3f, "Next phase starts on the terrain with the exit reset, phase " + (phase + 1));
            }
            Check(game.Phase == 6 && game.Spawner.CurrentBoss != null, "Boss spawns in phase six");
            Place(game.PhaseStart + 4);
            var boss = game.Spawner.CurrentBoss;
            player.Health.Invulnerable = true; // this block checks the boss flow; damage checks switch it off explicitly
            Set(player.Health, "invulnerabilitySeconds", 0f);
            Check(!boss.IntroFinished && boss.Health.Invulnerable && player.ControlLocked, "Boss presentation locks control and protects the boss");
            float bossHp = boss.Health.Current; boss.Health.TakeDamage(new DamageInfo(50, Team.Player));
            Check(boss.Health.Current == bossHp, "Boss takes no damage during the presentation");
            Check(FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0, "Silent roar: no AudioSource exists in the scene");
            var serpent = boss.Body;
            var bossBody = boss.GetComponent<Rigidbody2D>();
            Check(serpent != null && serpent.Built && serpent.HasArt && bossBody.bodyType == RigidbodyType2D.Kinematic,
                "P3: Jörmungandr is built from the phase atlases and anchored (kinematic, never displaced)");
            Check(serpent.PlateCount >= 6 && boss.FrontX < game.PhaseEnd - 3f, "P3: the scenery is its body: scale plates embedded along the arena floor");
            Check(ArenaCover.All.Count >= 2 && FindObjectsByType<OneWayPlatform>(FindObjectsSortMode.None).Length >= 3, "P7: boss arena has useful cover and high platforms only");
            Vector3 anchored = boss.transform.position;
            Time.timeScale = 3; float introDeadline = Time.realtimeSinceStartup + 12; bool introShot = false;
            while (!boss.IntroFinished && Time.realtimeSinceStartup < introDeadline)
            {
                if (FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length > 0 || !player.ControlLocked) introShot = true;
                yield return null;
            }
            Check(boss.IntroFinished && !introShot, "Presentation completes without attacks and without releasing control early");
            Check(!player.ControlLocked && !boss.Health.Invulnerable, "Battle starts: control returns and the boss can be damaged");
            // P1: the boss is a wall the player cannot walk through, although physics between them is off.
            player.enabled = true; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.position = new Vector2(boss.FrontX + 2f, game.GroundHeight(boss.FrontX + 2f) + 0.05f);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(body.position.x <= boss.FrontX, "P1: player is held in front of the serpent's body: " + body.position.x.ToString("0.00") + " <= " + boss.FrontX.ToString("0.00"));
            player.enabled = false; body.constraints = RigidbodyConstraints2D.FreezeAll;
            // P4: every attack, one by one: Anticipation → Active → Recovery, hazards tied to it, never a projectile.
            boss.AutoAttack = false; Time.timeScale = 2;
            float settle = Time.realtimeSinceStartup + 6;
            while (boss.CurrentAttack != BossAttack.None && Time.realtimeSinceStartup < settle) yield return null;
            var attacks = new[] { BossAttack.MordidaDeMidgard, BossAttack.VentiscaRasante, BossAttack.OlaDeEscamas, BossAttack.ErupcionDeColmillos,
                BossAttack.RespiracionDeVeneno, BossAttack.ContraccionDosTiempos, BossAttack.EspinasCarmesi, BossAttack.AlientoDelRagnarok,
                BossAttack.FracturaDelMundo, BossAttack.JuicioDeLaSerpiente };
            var cover = ArenaCover.All[0];
            foreach (var c in ArenaCover.All) if (c.Bounds.center.x > cover.Bounds.center.x && c.Bounds.center.x < boss.FrontX) cover = c;
            foreach (var attack in attacks)
            {
                bool sawA = false, sawB = false, sawC = false, projectile = false; int maxHazards = 0;
                Place(boss.FrontX - 3.5f);
                var run = boss.StartCoroutine(boss.Run(attack));
                float limit = Time.realtimeSinceStartup + 14; bool started = false;
                while (Time.realtimeSinceStartup < limit)
                {
                    sawA |= boss.AttackStageNow == AttackStage.Anticipation; sawB |= boss.AttackStageNow == AttackStage.Active; sawC |= boss.AttackStageNow == AttackStage.Recovery;
                    started |= boss.CurrentAttack != BossAttack.None;
                    projectile |= FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length > 0;
                    maxHazards = Mathf.Max(maxHazards, boss.HazardCount);
                    if (started && sawC && boss.AttackStageNow == AttackStage.Recovery && boss.CurrentAttack == attack && boss.StageProgress >= 0.99f) break;
                    yield return null;
                }
                yield return new WaitForSeconds(0.9f);
                Check(sawA && sawB && sawC, "P4: " + attack + " runs Anticipation, Active and Recovery");
                Check(!projectile, "P4: " + attack + " creates no projectile or bullet");
                Check(maxHazards >= 1, "P4: " + attack + " shows a ground warning or hazard tied to the attack");
                boss.StopCoroutine(run);
            }
            // Counterplay: frost wind hurts in the open, never behind solid cover or on a high platform.
            player.Health.Invulnerable = false; player.Health.ResetHealth(10000, Team.Player); Set(player.Health, "invulnerabilitySeconds", 0f);
            foreach (int spot in new[] { 0, 1, 2 })
            {
                OneWayPlatform high = null;
                foreach (var platform in FindObjectsByType<OneWayPlatform>(FindObjectsSortMode.None)) if (high == null || platform.transform.position.x > high.transform.position.x) high = platform;
                float hp0 = player.Health.Current;
                if (spot == 0) Place(boss.FrontX - 4f);
                if (spot == 1) Place(cover.Bounds.min.x - 0.35f);
                if (spot == 2) { float x = high.Surface.bounds.center.x; body.position = new Vector2(x, high.TopY + 0.03f); Physics2D.SyncTransforms(); }
                var wind = boss.StartCoroutine(boss.Run(BossAttack.VentiscaRasante));
                float limit = Time.realtimeSinceStartup + 8;
                while (Time.realtimeSinceStartup < limit && !(boss.AttackStageNow == AttackStage.Recovery && boss.CurrentAttack == BossAttack.VentiscaRasante)) yield return null;
                boss.StopCoroutine(wind);
                bool hurt = player.Health.Current < hp0;
                Check(spot == 0 ? hurt : !hurt, spot == 0 ? "Counterplay: frost wind damages a player standing in the open" : spot == 1 ? "Counterplay: stone cover blocks the frost wind" : "Counterplay: a high platform is above the frost wind");
                yield return new WaitForSeconds(0.6f);
            }
            {
                float hp0 = player.Health.Current; Place(cover.Bounds.min.x - 0.3f);
                var breath = boss.StartCoroutine(boss.Run(BossAttack.AlientoDelRagnarok));
                float limit = Time.realtimeSinceStartup + 8;
                while (Time.realtimeSinceStartup < limit && !(boss.AttackStageNow == AttackStage.Recovery && boss.CurrentAttack == BossAttack.AlientoDelRagnarok)) yield return null;
                boss.StopCoroutine(breath);
                Check(player.Health.Current == hp0, "Counterplay: hugging solid cover blocks the Ragnarök breath");
                yield return new WaitForSeconds(0.6f);
            }
            player.Health.Invulnerable = true;
            Check(boss.transform.position == anchored, "P3: the serpent never moves its body (root fixed through every attack)");
            // P4 transitions: one heavy hit crosses BOTH thresholds; they queue and run in order.
            boss.AutoAttack = true;
            bossHp = boss.Health.Current;
            boss.Health.TakeDamage(new DamageInfo(boss.Health.Maximum * 0.7f, Team.Player));
            yield return null;
            Check(boss.Enraged && boss.Transitioning && boss.Stage == 2 && boss.Health.Invulnerable && boss.HazardCount == 0,
                "Threshold cancels the attack, clears hazards, makes the boss invulnerable and starts phase two");
            float hpAfter = boss.Health.Current;
            boss.Health.TakeDamage(new DamageInfo(100, Team.Player));
            Check(boss.Health.Current == hpAfter && boss.Health.Current > 0, "Boss is invulnerable during the transition; single health bar never resets");
            float waitUntil = Time.realtimeSinceStartup + 15;
            while (boss.TransitionStep != BossTransitionStep.Stun && Time.realtimeSinceStartup < waitUntil) yield return null;
            var trace = string.Join(",", boss.TransitionTrace);
            Check(trace.StartsWith("Cancel,MorphBody,MorphNeck,MorphHead,Roar,Stun"), "Transformation order body → neck → head → silent roar → stun: " + trace);
            var stun = boss.Stun;
            Check(stun.Active && player.ControlLocked && stun.FirstKey == KeyCode.A && stun.SecondKey == KeyCode.D && stun.RequiredCycles == 4,
                "Roar stun: A + D alternated, 4 cycles, player locked");
            Check(stun.Press(KeyCode.W) == false && stun.Press(KeyCode.D) == false && stun.Presses == 0, "QTE ignores keys outside the pair and out-of-order presses");
            for (int i = 0; i < 4; i++) { stun.Press(KeyCode.A); if (i < 3) Check(stun.CompletedCycles == i, "QTE counter " + i + "/4 after A"); stun.Press(KeyCode.D); }
            Check(stun.State == StunQteState.Recovered && !player.ControlLocked, "Eight alternating presses free the player (Recovered)");
            waitUntil = Time.realtimeSinceStartup + 15;
            while (boss.TransitionsCompleted < 1 && Time.realtimeSinceStartup < waitUntil) yield return null;
            waitUntil = Time.realtimeSinceStartup + 15;
            while (boss.TransitionStep != BossTransitionStep.Stun && Time.realtimeSinceStartup < waitUntil) yield return null;
            Check(boss.Stage == 3 && boss.TransitionsCompleted == 1 && boss.SpecialsExecuted == 1 && boss.AttackLog.Contains("QuebrantoGlacial"),
                "Queued second transition: Quebranto glacial ran once, then phase three began");
            Check(stun.FirstKey == KeyCode.W && stun.SecondKey == KeyCode.D, "Phase three stun uses W + D");
            // Timeout path: never a permanent lock, light penalty only.
            player.Health.Invulnerable = false; float hpStun = player.Health.Current;
            waitUntil = Time.realtimeSinceStartup + 6;
            while (stun.Active && Time.realtimeSinceStartup < waitUntil) yield return null;
            Check(stun.State == StunQteState.FailedTimeout && !player.ControlLocked && player.Health.Current < hpStun && player.Health.Current >= hpStun - 9.01f,
                "QTE timeout frees the player with a light penalty");
            player.Health.Invulnerable = true;
            waitUntil = Time.realtimeSinceStartup + 15;
            while (boss.Transitioning && Time.realtimeSinceStartup < waitUntil) yield return null;
            Check(!boss.Transitioning && boss.SpecialsExecuted == 2 && boss.AttackLog.Contains("EclipseCarmesi") && !boss.Health.Invulnerable,
                "Eclipse carmesí ran once after the player was freed; combat resumes");
            Check(serpent.NeckMorph >= 3 && serpent.HeadMorph >= 3, "Neck and head now show the phase-three drawings");
            int logged = boss.AttackLog.Count; float watch = Time.realtimeSinceStartup + 8; bool phaseOk = true, shotsSeen = false;
            while (Time.realtimeSinceStartup < watch)
            {
                shotsSeen |= FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length > 0;
                yield return null;
            }
            for (int i = logged; i < boss.AttackLog.Count; i++)
                phaseOk &= BossAI.PhaseOf((BossAttack)Enum.Parse(typeof(BossAttack), boss.AttackLog[i])) == 3;
            Check(boss.AttackLog.Count > logged && phaseOk && !shotsSeen, "Phase three only uses its own attacks and never fires projectiles");
            CaptureFrame("boss-play-mode");
            Time.timeScale = 1;
            // Death mid-stun closes everything cleanly.
            player.Health.Invulnerable = false;
            boss.Health.Kill(); yield return null;
            Check(game.State == RunState.Victory && Time.timeScale == 0, "Boss defeat ends run in victory");
            Check(FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0 && FindObjectsByType<BossHazard>(FindObjectsSortMode.None).Length == 0 && !stun.Active,
                "No projectiles, boss hazards or stun remain after the fight");
            int seed = game.Seed; game.StartNewRun();
            Check(game.Phase == 1 && player.Stats.Current.fireRate == 1.6f && player.Health.Current == 100 && game.Seed != seed, "Victory retry resets stats, timers and seed");
            Place(game.PhaseStart + 4); body.position += Vector2.up * 2;
            guard.SetBlocking(true);
            player.Health.Kill();
            Check(game.State == RunState.GameOver && !melee.IsAttacking && !guard.IsBlocking, "Death cancels combat and shield and opens result");
            yield return new WaitForSecondsRealtime(1.05f);
            var deadRig = player.transform.Find("Visual/Rig"); float corpseBottom = float.PositiveInfinity;
            foreach (var draw in deadRig.GetComponentsInChildren<SpriteRenderer>()) if (draw.enabled && draw.sprite != null) corpseBottom = Mathf.Min(corpseBottom, draw.bounds.min.y);
            Check(Mathf.Abs(corpseBottom - game.GroundHeight(player.transform.position.x)) < 0.08f, "Death lands on actual ground even when killed airborne at timeScale zero");
            var corpse = new Dictionary<Transform, Vector3>();
            foreach (var part in deadRig.GetComponentsInChildren<Transform>()) corpse[part] = new Vector3(part.localPosition.x, part.localPosition.y, part.localEulerAngles.z);
            CaptureFrame("shield-death-play-mode");
            yield return new WaitForSecondsRealtime(0.3f);
            foreach (var part in corpse) Check(Vector3.Distance(part.Value, new Vector3(part.Key.localPosition.x, part.Key.localPosition.y, part.Key.localEulerAngles.z)) < 0.001f, "Final corpse holds " + part.Key.name);
            game.StartNewRun(); Check(game.IsPlaying && player.Health.Current == 100, "Death retry restores player");
            game.ReturnToMenu(); Check(game.State == RunState.MainMenu, "Return to menu cleans run");
            Check(PlayerPrefs.GetInt("OMT.BestScore.v1", 0) == storedRecord, "Acceptance leaves persisted record unchanged");
            Check(errors == 0, "No runtime exceptions during acceptance");
        }
    }
}
#endif
