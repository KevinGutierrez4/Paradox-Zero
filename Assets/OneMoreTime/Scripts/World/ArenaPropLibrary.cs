using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>
    /// Sprites cut from the platform/trap atlases (Art/Environment) and the prefabs generated from them
    /// (Tools > One More Time > 9). Lives in Resources so the runtime stage builder needs no scene reference.
    /// </summary>
    [CreateAssetMenu(menuName = "One More Time/Arena Prop Library")]
    public sealed class ArenaPropLibrary : ScriptableObject
    {
        [Serializable] public sealed class SpriteEntry { public string id; public Sprite sprite; }
        [Serializable] public sealed class PrefabEntry { public string id; public GameObject prefab; }
        public const string ResourceName = "OMT_ArenaPropLibrary";

        public List<SpriteEntry> sprites = new List<SpriteEntry>();
        public List<PrefabEntry> prefabs = new List<PrefabEntry>();

        public Sprite Sprite(string id)
        {
            foreach (var e in sprites) if (e != null && e.id == id) return e.sprite;
            return null;
        }

        public GameObject Prefab(string id)
        {
            foreach (var e in prefabs) if (e != null && e.id == id) return e.prefab;
            return null;
        }

        public void SetPrefab(string id, GameObject prefab)
        {
            foreach (var e in prefabs) if (e != null && e.id == id) { e.prefab = prefab; return; }
            prefabs.Add(new PrefabEntry { id = id, prefab = prefab });
        }

        private static ArenaPropLibrary cached;
        public static ArenaPropLibrary Load()
        {
            if (cached == null) cached = Resources.Load<ArenaPropLibrary>(ResourceName);
            return cached;
        }
    }
}
