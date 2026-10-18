using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;
        [SerializeField] private float height = 3.2f;
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.45f;
        private Transform target;
        private Camera sceneCamera;
        private float startX, endX, velocity;
        private bool bounded;
        private float velocityY;
        private float baseSize, desiredSize, sizeVelocity;
        private bool overrideActive;
        private Vector2 overrideFocus;
        private float overrideSmooth = 0.6f;
        private float shakeAmplitude, shakeUntil, shakeDuration;
        private Vector3 lastShake;
        private float floorY = float.NegativeInfinity, ceilingY = float.PositiveInfinity;

        public bool HasOverride => overrideActive;

        private void Awake()
        {
            sceneCamera = GetComponent<Camera>();
            baseSize = desiredSize = sceneCamera.orthographicSize;
        }
        public void Initialize(Transform player) { target = player; }

        public void SetBounds(float start, float end, bool snap)
        {
            startX = start; endX = end; bounded = true; velocity = 0;
            velocityY = 0;
            if (snap && target != null)
            {
                RemoveShake();
                transform.position = new Vector3(ClampX(target.position.x + 2f), ClampY(target.position.y + height), -10);
            }
        }

        /// <summary>Lowest world Y the view may show (bottom of the painted scenery), so zooming out never reveals empty space.</summary>
        public void SetVerticalFloor(float worldY) { floorY = worldY; }

        /// <summary>Bottom and top of the painted scenery: the view stays inside and the zoom never exceeds its height.</summary>
        public void SetVerticalRange(float bottom, float top) { floorY = bottom; ceilingY = Mathf.Max(bottom + 2f, top); }

        private float MaxSize => float.IsInfinity(ceilingY) || float.IsInfinity(floorY) ? float.PositiveInfinity : (ceilingY - floorY) * 0.5f;

        private float ClampY(float y)
        {
            if (!float.IsInfinity(ceilingY)) y = Mathf.Min(y, ceilingY - sceneCamera.orthographicSize);
            if (!float.IsInfinity(floorY)) y = Mathf.Max(y, floorY + sceneCamera.orthographicSize);
            return y;
        }

        /// <summary>Stops following the player and frames a point (cinematics). Bounds are still respected.</summary>
        public void SetFocusOverride(Vector2 focus, float orthographicSize, float smooth = 0.6f)
        {
            overrideActive = true; overrideFocus = focus; overrideSmooth = Mathf.Max(0.05f, smooth);
            desiredSize = Mathf.Max(1f, orthographicSize);
        }

        public void ClearFocusOverride() { overrideActive = false; }

        /// <summary>Orthographic size used while following (e.g. wider framing for the boss arena).</summary>
        public void SetCombatSize(float orthographicSize) { if (!overrideActive) desiredSize = Mathf.Max(1f, orthographicSize); }
        public void ResetFraming()
        {
            overrideActive = false; desiredSize = baseSize; shakeUntil = 0;
            if (sceneCamera != null) { sceneCamera.orthographicSize = baseSize; sizeVelocity = 0; }
        }

        /// <summary>Screen shake in world units. Uses gameplay time, so a pause also freezes it.</summary>
        public void Shake(float amplitude, float duration)
        {
            if (Time.time < shakeUntil && amplitude < shakeAmplitude * Mathf.Clamp01((shakeUntil - Time.time) / shakeDuration)) return;
            shakeAmplitude = amplitude; shakeDuration = Mathf.Max(0.02f, duration); shakeUntil = Time.time + shakeDuration;
        }

        private float HalfWidth(float size) => size * sceneCamera.aspect;

        private float ClampX(float x)
        {
            if (!bounded) return x;
            float half = HalfWidth(sceneCamera.orthographicSize);
            if (endX - startX < half * 2) return (startX + endX) * 0.5f;
            return Mathf.Clamp(x, startX + half, endX - half);
        }

        private void RemoveShake() { transform.position -= lastShake; lastShake = Vector3.zero; }

        private void LateUpdate()
        {
            if (target == null) return;
            RemoveShake();
            if (Time.deltaTime <= 0) return; // Paused: hold the frame exactly where it is.
            sceneCamera.orthographicSize = Mathf.SmoothDamp(sceneCamera.orthographicSize, Mathf.Min(desiredSize, MaxSize), ref sizeVelocity, zoomSmoothTime);
            Vector2 goal = overrideActive ? overrideFocus : new Vector2(target.position.x + 2f, target.position.y + height);
            float smooth = overrideActive ? overrideSmooth : smoothTime;
            float x = Mathf.SmoothDamp(transform.position.x, ClampX(goal.x), ref velocity, smooth);
            float y = Mathf.SmoothDamp(transform.position.y, goal.y, ref velocityY, overrideActive ? smooth : smoothTime * 1.8f);
            transform.position = new Vector3(ClampX(x), ClampY(y), -10);
            if (Time.time < shakeUntil)
            {
                float k = Mathf.Clamp01((shakeUntil - Time.time) / shakeDuration);
                float a = shakeAmplitude * k * k;
                lastShake = new Vector3((Mathf.PerlinNoise(Time.time * 31f, 0.3f) - 0.5f) * 2f * a, (Mathf.PerlinNoise(0.7f, Time.time * 29f) - 0.5f) * 2f * a, 0);
                transform.position += lastShake;
            }
        }
    }
}
