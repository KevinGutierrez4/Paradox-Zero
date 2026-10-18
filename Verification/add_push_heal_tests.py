from pathlib import Path
p=Path('Assets/OneMoreTime/Generated_Hacha_Final/RunConfig.asset');s=p.read_text(encoding='utf-8-sig')
if '  healOnKill:' not in s:s=s.replace('  recoveryBetweenPhases: 8','  recoveryBetweenPhases: 8\n  healOnKill: 5')
p.write_text(s,encoding='utf-8')
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig')
needle='''            int ground = LayerMask.GetMask("OMT_Ground");'''
insert='''            // Use live AI and real axe hits so steering cannot conceal a missing push.
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
                Check(Mathf.Abs(target.GetComponent<Rigidbody2D>().linearVelocity.x) < 0.5f, "Enemy resumes control after short push");
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
'''
assert needle in s;s=s.replace(needle,insert+needle)
p.write_text(s,encoding='utf-8')
