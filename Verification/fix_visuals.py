from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoRigFactory.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('private static readonly Dictionary<string, Rect> BoundsCache', 'private static readonly Dictionary<string, bool[]> MaskCache = new Dictionary<string, bool[]>();\n        private static readonly Dictionary<string, Rect> BoundsCache')
s=s.replace('new Vector2(0.03f, 0.83f)','new Vector2(0.03f, 0.76f)').replace('player ? new Vector2(0.08f, 0.04f)','player ? new Vector2(0.18f, 0.04f)')
s=s.replace('sprite.name = name; AssetDatabase.CreateAsset(sprite, path); return sprite;','''sprite.name = name;
            // Restrict the mesh to the main connected piece. Small neighbouring fragments in
            // the supplied cutouts stay outside the mesh; the original texture remains untouched.
            var vertices = new List<Vector2>(); var triangles = new List<ushort>();
            var mask = MaskCache[source]; int width = texture.width;
            int y0 = Mathf.CeilToInt(rect.yMin), y1 = Mathf.FloorToInt(rect.yMax);
            int x0 = Mathf.CeilToInt(rect.xMin), x1 = Mathf.FloorToInt(rect.xMax);
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    if (!mask[y * width + x]) continue;
                    int start = x; while (x + 1 < x1 && mask[y * width + x + 1]) x++;
                    float left = start - rect.x - pivot.x * rect.width, right = x + 1 - rect.x - pivot.x * rect.width;
                    float bottom = y - rect.y - pivot.y * rect.height, top = bottom + 1;
                    ushort v = (ushort)vertices.Count;
                    vertices.Add(new Vector2(left, bottom) / 100); vertices.Add(new Vector2(left, top) / 100);
                    vertices.Add(new Vector2(right, top) / 100); vertices.Add(new Vector2(right, bottom) / 100);
                    triangles.Add(v); triangles.Add((ushort)(v + 1)); triangles.Add((ushort)(v + 2));
                    triangles.Add(v); triangles.Add((ushort)(v + 2)); triangles.Add((ushort)(v + 3));
                }
            }
            if (vertices.Count > 0 && vertices.Count < ushort.MaxValue) sprite.OverrideGeometry(vertices.ToArray(), triangles.ToArray());
            AssetDatabase.CreateAsset(sprite, path); return sprite;''')
s=s.replace('int largest = 0; Rect result', 'int largest = 0; bool[] main = null; Rect result')
s=s.replace('if (write > largest) { largest = write; result = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1); }', '''if (write > largest)
                {
                    largest = write; result = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                    main = new bool[pixels.Length]; for (int i = 0; i < write; i++) main[queue[i]] = true;
                }''')
s=s.replace('BoundsCache[path] = result; return result;', 'BoundsCache[path] = result; MaskCache[path] = main; return result;')
p.write_text(s,encoding='utf-8')
p=Path('Assets/OneMoreTime/Editor/DemoVisualValidation.cs');s=p.read_text(encoding='utf-8-sig').replace('                        camera.Render();','                        camera.Render(); camera.Render();');p.write_text(s,encoding='utf-8')
# Make batch build run visual QA too after generating fresh sprites.
p=Path('Assets/OneMoreTime/Editor/DemoBatch.cs');s=p.read_text(encoding='utf-8-sig').replace('DemoValidation.ValidateAll();','DemoValidation.ValidateAll();\n                DemoVisualValidation.RenderPoses();');p.write_text(s,encoding='utf-8')
