from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig')
needle='''            yield return new WaitForSeconds(0.25f);
            float initialY'''
insert='''            body.constraints = RigidbodyConstraints2D.FreezeAll;
            string directionScene = EditorBuildSettings.scenes[0].path;
            string directionRoot = directionScene.Substring(0, directionScene.LastIndexOf("/Scenes/", StringComparison.Ordinal));
            foreach (string kind in new[] { "Player", "Archer", "Mage", "WhiteWolf", "BlueWolf", "Boss" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(directionRoot + "/Prefabs/" + kind + ".prefab");
                var instance = Instantiate(prefab, new Vector3(1000, 0, 0), Quaternion.identity);
                instance.GetComponent<Rigidbody2D>().simulated = false;
                var input = instance.GetComponent<PlayerController>(); if (input != null) input.enabled = false;
                var face = instance.GetComponent<AnimationDriver>();
                var head = instance.transform.Find(kind == "Player" || kind == "Archer" || kind == "Mage" ? "Visual/Rig/Pelvis/Torso/Head" : "Visual/Rig/Neck/Head");
                var nose = head.Find("FacingPoint");
                foreach (int parentSign in new[] { 1, -1 })
                {
                    instance.transform.localScale = new Vector3(parentSign, 1, 1);
                    foreach (int sign in new[] { 1, -1 })
                    {
                        face.SetMotion(sign * 3, true, false, 0);
                        yield return null; yield return null;
                        Check(face.Facing == sign && (nose.position.x - head.position.x) * sign > 0.01f,
                            kind + " drawing faces movement " + sign + " with parent scale " + parentSign + " after Animator evaluation");
                    }
                }
                if (kind == "Player" || kind == "Archer" || kind == "Mage")
                {
                    var rig = instance.GetComponent<ArticulatedRig>();
                    instance.transform.localScale = Vector3.one; face.Face(1);
                    var skeleton = instance.transform.Find("Visual/Rig");
                    var knee = skeleton.Find("Pelvis/FrontLeg/Lower");
                    rig.EvaluatePose(Mathf.PI / 2, 7.5f, true, 0, 0, -1);
                    float stanceY = skeleton.InverseTransformPoint(knee.TransformPoint(new Vector3(0, -0.44f, 0))).y;
                    rig.EvaluatePose(Mathf.PI * 1.5f, 7.5f, true, 0, 0, -1);
                    float swingY = skeleton.InverseTransformPoint(knee.TransformPoint(new Vector3(0, -0.44f, 0))).y;
                    Check(stanceY < 0.10f && swingY > stanceY + 0.1f, kind + " plants the backwards-moving foot and lifts the forwards return");
                    foreach (string armName in new[] { "FrontArm", "BackArm" })
                    {
                        var shoulder = skeleton.Find("Pelvis/Torso/" + armName);
                        foreach (var joint in new[] { shoulder, shoulder.Find("Lower") })
                        {
                            var image = joint.Find("Sprite");
                            Check(image != null && Vector2.Distance(image.localPosition, Vector2.zero) < 0.001f && Mathf.Abs(Mathf.DeltaAngle(0, image.localEulerAngles.z)) > 1,
                                kind + " " + armName + " " + joint.name + " artwork pivots at the joint with original inclination corrected");
                        }
                    }
                }
                Destroy(instance); yield return null;
            }
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            player.GetComponent<AnimationDriver>().Face(1);
            yield return new WaitForSeconds(0.25f);
            float initialY'''
assert needle in s;s=s.replace(needle,insert)
p.write_text(s,encoding='utf-8')
