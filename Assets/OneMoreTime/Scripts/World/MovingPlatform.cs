using UnityEngine;

namespace OneMoreTime
{
    /// <summary>
    /// Kinematic one-way platform that travels back and forth along a local offset, pausing briefly at each end.
    /// The player standing on it is carried by the platform's own displacement each physics step (no re-parenting),
    /// so the existing controller keeps full authority over input, jumping and collisions.
    /// The motion is local to the platform: nothing else (and never the serpent's body) is moved.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Recorrido desde el punto inicial (unidades de mundo).")]
        [SerializeField] private Vector2 travel = new Vector2(2.5f, 0);
        [Tooltip("Velocidad de desplazamiento (unidades por segundo).")]
        [SerializeField, Min(0.1f)] private float speed = 1.1f;
        [Tooltip("Pausa en cada extremo (segundos).")]
        [SerializeField, Min(0f)] private float endPause = 0.6f;
        private Rigidbody2D rb;
        private Vector2 origin;
        private float progress, pauseTimer;
        private int direction = 1;
        private bool started;
        private PlayerController player;
        private Collider2D surface;

        public Vector2 Travel => travel;
        public float Speed => speed;
        public float EndPause => endPause;
        public Vector2 Origin => origin;
        public bool Paused => pauseTimer > 0;
        public Collider2D Surface => surface;
        /// <summary>Displacement applied during the last physics step (QA: carried player moves by the same amount).</summary>
        public Vector2 LastDelta { get; private set; }

        public void Configure(Vector2 offset, float unitsPerSecond, float pause = 0.6f)
        { travel = offset; speed = Mathf.Max(0.1f, unitsPerSecond); endPause = Mathf.Max(0, pause); }

        /// <summary>Back-compatible overload: whole back-and-forth cycle duration in seconds.</summary>
        public void ConfigureCycle(Vector2 offset, float cycleSeconds) { Configure(offset, Mathf.Max(0.1f, 2f * offset.magnitude / Mathf.Max(0.2f, cycleSeconds - 2 * endPause)), endPause); }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.useFullKinematicContacts = false;
            surface = GetComponent<Collider2D>();
        }

        private void Start() { origin = rb.position; started = true; player = FindAnyObjectByType<PlayerController>(); }

        private void FixedUpdate()
        {
            if (!started) return;
            float length = travel.magnitude;
            if (length < 0.001f) return;
            Vector2 before = rb.position;
            if (pauseTimer > 0) pauseTimer -= Time.fixedDeltaTime;
            else
            {
                progress += direction * speed * Time.fixedDeltaTime / length;
                if (progress >= 1f) { progress = 1f; direction = -1; pauseTimer = endPause; }
                else if (progress <= 0f) { progress = 0f; direction = 1; pauseTimer = endPause; }
            }
            // Ease near the ends so the stop and the restart never jerk the rider.
            float eased = Mathf.SmoothStep(0, 1, progress);
            Vector2 next = origin + travel * eased;
            LastDelta = next - before;
            rb.MovePosition(next);
            if (player != null && surface != null && player.GroundCollider == surface && LastDelta.sqrMagnitude > 0)
            {
                var pb = player.GetComponent<Rigidbody2D>();
                pb.position += LastDelta;
            }
        }

        /// <summary>World-space bounds of the platform collider at a given travel fraction (QA: path never overlaps scenery).</summary>
        public Bounds BoundsAt(float fraction)
        {
            var b = surface.bounds;
            Vector2 offset = origin + travel * Mathf.SmoothStep(0, 1, Mathf.Clamp01(fraction)) - rb.position;
            b.center += (Vector3)offset;
            return b;
        }
    }
}
