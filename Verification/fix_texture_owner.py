from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoRigFactory.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('''            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(source);
            if (texture == null) throw new FileNotFoundException(source);''','''            if (!File.Exists(source)) throw new FileNotFoundException(source);
            // Editable derived Sprite assets own a pixel-identical texture copy. Unity refuses
            // OverrideGeometry on sprites backed by an imported Sprite texture in this version.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = file, filterMode = FilterMode.Point };
            texture.LoadImage(File.ReadAllBytes(source));''')
s=s.replace('''            AssetDatabase.CreateAsset(sprite, path); return sprite;''','''            AssetDatabase.CreateAsset(sprite, path);
            AssetDatabase.AddObjectToAsset(texture, sprite);
            EditorUtility.SetDirty(sprite); EditorUtility.SetDirty(texture);
            return sprite;''')
p.write_text(s,encoding='utf-8')
