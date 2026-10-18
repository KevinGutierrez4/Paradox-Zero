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
            Physics2D.IgnoreLayerCollision(player, enemy, false);
        }
    }
}
