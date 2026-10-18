using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ParallaxBackground : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField, Range(0f, 1f)] private float parallax = 0.1f;
        [SerializeField, Min(0f)] private float extraWidth = 8f;
        private SpriteRenderer sprite;
        private float initialCameraX, halfWidth;
        private float lastAspect, lastSize;

        private void Awake() { sprite = GetComponent<SpriteRenderer>(); }

        public void SetSprite(Sprite background)
        {
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();
            sprite.sprite = background;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || background == null) return;
            initialCameraX = targetCamera.transform.position.x;
            Fit();
        }

        private void Fit()
        {
            float viewHeight = targetCamera.orthographicSize * 2f;
            float viewWidth = viewHeight * targetCamera.aspect;
            Vector2 size = sprite.sprite.bounds.size;
            float scale = Mathf.Max(viewHeight / size.y, (viewWidth + extraWidth) / size.x);
            transform.localScale = Vector3.one * scale;
            halfWidth = size.x * scale * 0.5f;
            lastAspect = targetCamera.aspect; lastSize = targetCamera.orthographicSize;
        }

        private void LateUpdate()
        {
            if (targetCamera == null || sprite == null || sprite.sprite == null) return;
            if (!Mathf.Approximately(lastAspect, targetCamera.aspect) || !Mathf.Approximately(lastSize, targetCamera.orthographicSize)) Fit();
            float cameraX = targetCamera.transform.position.x;
            float margin = Mathf.Max(0, halfWidth - targetCamera.orthographicSize * targetCamera.aspect);
            float drift = Mathf.Clamp((cameraX - initialCameraX) * parallax, -margin, margin);
            transform.position = new Vector3(cameraX - drift, targetCamera.transform.position.y, 8);
        }
    }
}
