using UnityEngine;

namespace OneMoreTime
{
    /// <summary>The transform is the ground contact, independent of the artwork's sprite pivot.</summary>
    [ExecuteAlways]
    public sealed class BossScaleAnchor : MonoBehaviour
    {
        public Sprite sprite;
        [Min(0), Tooltip("Profundidad de la base bajo la nieve, en unidades de mundo.")]
        public float embedding = .08f;
        [HideInInspector] public SpriteRenderer preview;
        public Vector3 RenderPosition(Sprite art)
            => transform.TransformPoint(Vector3.up * (art != null ? -art.bounds.min.y : 0)) - Vector3.up * embedding;

        private void OnEnable() { UpdatePreview(); }
        private void LateUpdate() { if (!Application.isPlaying) UpdatePreview(); }
        private void OnValidate() { UpdatePreview(); }
        private void UpdatePreview()
        {
            if (preview == null) return;
            preview.enabled = !Application.isPlaying;
            if (Application.isPlaying) return;
            preview.sprite = sprite;
            preview.transform.position = RenderPosition(sprite);
        }
    }
}
