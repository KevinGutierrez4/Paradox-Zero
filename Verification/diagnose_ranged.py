from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig').replace('''                Check(found, kind + " emits''','''                if (!found)
                {
                    var weapon = enemy.GetComponent<ProjectileWeapon>();
                    Debug.Log("RANGED_DIAGNOSTIC " + kind + " state=" + game.State + " alive=" + enemy.IsAlive + " playerDead=" + player.Health.IsDead + " enemy=" + enemy.transform.position + " target=" + player.AimPosition + " muzzle=" + weapon.MuzzlePosition
                        + " preparing=" + Field(enemy.GetType(), "preparing").GetValue(enemy) + " next=" + Field(enemy.GetType(), "nextAttack").GetValue(enemy) + " now=" + Time.time
                        + " floor=" + Physics2D.Linecast(weapon.MuzzlePosition, player.AimPosition, LayerMask.GetMask("OMT_Ground")).collider);
                    CaptureFrame("ranged-diagnostic");
                }
                Check(found, kind + " emits''')
p.write_text(s,encoding='utf-8')
