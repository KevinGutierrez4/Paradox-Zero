using System.Collections;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>
    /// Owns the exit portal of each normal phase and the arrival portal of the next one.
    /// The exit only opens once GameManager reports the phase objectives complete; entering it runs a short
    /// travel animation and then calls back into GameManager, which shows the existing upgrade choice.
    /// Nothing here touches stats, health or the phase counter.
    /// </summary>
    public sealed class PortalTransitionController : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] private float enterDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float arrivalFade = 0.5f;
        private GameManager game;
        private PlayerController player;
        private Rigidbody2D playerBody;
        private AnimationDriver playerVisuals;
        private DimensionalPortal2D exit, arrival;
        private Coroutine routine;
        public bool Busy { get; private set; }
        public bool ExitOpen => exit != null && exit.State == PortalState.Active;
        public bool ExitOpening => exit != null && exit.State == PortalState.Opening;
        public bool PlayerInRange { get; private set; }
        public DimensionalPortal2D Exit => exit;

        public void Initialize(GameManager manager, PlayerController target)
        {
            game = manager; player = target;
            playerBody = player.GetComponent<Rigidbody2D>(); playerVisuals = player.GetComponent<AnimationDriver>();
            if (exit == null) exit = DimensionalPortal2D.Create("ExitPortal", transform);
            if (arrival == null) arrival = DimensionalPortal2D.Create("ArrivalPortal", transform);
            ResetAll();
        }

        public void ResetAll()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            Busy = false; PlayerInRange = false;
            exit?.Hide(); arrival?.Hide();
            if (playerVisuals != null) playerVisuals.SetFade(1);
        }

        /// <summary>Called by GameManager at the start of every phase (including the first and the boss arena).</summary>
        public void OnPhaseBegun(Vector2 playerGround, int winter)
        {
            ResetAll();
            routine = StartCoroutine(Arrive(playerGround, winter));
        }

        /// <summary>Opens the exit once (idempotent). GameManager only calls this when the phase is cleared.</summary>
        public void OpenExit(Vector2 groundPoint, int winter)
        {
            if (exit == null || exit.State != PortalState.Inactive || Busy) return;
            exit.Open(groundPoint, winter);
        }

        private IEnumerator Arrive(Vector2 at, int winter)
        {
            // Purely visual: the player is already placed on the terrain and keeps control.
            arrival.Open(at, winter);
            float t = 0;
            while (t < arrivalFade)
            {
                t += Time.deltaTime;
                if (playerVisuals != null && !player.Health.IsDead) playerVisuals.SetFade(Mathf.Clamp01(t / arrivalFade));
                yield return null;
            }
            if (playerVisuals != null && !player.Health.IsDead) playerVisuals.SetFade(1);
            float hold = 0;
            while (hold < 0.6f) { hold += Time.deltaTime; yield return null; }
            arrival.Close();
            routine = null;
        }

        private void LateUpdate()
        {
            // After every Update, so the player's interact key for this frame has been read.
            PlayerInRange = ExitOpen && !Busy && player != null && exit.Contains(player.transform.position);
            if (PlayerInRange && player.InteractPressed && !player.ControlLocked) RequestEnter();
        }

        public bool RequestEnter()
        {
            if (Busy || !ExitOpen || game == null || !game.CanLeaveThroughPortal) return false;
            Busy = true;
            routine = StartCoroutine(Enter());
            return true;
        }

        private IEnumerator Enter()
        {
            game.SetPlayerControlLocked(true);
            exit.BeginEntering();
            float startX = playerBody.position.x, t = 0;
            while (t < enterDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0, 1, t / enterDuration);
                playerBody.position = new Vector2(Mathf.Lerp(startX, exit.transform.position.x, k), playerBody.position.y);
                playerVisuals.SetFade(1 - 0.9f * k);
                if (Random.value < 0.6f)
                {
                    Vector2 core = (Vector2)exit.transform.position + Vector2.up * 1.5f;
                    Vector2 from = core + Random.insideUnitCircle.normalized * 1.3f;
                    PixelFx.Emit(from, (core - from) * 2.4f, PortalVisualController.Light(exit.Winter), 0.4f, 0, 0.07f, 3);
                }
                yield return null;
            }
            exit.MarkTransition();
            routine = null;
            // GameManager validates again and opens the existing upgrade selection (pauses the game).
            if (!game.CompletePhaseThroughPortal()) { Busy = false; playerVisuals.SetFade(1); game.SetPlayerControlLocked(false); exit.Hide(); }
        }
    }
}
