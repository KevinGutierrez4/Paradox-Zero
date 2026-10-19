using UnityEngine;

namespace OneMoreTime
{
    /// <summary>Collision changes are scoped to this package's named layers.</summary>
    public sealed class CollisionRules2D : MonoBehaviour
    {
        private void Awake()
        {
            int ground = LayerMask.NameToLayer("OMT_Ground"), player = LayerMask.NameToLayer("OMT_Player");
            int enemy = LayerMask.NameToLayer("OMT_Enemy"), projectile = LayerMask.NameToLayer("OMT_Projectile");
            if (ground < 0 || player < 0 || enemy < 0 || projectile < 0) return;
            Physics2D.IgnoreLayerCollision(enemy, enemy, true);
            Physics2D.IgnoreLayerCollision(projectile, projectile, true);
            Physics2D.IgnoreLayerCollision(projectile, ground, false);
            Physics2D.IgnoreLayerCollision(projectile, player, false);
            Physics2D.IgnoreLayerCollision(projectile, enemy, false);
            Physics2D.IgnoreLayerCollision(player, ground, false);
            Physics2D.IgnoreLayerCollision(enemy, ground, false);
            // Bodies never touch physically: the player cannot stand on, carry or be pushed by enemies.
            // Damage uses explicit overlap queries (StrikeAt/OverlapBox/bounds), so hits keep working.
            Physics2D.IgnoreLayerCollision(player, enemy, true);
            // Scenery props (covers, barricades): solid for the player and projectiles; enemies walk past them.
            int prop = LayerMask.NameToLayer("OMT_Prop");
            if (prop >= 0)
            {
                Physics2D.IgnoreLayerCollision(prop, enemy, true);
                Physics2D.IgnoreLayerCollision(prop, player, false);
                Physics2D.IgnoreLayerCollision(prop, projectile, false);
                Physics2D.IgnoreLayerCollision(prop, ground, true);
                Physics2D.IgnoreLayerCollision(prop, prop, true);
            }
        }
    }
}
