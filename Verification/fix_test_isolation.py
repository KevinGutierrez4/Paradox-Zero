from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig').replace('''            var driver = player.GetComponent<AnimationDriver>(); driver.Face(1);''','''            // Isolate hit tests from downhill sliding while the input controller is disabled.
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            var driver = player.GetComponent<AnimationDriver>(); driver.Face(1);''')
p.write_text(s,encoding='utf-8')
