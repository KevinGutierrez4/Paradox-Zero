using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;
        [SerializeField] private float height = 3.2f;
        private Transform target;
        private Camera sceneCamera;
        private float startX, endX, velocity;
        private bool bounded;
        private float velocityY;

        private void Awake() { sceneCamera = GetComponent<Camera>(); }
        public void Initialize(Transform player) { target = player; }

        public void SetBounds(float start, float end, bool snap)
        {
            startX = start; endX = end; bounded = true; velocity = 0;
            velocityY = 0;
            if (snap && target != null) transform.position = new Vector3(DesiredX(), target.position.y + height, -10);
        }

        private float DesiredX()
        {
            float half = sceneCamera.orthographicSize * sceneCamera.aspect;
            if (!bounded) return target.position.x;
            if (endX - startX < half * 2) return (startX + endX) * 0.5f;
            return Mathf.Clamp(target.position.x + 2f, startX + half, endX - half);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float x = Mathf.SmoothDamp(transform.position.x, DesiredX(), ref velocity, smoothTime);
            float y = Mathf.SmoothDamp(transform.position.y, target.position.y + height, ref velocityY, smoothTime * 1.8f);
            transform.position = new Vector3(x, y, -10);
        }
    }
}
