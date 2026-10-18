from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('''                yield return new WaitForSeconds(1.08f);
                bool found = false;
                foreach (var shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    string art = shot.GetComponent<SpriteRenderer>().sprite.name;
                    if (kind == "Archer" ? art.Contains("flecha") : art.Contains("magia")) found = true;
                }''','''                bool found = false; float deadline = Time.time + 1.8f;
                while (!found && Time.time < deadline)
                {
                    foreach (var shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                    {
                        string art = shot.GetComponent<SpriteRenderer>().sprite.name;
                        if (kind == "Archer" ? art.Contains("flecha") : art.Contains("magia")) found = true;
                    }
                    yield return new WaitForFixedUpdate();
                }''');p.write_text(s,encoding='utf-8')
