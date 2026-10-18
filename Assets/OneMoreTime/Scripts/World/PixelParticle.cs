using UnityEngine;

namespace OneMoreTime
{
    /// <summary>One pooled effect sprite. Uses gameplay time: pausing the game freezes it.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PixelParticle : MonoBehaviour
    {
        private SpriteRenderer draw;
        private Vector2 velocity;
        private Color color;
        private float life, age, gravity, size, spin, drag;
        private bool growth;

        public void Launch(Vector2 position, Vector2 v, Color c, float lifetime, float g, float s, int order, Sprite sprite, float spinSpeed, float dragAmount, bool grow)
        {
            if (draw == null) draw = GetComponent<SpriteRenderer>();
            transform.position = new Vector3(position.x, position.y, 0);
            transform.rotation = Quaternion.identity;
            velocity = v; color = c; life = Mathf.Max(0.02f, lifetime); age = 0; gravity = g; size = s; spin = spinSpeed; drag = dragAmount; growth = grow;
            draw.sprite = sprite; draw.sortingOrder = order; draw.color = c;
            float baseSize = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.x) : 1;
            transform.localScale = Vector3.one * (size / baseSize);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            age += dt;
            if (age >= life) { PixelFx.Recycle(this); return; }
            velocity.y -= gravity * dt;
            if (drag > 0) velocity *= Mathf.Max(0, 1 - drag * dt);
            transform.position += (Vector3)(velocity * dt);
            if (spin != 0) transform.Rotate(0, 0, spin * dt);
            float k = age / life;
            var c = color; c.a = color.a * (k < 0.15f && growth ? k / 0.15f : 1 - Mathf.Max(0, (k - 0.55f) / 0.45f));
            draw.color = c;
            if (growth)
            {
                float baseSize = draw.sprite != null ? Mathf.Max(0.01f, draw.sprite.bounds.size.x) : 1;
                transform.localScale = Vector3.one * (size * (0.6f + 0.8f * k) / baseSize);
            }
        }
    }
}
