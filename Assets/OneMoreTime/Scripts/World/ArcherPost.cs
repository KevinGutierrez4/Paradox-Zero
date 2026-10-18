using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>Elevated firing position for an archer: a one-way platform reachable by jumping.</summary>
    public sealed class ArcherPost : MonoBehaviour
    {
        private static readonly List<ArcherPost> all = new List<ArcherPost>();
        public static IReadOnlyList<ArcherPost> All => all;
        public EnemyAI Occupant { get; set; }
        public bool Free => Occupant == null || !Occupant.IsAlive;
        public float TopY => GetComponent<Collider2D>().bounds.max.y;
        public float MinX => GetComponent<Collider2D>().bounds.min.x + 0.45f;
        public float MaxX => GetComponent<Collider2D>().bounds.max.x - 0.45f;
        public float CenterX => GetComponent<Collider2D>().bounds.center.x;
        private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() { all.Remove(this); }
    }
}
