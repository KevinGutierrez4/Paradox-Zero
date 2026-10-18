from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoRigFactory.cs');s=p.read_text(encoding='utf-8-sig').replace('SpriteMeshType.FullRect','SpriteMeshType.Tight')
s=s.replace('var visited = new bool[pixels.Length]; var queue = new int[pixels.Length];','''var visited = new bool[pixels.Length]; var queue = new int[pixels.Length];
            bool Foreground(int p)
            {
                var c = pixels[p];
                // Cutouts include pale grid/crop-guide lines joining neighbouring pieces.
                return c.a >= 100 && !(c.r >= 225 && c.g >= 225 && c.b >= 225 && Math.Max(c.r, Math.Max(c.g, c.b)) - Math.Min(c.r, Math.Min(c.g, c.b)) < 8);
            }''')
s=s.replace('pixels[start].a < 100','!Foreground(start)').replace('pixels[p].a >= 100','Foreground(p)')
s=s.replace('            UnityEngine.Object.DestroyImmediate(readable); BoundsCache[path] = result; MaskCache[path] = main; return result;', '''            // Fill enclosed light regions, preserving the original white fur and highlights.
            var outside = new bool[pixels.Length]; int readOutside = 0, writeOutside = 0;
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (readOutside < writeOutside)
            {
                int p = queue[readOutside++], x = p % w, y = p / w;
                if (x > 0) Seed(p - 1); if (x < w - 1) Seed(p + 1); if (y > 0) Seed(p - w); if (y < h - 1) Seed(p + w);
            }
            void Seed(int p) { if (!main[p] && !outside[p]) { outside[p] = true; queue[writeOutside++] = p; } }
            for (int i = 0; i < main.Length; i++) if (!outside[i] && pixels[i].a >= 100) main[i] = true;
            UnityEngine.Object.DestroyImmediate(readable); BoundsCache[path] = result; MaskCache[path] = main; return result;''')
p.write_text(s,encoding='utf-8')
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig').replace('''                    foreach (var enemy in FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
                    {
                        Check''','''                    Place(game.PhaseEnd - 1.1f);
                    foreach (var enemy in FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
                    {
                        if (!enemy.IsAlive) continue;
                        Check''');p.write_text(s,encoding='utf-8')
p=Path('Assets/OneMoreTime/Editor/DemoBatch.cs');s=p.read_text(encoding='utf-8-sig').replace('''            try
            {''','''            int errors = 0;
            Application.LogCallback listener = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            Application.logMessageReceived += listener;
            try
            {''').replace('''                Debug.Log("OMT_BUILD_PASS");''','''                if (errors != 0) throw new InvalidOperationException("Unity logged " + errors + " errors while generating or rendering.");
                Debug.Log("OMT_BUILD_PASS");''').replace('''            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }''','''            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
            finally { Application.logMessageReceived -= listener; }''');p.write_text(s,encoding='utf-8')
