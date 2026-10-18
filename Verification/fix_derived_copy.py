from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoRigFactory.cs');s=p.read_text(encoding='utf-8-sig')
a=s.index('            var sprite = Sprite.Create(texture, rect, pivot');b=s.index('            AssetDatabase.CreateAsset(sprite, path);',a)
s=s[:a]+'''            // Clear only pixels outside the selected connected piece in this derived copy.
            // This removes crop-guide bridges and adjacent fragments without touching the PNG.
            var mask = MaskCache[source]; var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) if (!mask[i]) pixels[i].a = 0;
            texture.SetPixels32(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, rect, pivot, 100, 0, SpriteMeshType.FullRect);
            sprite.name = name;
'''+s[b:]
s=s.replace('''            // Editable derived Sprite assets own a pixel-identical texture copy. Unity refuses
            // OverrideGeometry on sprites backed by an imported Sprite texture in this version.''','''            // Derived assets own a texture copy so the supplied PNGs and their GUIDs stay intact.''')
p.write_text(s,encoding='utf-8')
