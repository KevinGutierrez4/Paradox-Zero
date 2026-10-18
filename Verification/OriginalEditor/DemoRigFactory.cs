#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneMoreTime.Editor
{
    /// <summary>Approximate cutout rigs from the original PNG pieces. Fine-tune pivots in the editor.</summary>
    internal static class DemoRigFactory
    {
        private const string Art = "Assets/OneMoreTime/Art/Originales/";
        internal static Material Material;
        internal static Sprite Solid;

        internal static Transform Create(GameObject actor, string kind)
        {
            Transform visual = Child(actor.transform, "Visual", Vector2.zero);
            visual.gameObject.AddComponent<SortingGroup>();
            Transform rig = Child(visual, "Rig", Vector2.zero);
            if (kind == "Player") Humanoid(rig, "02_guerrero_hacha", "torso_frente", "cabeza_perfil", "brazo_1", "brazo_2", "pierna_1", "pierna_2", true);
            else if (kind == "Archer") Humanoid(rig, "03_guerrero_arco", "torso", "cabeza_humana", "brazo_1", "brazo_2", "pierna_1", "pierna_2", false);
            else if (kind == "Mage") Humanoid(rig, "04_mago", "torso_frente", "cabeza_casco", "brazo_izquierdo_1", "brazo_derecho_1", "pierna_1", "pierna_2", false);
            else if (kind == "WhiteWolf" || kind == "BlueWolf") Wolf(rig, kind == "WhiteWolf");
            else Serpent(rig);

            Vector2 muzzle = kind == "Boss" ? new Vector2(1.25f, 2.4f) :
                kind.Contains("Wolf") ? new Vector2(1.2f, 1f) : new Vector2(0.95f, 1.05f);
            Child(rig, "Muzzle", muzzle);
            return visual;
        }

        private static void Humanoid(Transform rig, string folder, string torso, string head,
            string armBack, string armFront, string legBack, string legFront, bool player)
        {
            Part(rig, "BackArm", folder, armBack, new Vector2(-0.25f, 1.25f), new Vector2(0, -0.28f), 0.65f, 0);
            Part(rig, "BackLeg", folder, legBack, new Vector2(-0.18f, 0.62f), new Vector2(0, -0.3f), 0.66f, 1);
            Part(rig, "FrontLeg", folder, legFront, new Vector2(0.18f, 0.62f), new Vector2(0, -0.3f), 0.66f, 2);
            Part(rig, "Torso", folder, torso, new Vector2(0, 1.08f), Vector2.zero, 1.02f, 3);
            Part(rig, "Head", folder, head, new Vector2(0.06f, 1.75f), Vector2.zero, 0.58f, 4);
            var front = Part(rig, "FrontArm", folder, armFront, new Vector2(0.25f, 1.2f), new Vector2(0, -0.26f), 0.62f, 5);
            if (player)
            {
                // Code-native block weapon. The bald player's original body pieces are unchanged.
                var weapon = Child(front, "TemporalBlaster", new Vector2(0, -0.55f));
                var renderer = weapon.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = Solid; renderer.sharedMaterial = Material;
                renderer.color = new Color(0.15f, 0.22f, 0.3f); renderer.sortingOrder = 6;
                weapon.localScale = new Vector3(0.18f, 0.5f, 1);
            }
            else if (folder == "03_guerrero_arco")
                Part(rig, "WeaponArt", folder, "arco", new Vector2(0.65f, 1.05f), Vector2.zero, 1.05f, 6);
            else Part(rig, "WeaponArt", folder, "baston_1", new Vector2(0.65f, 0.9f), Vector2.zero, 1.6f, 6);
        }

        private static void Wolf(Transform rig, bool white)
        {
            string folder = white ? "01_lobo_blanco" : "05_lobo_azul";
            string frontUpper = white ? "pata_delantera_muslo" : "pata_delantera_sup_1";
            string frontLower = white ? "pata_delantera_pierna" : "pata_delantera_inf_1";
            string backUpper = white ? "pata_trasera_muslo" : "pata_trasera_sup_1";
            string backLower = white ? "pata_trasera_pierna" : "pata_trasera_inf_1";
            Part(rig, "BackLeg", folder, backUpper, new Vector2(-0.55f, 0.65f), new Vector2(0, -0.15f), 0.4f, 1);
            Part(rig.Find("BackLeg"), "Lower", folder, backLower, new Vector2(0, -0.32f), new Vector2(0, -0.12f), 0.4f, 1);
            Part(rig, "FrontLeg", folder, frontUpper, new Vector2(0.5f, 0.65f), new Vector2(0, -0.15f), 0.4f, 2);
            Part(rig.Find("FrontLeg"), "Lower", folder, frontLower, new Vector2(0, -0.32f), new Vector2(0, -0.12f), 0.4f, 2);
            Part(rig, "Torso", folder, "torso", new Vector2(-0.15f, 0.85f), Vector2.zero, 0.85f, 3);
            Part(rig, "Neck", folder, white ? "cuello_pecho" : "cuello", new Vector2(0.65f, 0.95f), Vector2.zero, 0.75f, 4);
            Part(rig, "Head", folder, white ? "cabeza" : "cabeza_1", new Vector2(0.95f, 1.2f), Vector2.zero, 0.65f, 5);
            Part(rig, "Tail", folder, "cola_base", new Vector2(-0.95f, 0.9f), Vector2.zero, 0.5f, 2);
            // Empty arm pivots keep the reusable animation paths consistent between rigs.
            Child(rig, "FrontArm", Vector2.zero); Child(rig, "BackArm", Vector2.zero);
        }

        private static void Serpent(Transform rig)
        {
            const string folder = "06_serpiente_azul";
            Part(rig, "Torso", folder, "segmento_curva_1", new Vector2(-0.3f, 1.1f), Vector2.zero, 2.1f, 2);
            Part(rig, "Coil", folder, "segmento_recto_1", new Vector2(-0.2f, 0.5f), Vector2.zero, 0.9f, 3);
            Part(rig, "Head", folder, "cabeza_neutra", new Vector2(0.55f, 2.45f), Vector2.zero, 1.8f, 5);
            Part(rig, "Tail", folder, "cola_aletas", new Vector2(-1.3f, 0.4f), Vector2.zero, 0.8f, 1);
            foreach (string name in new[] { "FrontLeg", "BackLeg", "FrontArm", "BackArm" }) Child(rig, name, Vector2.zero);
        }

        private static Transform Part(Transform parent, string name, string folder, string filename,
            Vector2 pivotPosition, Vector2 imageOffset, float height, int order)
        {
            Transform pivot = Child(parent, name, pivotPosition);
            Transform image = Child(pivot, "Sprite", imageOffset);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + folder + "/" + filename + ".png");
            if (sprite == null) throw new System.IO.FileNotFoundException("Falta una pieza: " + folder + "/" + filename);
            var renderer = image.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = Material; renderer.sortingOrder = order;
            image.localScale = Vector3.one * (height / sprite.bounds.size.y);
            return pivot;
        }

        internal static Transform Child(Transform parent, string name, Vector2 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); child.localPosition = position;
            return child;
        }
    }
}
#endif
