using System;
using UnityEngine;

namespace OneMoreTime
{
    public enum PortalState { Inactive, Opening, Active, Entering, Transition, Closing }

    /// <summary>
    /// One rune portal: Inactive → Opening → Active → Entering → Transition (→ Closing → Inactive).
    /// It does not change game state by itself; <see cref="PortalTransitionController"/> decides when it may
    /// open and what entering it means, so progression and upgrades stay owned by GameManager.
    /// </summary>
    public sealed class DimensionalPortal2D : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float openDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float closeDuration = 0.45f;
        [SerializeField, Min(0.3f)] private float interactRadius = 1.2f;
        private PortalVisualController visual;
        private float timer;
        public PortalState State { get; private set; } = PortalState.Inactive;
        public int Winter { get; private set; } = 1;
        public float InteractRadius => interactRadius;
        public event Action<DimensionalPortal2D> Opened;

        public static DimensionalPortal2D Create(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var portal = root.AddComponent<DimensionalPortal2D>();
            var v = new GameObject("PortalVisual"); v.transform.SetParent(root.transform, false);
            portal.visual = v.AddComponent<PortalVisualController>();
            portal.visual.Build(1);
            return portal;
        }

        public void Open(Vector2 groundPoint, int winter)
        {
            Winter = winter;
            transform.position = groundPoint;
            visual.Build(winter);
            State = PortalState.Opening; timer = 0;
        }

        public void BeginEntering() { if (State == PortalState.Active) { State = PortalState.Entering; visual.SetIntensity(1); } }
        public void MarkTransition() { State = PortalState.Transition; }

        public void Close()
        {
            if (State == PortalState.Inactive || State == PortalState.Closing) return;
            State = PortalState.Closing; timer = 0;
        }

        /// <summary>Immediate reset (new run, menu, phase rebuild): no residual state.</summary>
        public void Hide()
        {
            State = PortalState.Inactive; timer = 0;
            visual.SetIntensity(0); visual.SetOpen(0);
        }

        public bool Contains(Vector2 point) => State == PortalState.Active
            && Mathf.Abs(point.x - transform.position.x) <= interactRadius && point.y > transform.position.y - 0.6f && point.y < transform.position.y + 3.4f;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            switch (State)
            {
                case PortalState.Opening:
                    timer += dt;
                    visual.SetOpen(timer / openDuration);
                    visual.SetIntensity(Mathf.Clamp01(1 - timer / openDuration) * 0.6f);
                    if (timer >= openDuration) { State = PortalState.Active; visual.SetOpen(1); visual.SetIntensity(0); Opened?.Invoke(this); }
                    break;
                case PortalState.Closing:
                    timer += dt;
                    visual.SetOpen(1 - timer / closeDuration);
                    if (timer >= closeDuration) Hide();
                    break;
            }
        }
    }
}
