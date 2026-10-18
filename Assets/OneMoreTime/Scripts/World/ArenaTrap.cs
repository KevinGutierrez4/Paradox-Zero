using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    public enum TrapKind { Spikes, DangerousIce, Crack, JawTrap, IceGeyser, VenomVent, UnstablePlatform, Icicle }

    public enum TrapState { Idle, Warning, Active, Cooldown }

    /// <summary>
    /// Environmental trap. Each kind has a readable cycle (Idle → Warning → Active → Cooldown) and only hurts in its
    /// dangerous state. Sprites switch per state (geyser idle/erupting, open/closed jaw, ...). No projectiles.
    /// </summary>
    public sealed class ArenaTrap : MonoBehaviour
    {
        [SerializeField] private TrapKind kind;
        [SerializeField, Min(0)] private float damage = 10;
        [SerializeField] private Vector2 areaSize = new Vector2(1.4f, 0.8f);
        [SerializeField] private Vector2 areaOffset = new Vector2(0, 0.4f);
        [SerializeField, Min(0.1f)] private float idleTime = 2.4f;
        [SerializeField, Min(0.05f)] private float warningTime = 0.6f;
        [SerializeField, Min(0.05f)] private float activeTime = 0.9f;
        [SerializeField, Min(0.05f)] private float cooldownTime = 1.2f;
        [SerializeField] private Sprite idleSprite, warningSprite, activeSprite;
        [SerializeField] private float phaseOffset;
        private SpriteRenderer art;
        private Collider2D platformCollider;
        private PlayerController player;
        private Collider2D playerCollider;
        private GameManager game;
        private float timer, tick;
        private Vector3 restPosition;
        private bool hitThisCycle;

        public TrapKind Kind => kind;
        public TrapState State { get; private set; } = TrapState.Idle;
        public bool Dangerous => State == TrapState.Active && kind != TrapKind.UnstablePlatform;
        public Bounds Area => new Bounds((Vector2)transform.position + areaOffset, areaSize);

        public void Configure(TrapKind trapKind, float hit, Vector2 size, Vector2 offset, Sprite idle, Sprite warning, Sprite active,
            float idleSeconds, float warnSeconds, float activeSeconds, float cooldownSeconds, float offsetSeconds = 0)
        {
            kind = trapKind; damage = hit; areaSize = size; areaOffset = offset;
            idleSprite = idle; warningSprite = warning != null ? warning : idle; activeSprite = active != null ? active : idle;
            idleTime = idleSeconds; warningTime = warnSeconds; activeTime = activeSeconds; cooldownTime = cooldownSeconds; phaseOffset = offsetSeconds;
        }

        private void Start()
        {
            art = GetComponentInChildren<SpriteRenderer>();
            platformCollider = GetComponent<Collider2D>();
            game = FindFirstObjectByType<GameManager>();
            player = game != null ? game.Player : FindFirstObjectByType<PlayerController>();
            playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
            restPosition = transform.localPosition;
            timer = -phaseOffset;
            Enter(TrapState.Idle);
        }

        private bool Triggered => kind == TrapKind.JawTrap || kind == TrapKind.DangerousIce || kind == TrapKind.UnstablePlatform || kind == TrapKind.Icicle;

        private bool PlayerInside(Bounds b) => playerCollider != null && !player.Health.IsDead && b.Intersects(playerCollider.bounds);

        private bool PlayerStandingOn()
        {
            if (player == null) return false;
            if (platformCollider != null && player.GroundCollider == platformCollider) return true;
            return PlayerInside(Area) && player.Grounded;
        }

        private void Enter(TrapState next)
        {
            State = next; timer = 0; hitThisCycle = false; tick = 0;
            if (art == null) return;
            art.sprite = next == TrapState.Active || (next == TrapState.Cooldown && kind == TrapKind.JawTrap) ? activeSprite : next == TrapState.Warning ? warningSprite : idleSprite;
            art.color = Color.white;
            if (kind == TrapKind.UnstablePlatform && platformCollider != null) platformCollider.enabled = next != TrapState.Active;
            if (kind == TrapKind.DangerousIce && next == TrapState.Cooldown) art.color = new Color(1, 1, 1, 0.35f);
            if (kind == TrapKind.Icicle) transform.localPosition = restPosition;
            // Spikes are retracted (squashed into the floor) when harmless, fully out only while dangerous.
            if (kind == TrapKind.Spikes) art.transform.localScale = new Vector3(1, next == TrapState.Active ? 1f : 0.35f, 1);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0 || art == null) return;
            timer += dt;
            switch (State)
            {
                case TrapState.Idle:
                    if (Triggered)
                    {
                        bool trigger = kind == TrapKind.Icicle
                            ? player != null && Mathf.Abs(player.transform.position.x - transform.position.x) < 0.9f && player.transform.position.y < transform.position.y
                            : kind == TrapKind.JawTrap ? PlayerInside(Area) : PlayerStandingOn();
                        if (trigger) Enter(TrapState.Warning);
                    }
                    else if (timer >= idleTime) Enter(TrapState.Warning);
                    break;
                case TrapState.Warning:
                    Warn();
                    if (timer >= warningTime) Enter(TrapState.Active);
                    break;
                case TrapState.Active:
                    Act(dt);
                    if (timer >= activeTime) Enter(TrapState.Cooldown);
                    break;
                case TrapState.Cooldown:
                    if (kind == TrapKind.UnstablePlatform && art != null)
                        art.color = new Color(1, 1, 1, Mathf.Clamp01(timer / cooldownTime));
                    if (timer >= cooldownTime) { transform.localPosition = restPosition; Enter(TrapState.Idle); }
                    break;
            }
        }

        private void Warn()
        {
            float k = Mathf.Clamp01(timer / warningTime);
            switch (kind)
            {
                case TrapKind.UnstablePlatform:
                case TrapKind.DangerousIce:
                case TrapKind.Icicle:
                    transform.localPosition = restPosition + new Vector3(Mathf.Sin(timer * 70f) * 0.04f, 0, 0);
                    if (Random.value < 0.2f) PixelFx.Emit(transform.position + Vector3.down * 0.1f, new Vector2(Random.Range(-0.5f, 0.5f), -1f), new Color(0.85f, 0.95f, 1f), 0.4f, 6f, 0.06f, 31);
                    break;
                case TrapKind.IceGeyser:
                    if (Random.value < 0.4f) PixelFx.Emit(transform.position + Vector3.up * 0.3f, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.4f)), new Color(1, 1, 1, 0.6f), 0.5f, -0.3f, 0.25f, 31, PixelFx.SoftDot, 0, 0.5f, true);
                    break;
                case TrapKind.VenomVent:
                    if (Random.value < 0.4f) PixelFx.Emit(transform.position + Vector3.up * 0.3f, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 1f)), new Color(0.55f, 1f, 0.45f, 0.6f), 0.5f, -0.2f, 0.25f, 31, PixelFx.SoftDot, 0, 0.5f, true);
                    break;
                case TrapKind.Crack:
                    art.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.45f), 0.5f + 0.5f * Mathf.Sin(timer * 25f));
                    break;
                case TrapKind.Spikes:
                    art.transform.localScale = new Vector3(1, 0.35f + 0.2f * k + 0.04f * Mathf.Sin(timer * 40f), 1);
                    break;
            }
        }

        private void Act(float dt)
        {
            Vector2 push = new Vector2(player != null ? Mathf.Sign(player.transform.position.x - transform.position.x + 0.001f) * 3f : 0, 3f);
            switch (kind)
            {
                case TrapKind.UnstablePlatform:
                    transform.localPosition += Vector3.down * 6f * dt; // falls away, harmless by itself
                    art.color = new Color(1, 1, 1, 1 - Mathf.Clamp01(timer / activeTime));
                    return;
                case TrapKind.Icicle:
                    transform.localPosition += Vector3.down * 11f * dt;
                    if (!hitThisCycle && PlayerInside(new Bounds(transform.position + Vector3.down * 0.4f, new Vector3(0.6f, 0.8f, 1))))
                        hitThisCycle = player.Health.TakeDamage(new DamageInfo(damage, Team.Enemy, new Vector2(push.x, 1f)));
                    if (game != null && transform.position.y < game.GroundHeight(transform.position.x) + 0.2f)
                    {
                        PixelFx.Burst(transform.position, 8, new Color(0.85f, 0.95f, 1f), 3f, 0.5f, 7, 0.07f, 31);
                        art.color = new Color(1, 1, 1, 0); Enter(TrapState.Cooldown);
                    }
                    return;
                case TrapKind.VenomVent:
                    if (Random.value < 0.6f) PixelFx.Emit((Vector2)transform.position + areaOffset + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.4f, 0.6f)), new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.3f, 0.9f)), new Color(0.55f, 1f, 0.45f, 0.65f), 0.8f, -0.2f, 0.5f, 31, PixelFx.SoftDot, 0, 0.8f, true);
                    tick -= dt;
                    if (tick <= 0) { tick = 0.45f; if (PlayerInside(Area)) player.Health.TakeDamage(new DamageInfo(damage, Team.Enemy)); }
                    return;
                case TrapKind.IceGeyser:
                    if (Random.value < 0.7f) PixelFx.Emit((Vector2)transform.position + new Vector2(Random.Range(-0.2f, 0.2f), 0.4f), new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(5f, 8f)), new Color(0.85f, 0.95f, 1f), 0.4f, 4f, 0.08f, 31);
                    break;
                case TrapKind.DangerousIce:
                    if (timer < dt * 1.5f) PixelFx.Burst(transform.position + Vector3.up * 0.2f, 12, new Color(0.8f, 0.92f, 1f), 3.5f, 0.6f, 8, 0.08f, 31);
                    break;
                case TrapKind.Crack:
                    if (Random.value < 0.6f) PixelFx.Emit((Vector2)transform.position + new Vector2(Random.Range(-0.5f, 0.5f), 0.1f), new Vector2(0, Random.Range(2f, 4f)), new Color(1f, 0.55f, 0.35f), 0.35f, 2f, 0.08f, 31);
                    break;
                case TrapKind.Spikes:
                    break;
            }
            if (!hitThisCycle && PlayerInside(Area))
                hitThisCycle = player.Health.TakeDamage(new DamageInfo(damage, Team.Enemy, push));
        }
    }
}
