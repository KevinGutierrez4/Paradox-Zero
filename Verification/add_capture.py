from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig')
pos=s.index('        private IEnumerator Tests()')
s=s[:pos]+'''        private void CaptureFrame(string name)
        {
            var camera = Camera.main; var previousTarget = camera.targetTexture;
            var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
            camera.Render(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes("Verification/" + name + ".png", image.EncodeToPNG());
            Destroy(image); RenderTexture.active = previous; camera.targetTexture = previousTarget;
            target.Release(); Destroy(target);
        }
'''+s[pos:]
s=s.replace('''            yield return new WaitForSeconds(0.6f);
            Check(a.Current''','''            yield return new WaitForSeconds(0.18f); CaptureFrame("axe-play-mode");
            yield return new WaitForSeconds(0.42f);
            Check(a.Current''')
s=s.replace('''            yield return new WaitForSeconds(3);
            Check(FindObjectsByType<Projectile>''','''            yield return new WaitForSeconds(3); CaptureFrame("boss-play-mode");
            Check(FindObjectsByType<Projectile>''')
p.write_text(s,encoding='utf-8')
