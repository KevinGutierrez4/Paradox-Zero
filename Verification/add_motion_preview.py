from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoVisualValidation.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('''                    UnityEngine.Object.DestroyImmediate(actor);''','''                    if (kind == "Player")
                    {
                        Directory.CreateDirectory("Verification/Motion");
                        for (int frame = 0; frame < 48; frame++)
                        {
                            bool walking = frame < 24; float t = walking ? frame / 24f : (frame - 24) / 23f;
                            rig.EvaluatePose(t * Mathf.PI * 2, walking ? 7.5f : 0, true, 0, 0, walking ? -1 : t);
                            camera.Render(); camera.Render();
                            var previous = RenderTexture.active; RenderTexture.active = target;
                            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
                            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                            File.WriteAllBytes("Verification/Motion/" + (walking ? "walk_" : "axe_") + (walking ? frame : frame - 24).ToString("00") + ".png", image.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = previous;
                        }
                    }
                    UnityEngine.Object.DestroyImmediate(actor);''')
p.write_text(s,encoding='utf-8')
