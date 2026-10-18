using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>Layer helpers for the scenery props (OMT_Prop collides with the player and projectiles, never with enemies).</summary>
    public static class PropLayers
    {
        public static int Prop => LayerMask.NameToLayer("OMT_Prop");
        public static int Ground => LayerMask.NameToLayer("OMT_Ground");
        public static int PropMask => Prop >= 0 ? 1 << Prop : 0;
    }

    /// <summary>
    /// Solid cover. Blocks the player's movement, enemy projectiles (arrows, magic) and — if <see cref="BlocksBreath"/> —
    /// the boss breath and frost wind. A breakable cover absorbs a limited amount of damage and then shatters.
    /// Purely decorative props have no ArenaCover and no collider.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ArenaCover : MonoBehaviour
    {
        [SerializeField] private bool blocksBreath = true;
        [SerializeField] private bool blocksProjectiles = true;
        [SerializeField] private bool breakable;
        [SerializeField, Min(1)] private float hitPoints = 30;
        private float damageTaken;
        private SpriteRenderer art;
        private static readonly List<ArenaCover> all = new List<ArenaCover>();
        public static IReadOnlyList<ArenaCover> All => all;
        public bool BlocksBreath => blocksBreath && !Broken;
        public bool BlocksProjectiles => blocksProjectiles && !Broken;
        public bool Breakable => breakable;
        public bool Broken { get; private set; }
        public float Remaining => Mathf.Max(0, hitPoints - damageTaken);
        public Bounds Bounds => GetComponent<BoxCollider2D>().bounds;

        public void Configure(bool breath, bool projectiles, bool canBreak, float points)
        { blocksBreath = breath; blocksProjectiles = projectiles; breakable = canBreak; hitPoints = Mathf.Max(1, points); }

        private void OnEnable() { if (!all.Contains(this)) all.Add(this); art = GetComponentInChildren<SpriteRenderer>(); }
        private void OnDisable() { all.Remove(this); }

        /// <summary>Damage from enemy projectiles or boss breath. Only breakable covers react.</summary>
        public void Absorb(float amount)
        {
            if (!breakable || Broken || amount <= 0) return;
            damageTaken += amount;
            if (art != null) art.color = Color.Lerp(Color.white, new Color(1f, 0.7f, 0.6f), damageTaken / hitPoints);
            var b = Bounds;
            PixelFx.Burst(b.center, 4, new Color(0.65f, 0.5f, 0.35f), 2.5f, 0.4f, 6, 0.07f, 31);
            if (damageTaken >= hitPoints) Break();
        }

        private void Break()
        {
            Broken = true;
            var b = Bounds;
            PixelFx.Burst(b.center, 18, new Color(0.6f, 0.45f, 0.3f), 5f, 0.8f, 9, 0.1f, 31);
            GetComponent<BoxCollider2D>().enabled = false;
            if (art != null) art.color = new Color(1, 1, 1, 0.25f);
        }

        public void Restore()
        {
            Broken = false; damageTaken = 0;
            GetComponent<BoxCollider2D>().enabled = true;
            if (art != null) art.color = Color.white;
        }
    }
}
