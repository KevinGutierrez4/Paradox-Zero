#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace OneMoreTime.Editor
{
    internal static class NeutralRigPreview
    {
        internal static void Capture(GameObject hero, Camera camera)
        {
            var previousTarget = camera.targetTexture; float previousSize = camera.orthographicSize;
            Vector3 previousPosition = camera.transform.position; Color previousColor = camera.backgroundColor;
            var target = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target; camera.orthographicSize = 1.15f;
            camera.transform.position = new Vector3(0, 0.95f, -10); camera.backgroundColor = new Color(0.14f, 0.17f, 0.22f, 1);
            var rig = hero.transform.Find("Visual/Rig"); var pose = hero.GetComponent<ArticulatedRig>();
            pose.EvaluatePose(0, 0, true, 0, 0, -1);
            Save(camera, target, "Neutral");
            var guides = new GameObject("JointGuides").transform; guides.SetParent(rig, false);
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[256]; for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                pixels[y * 16 + x] = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f)) < 6 ? new Color32(255, 255, 255, 255) : new Color32();
            texture.SetPixels32(pixels); texture.Apply(); var dot = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 250);
            foreach (string path in new[] { "Pelvis", "Pelvis/Torso/Head", "Pelvis/FrontLeg", "Pelvis/FrontLeg/Lower", "Pelvis/FrontLeg/Lower/Foot", "Pelvis/BackLeg", "Pelvis/BackLeg/Lower", "Pelvis/BackLeg/Lower/Foot", "Pelvis/Torso/FrontArm", "Pelvis/Torso/FrontArm/Lower", "Pelvis/Torso/FrontArm/Lower/Hand", "Pelvis/Torso/FrontArm/Lower/Hand/AxeSocket", "Pelvis/Torso/BackArm", "Pelvis/Torso/BackArm/Lower", "Pelvis/Torso/BackArm/Lower/Hand", "Pelvis/Torso/BackArm/Lower/Hand/ShieldSocket" })
            {
                var joint = rig.Find(path); var marker = new GameObject(path.Replace('/', '_')).transform; marker.SetParent(guides, false);
                marker.position = joint.position; var draw = marker.gameObject.AddComponent<SpriteRenderer>(); draw.sprite = dot;
                draw.sharedMaterial = DemoRigFactory.Material; draw.sortingOrder = 1000;
                draw.color = path.Contains("Hand") ? new Color(1, 0.7f, 0.2f) : path.Contains("Leg") ? new Color(0.2f, 1, 0.65f) : new Color(0.3f, 0.8f, 1);
            }
            Save(camera, target, "NeutralJoints");
            Object.DestroyImmediate(guides.gameObject); Object.DestroyImmediate(dot); Object.DestroyImmediate(texture);
            camera.targetTexture = previousTarget; camera.orthographicSize = previousSize; camera.transform.position = previousPosition; camera.backgroundColor = previousColor;
            target.Release(); Object.DestroyImmediate(target);
        }
        private static void Save(Camera camera, RenderTexture target, string name)
        {
            camera.Render(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1024, 1024, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); image.Apply();
            File.WriteAllBytes("Verification/ShieldHeroV4/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image); RenderTexture.active = previous;
        }
    }
}
#endif
