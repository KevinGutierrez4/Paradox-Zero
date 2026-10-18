using UnityEngine;

namespace OneMoreTime
{
    [RequireComponent(typeof(Health), typeof(MeleeWeapon))]
    [DisallowMultipleComponent]
    public sealed class PlayerGuard : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1)] private float damageMultiplier = 0.35f;
        [SerializeField, Range(0.1f, 1)] private float moveMultiplier = 0.45f;
        private Health health;
        private MeleeWeapon melee;
        private GameManager game;
        public bool IsBlocking { get; private set; }
        public float MoveMultiplier => IsBlocking ? moveMultiplier : 1;
        public float Reduction => 1 - damageMultiplier;
        private void Awake() { health = GetComponent<Health>(); melee = GetComponent<MeleeWeapon>(); }
        public void Initialize(GameManager manager)
        {
            if (game != null) game.StateChanged -= StateChanged;
            game = manager; if (game != null) game.StateChanged += StateChanged;
            SetBlocking(false);
        }
        public void SetBlocking(bool held)
        {
            if (health == null) health = GetComponent<Health>();
            bool next = held && isActiveAndEnabled && game != null && game.IsPlaying && !health.IsDead;
            if (next && !IsBlocking) melee.Cancel();
            IsBlocking = next; health.GuardDamageMultiplier = next ? damageMultiplier : 1;
        }
        private void StateChanged(RunState state) { if (state != RunState.Playing) SetBlocking(false); }
        private void OnDisable() { SetBlocking(false); }
        private void OnDestroy() { if (game != null) game.StateChanged -= StateChanged; }
    }
}
