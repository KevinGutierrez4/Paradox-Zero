from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoVisualValidation.cs');s=p.read_text(encoding='utf-8-sig');needle='''                    if (kind == "Player")
                    {'''
insert='''                    Directory.CreateDirectory("Verification/Directions");
                    foreach (int direction in new[] { -1, 1 })
                    {
                        visual.localScale = new Vector3(direction, 1, 1);
                        foreach (bool attacking in new[] { false, true })
                        {
                            rig.EvaluatePose(0, attacking ? 0 : 7.5f, true, 0, 0, attacking ? 0.43f : -1);
                            camera.Render(); camera.Render();
                            var previous = RenderTexture.active; RenderTexture.active = target;
                            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
                            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                            File.WriteAllBytes("Verification/Directions/" + kind + "_" + (direction < 0 ? "Left" : "Right") + "_" + (attacking ? "Attack" : "Walk") + ".png", image.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = previous;
                        }
                    }
                    visual.localScale = Vector3.one;
                    if (kind == "Player")
                    {'''
assert needle in s;s=s.replace(needle,insert);p.write_text(s,encoding='utf-8')
