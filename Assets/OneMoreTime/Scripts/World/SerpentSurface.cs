using System;
using UnityEngine;

namespace OneMoreTime
{
    [Serializable]
    public sealed class SurfaceProfile
    {
        public Sprite image;
        [Tooltip("Coordenadas normalizadas de la imagen: (0,0) abajo izquierda. Ignora las espinas decorativas.")]
        public Vector2[] points;
    }

    [RequireComponent(typeof(SpriteRenderer), typeof(EdgeCollider2D))]
    public sealed class SerpentSurface : MonoBehaviour
    {
        [SerializeField] private SurfaceProfile[] phaseProfiles;
        [SerializeField] private int previewPhase = 1;
        private Vector2[] worldPoints;
        public float MinimumY { get; private set; }
        public float MaximumY { get; private set; }

        public void Configure(int phase, float start, float end)
        {
            if (phaseProfiles == null || phaseProfiles.Length == 0) throw new InvalidOperationException("Faltan perfiles de la espalda.");
            var profile = phaseProfiles[Mathf.Clamp(phase - 1, 0, phaseProfiles.Length - 1)];
            if (profile.image == null || profile.points == null || profile.points.Length < 2) throw new InvalidOperationException("Perfil de superficie incompleto.");
            previewPhase = phase;
            var renderer = GetComponent<SpriteRenderer>(); renderer.sprite = profile.image;
            float width = end - start + 6f;
            float height = width * profile.image.rect.height / profile.image.rect.width;
            // Image and edge share one transform. Camera movement cannot offset their alignment.
            transform.position = new Vector3(start - 3 + width * 0.5f, height * 0.23f, 0);
            transform.localScale = Vector3.one * (width / profile.image.bounds.size.x);
            var local = new Vector2[profile.points.Length]; worldPoints = new Vector2[local.Length];
            MinimumY = float.PositiveInfinity; MaximumY = float.NegativeInfinity;
            for (int i = 0; i < local.Length; i++)
            {
                local[i] = Vector2.Scale(profile.points[i] - Vector2.one * 0.5f, profile.image.bounds.size);
                worldPoints[i] = transform.TransformPoint(local[i]);
                MinimumY = Mathf.Min(MinimumY, worldPoints[i].y); MaximumY = Mathf.Max(MaximumY, worldPoints[i].y);
            }
            var edge = GetComponent<EdgeCollider2D>(); edge.points = local; edge.edgeRadius = 0.006f;
            Physics2D.SyncTransforms();
        }

        public float HeightAt(float x)
        {
            if (worldPoints == null || worldPoints.Length < 2) return 0;
            if (x <= worldPoints[0].x) return worldPoints[0].y;
            for (int i = 1; i < worldPoints.Length; i++)
                if (x <= worldPoints[i].x)
                    return Mathf.Lerp(worldPoints[i - 1].y, worldPoints[i].y, Mathf.InverseLerp(worldPoints[i - 1].x, worldPoints[i].x, x));
            return worldPoints[worldPoints.Length - 1].y;
        }

        private void OnDrawGizmosSelected()
        {
            var edge = GetComponent<EdgeCollider2D>(); if (edge == null) return;
            var points = edge.points; Gizmos.color = Color.green;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = transform.TransformPoint(points[i]); Gizmos.DrawSphere(p, 0.08f);
                if (i > 0) Gizmos.DrawLine(transform.TransformPoint(points[i - 1]), p);
            }
        }
    }
}
