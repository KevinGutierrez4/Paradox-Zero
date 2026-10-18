from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig');needle='            var visual = player.transform.Find("Visual");'
s=s.replace(needle,'''            driver.Face(1);
            var buffered = Dummy("BufferedCombo", new Vector2(0.8f, 1)); Physics2D.SyncTransforms();
            melee.RequestAttack(12, 1.6f); yield return new WaitForSeconds(0.51f); melee.RequestAttack(12, 1.6f);
            yield return new WaitForSeconds(0.63f);
            Check(buffered.Current == 76, "Late input buffer chains exactly one further swing");
            melee.Cancel(); Destroy(buffered.gameObject); yield return null;
'''+needle)
p.write_text(s,encoding='utf-8')
