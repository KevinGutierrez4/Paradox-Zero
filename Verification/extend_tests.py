from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('            yield return null; yield return null;','            if (File.Exists("Verification/play-pass.txt")) File.Delete("Verification/play-pass.txt");\n            yield return null; yield return null;',1)
needle='            int ground = LayerMask.GetMask("OMT_Ground");'
insert='''            var visual = player.transform.Find("Visual");
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
                yield return new WaitForSeconds(1.08f);
                bool found = false;
                foreach (var shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    string art = shot.GetComponent<SpriteRenderer>().sprite.name;
                    if (kind == "Archer" ? art.Contains("flecha") : art.Contains("magia")) found = true;
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
            Destroy(wolfActor.gameObject); player.Health.ResetHealth(100, Team.Player); yield return null;
'''
s=s.replace(needle,insert+needle)
p.write_text(s,encoding='utf-8')
