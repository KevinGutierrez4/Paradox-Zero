using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>Kinematic one-way platform that moves between two points and carries the player standing on it.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MovingPlatform : MonoBehaviour
    {
        [SerializeField] private Vector2 travel = new Vector2(3, 0);
        [SerializeField, Min(0.2f)] private float period = 4f;
        private Rigidbody2D rb;
        private Vector2 origin;
        private float phase;
        private PlayerController player;
        private Collider2D surface;

        public void Configure(Vector2 offset, float seconds) { travel = offset; period = Mathf.Max(0.2f, seconds); }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            surface = GetComponent<Collider2D>();
        }

        private void Start() { origin = rb.position; player = FindFirstObjectByType<PlayerController>(); }

        private void FixedUpdate()
        {
            phase += Time.fixedDeltaTime / period;
            Vector2 next = origin + travel * (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2));
            Vector2 delta = next - rb.position;
            rb.MovePosition(next);
            if (player != null && player.GroundCollider == surface && delta.sqrMagnitude > 0)
            {
                var pb = player.GetComponent<Rigidbody2D>();
                pb.position += delta;
            }
        }
    }
}
