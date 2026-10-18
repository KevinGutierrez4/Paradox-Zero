using UnityEngine;

namespace OneMoreTime
{
    /// <summary>
    /// Moving one-way platform. Supports both the speed-and-pause configuration from Flores
    /// and the timed oscillation used by older dev prefabs.
    /// Only this platform moves; the serpent and the scenery are not moved.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Travel offset from the starting position, in world units.")]
        [SerializeField] private Vector2 travel = new Vector2(2.5f, 0f);
        [Tooltip("Speed in world units per second (Flores mode).")]
        [SerializeField, Min(0.1f)] private float speed = 1.1f;
        [Tooltip("Pause at each end of the path, in seconds (Flores mode).")]
        [SerializeField, Min(0f)] private float endPause = 0.6f;

        // The dev prefab serializes 'period: 4'. The Flores prefab has no 'period'
        // field. A negative default lets each existing prefab retain its original
        // movement model without modifying its .prefab or .meta file.
        [SerializeField, HideInInspector] private float period = -1f;

        private Rigidbody2D rb;
        private Vector2 origin;
        private float progress;
        private float pauseTimer;
        private float phase;
        private int direction = 1;
        private bool started;
        private PlayerController player;
        private Collider2D surface;

        public Vector2 Travel => travel;
        public float Speed => speed;
        public float EndPause => endPause;
        public Vector2 Origin => origin;
        public bool Paused => period <= 0f && pauseTimer > 0f;
        public Collider2D Surface => surface;
        public Vector2 LastDelta { get; private set; }

        /// <summary>
        /// Legacy dev API: the second argument is the duration of a full cycle,
        /// in seconds (out and back, without a pause).
        /// </summary>
        public void Configure(Vector2 offset, float cycleSeconds)
        {
            travel = offset;
            period = Mathf.Max(0.2f, cycleSeconds);
            ResetMovement();
        }

        /// <summary>
        /// Flores API: speed in units per second and an explicit end pause.
        /// Calls with three arguments retain their original meaning.
        /// </summary>
        public void Configure(Vector2 offset, float unitsPerSecond, float pause)
        {
            travel = offset;
            speed = Mathf.Max(0.1f, unitsPerSecond);
            endPause = Mathf.Max(0f, pause);
            period = -1f;
            ResetMovement();
        }

        /// <summary>Use the Flores behavior, with an optional end pause.</summary>
        public void ConfigureSpeed(Vector2 offset, float unitsPerSecond, float pause = 0.6f)
        {
            Configure(offset, unitsPerSecond, pause);
        }

        /// <summary>
        /// Flores compatibility API: convert a desired total cycle duration
        /// into speed while retaining a pause at each end.
        /// </summary>
        public void ConfigureCycle(Vector2 offset, float cycleSeconds)
        {
            float movingSeconds = Mathf.Max(0.2f, cycleSeconds - 2f * endPause);
            float unitsPerSecond = Mathf.Max(0.1f, 2f * offset.magnitude / movingSeconds);
            Configure(offset, unitsPerSecond, endPause);
        }

        private void ResetMovement()
        {
            progress = 0f;
            phase = 0f;
            direction = 1;
            pauseTimer = 0f;
            LastDelta = Vector2.zero;
            if (started && rb != null)
                origin = rb.position;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.useFullKinematicContacts = false;
            surface = GetComponent<Collider2D>();
        }

        private void Start()
        {
            origin = rb.position;
            started = true;
            player = FindAnyObjectByType<PlayerController>();
        }

        private void FixedUpdate()
        {
            if (!started) return;
            float distance = travel.magnitude;
            if (distance < 0.001f)
            {
                LastDelta = Vector2.zero;
                return;
            }

            Vector2 before = rb.position;
            float fraction;

            if (period > 0f)
            {
                // dev: sinusoidal full cycle (there and back).
                phase += Time.fixedDeltaTime / period;
                fraction = 0.5f - 0.5f * Mathf.Cos(phase * 2f * Mathf.PI);
            }
            else
            {
                // Flores: speed controlled movement, smoothly easing at the ends.
                if (pauseTimer > 0f)
                    pauseTimer -= Time.fixedDeltaTime;
                else
                {
                    progress += direction * speed * Time.fixedDeltaTime / distance;
                    if (progress >= 1f)
                    {
                        progress = 1f;
                        direction = -1;
                        pauseTimer = endPause;
                    }
                    else if (progress <= 0f)
                    {
                        progress = 0f;
                        direction = 1;
                        pauseTimer = endPause;
                    }
                }
                fraction = Mathf.SmoothStep(0f, 1f, progress);
            }

            Vector2 next = origin + travel * fraction;
            LastDelta = next - before;
            rb.MovePosition(next);

            if (player != null && surface != null &&
                player.GroundCollider == surface && LastDelta.sqrMagnitude > 0f)
            {
                var playerBody = player.GetComponent<Rigidbody2D>();
                if (playerBody != null)
                    playerBody.position += LastDelta;
            }
        }

        /// <summary>World-space bounds at a particular travel fraction, for placement checks.</summary>
        public Bounds BoundsAt(float fraction)
        {
            if (surface == null) surface = GetComponent<Collider2D>();
            if (surface == null) return new Bounds(transform.position, Vector3.zero);
            var result = surface.bounds;
            Vector2 offset = origin + travel * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fraction)) -
                             (rb != null ? rb.position : (Vector2)transform.position);
            result.center += (Vector3)offset;
            return result;
        }
    }
}
