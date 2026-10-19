using UnityEngine;

namespace OneMoreTime
{
    /// <summary>Authored placement of the boss and its visible, solid mound in the phase prefab.</summary>
    public sealed class BossArenaLayout : MonoBehaviour
    {
        [Tooltip("Mueve esta base en Scene para colocar al jefe. Sus escamas se editan por separado.")]
        public Transform neckBase;
        [Tooltip("Colisión del montículo visible. Su contorno se puede editar en el prefab de la fase 6.")]
        public Collider2D moundCollider;
        [Tooltip("Pared vertical delante del montículo: impide subir al cuerpo del jefe incluso saltando o esquivando. Se edita en Scene.")]
        public BoxCollider2D noClimbBoundary;
        public float FrontX => noClimbBoundary != null ? noClimbBoundary.bounds.min.x : moundCollider.bounds.min.x;
        public BossScaleAnchor[] Scales => GetComponentsInChildren<BossScaleAnchor>();

        private void OnDrawGizmos()
        {
            if (noClimbBoundary == null) return;
            Gizmos.color = new Color(1, .45f, .1f);
            var bounds = noClimbBoundary.bounds;
            Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.min.y, 0), new Vector3(bounds.min.x, bounds.max.y, 0));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (neckBase != null)
            {
                Gizmos.DrawWireSphere(neckBase.position, .25f);
                Gizmos.DrawLine(neckBase.position, neckBase.position + Vector3.up * 4);
            }
            if (moundCollider != null) Gizmos.DrawWireCube(moundCollider.bounds.center, moundCollider.bounds.size);
        }
    }
}
