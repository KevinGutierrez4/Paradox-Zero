using System;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreTime
{
    public enum StunQteState { Idle, Stunned, Escaping, Recovered, FailedTimeout }

    /// <summary>
    /// Stun caused by the silent roar of a phase change. The player is frozen (no movement, jump or attack)
    /// and escapes by alternating two keys from {A, W, D} for a number of full cycles (key-down only).
    /// A timeout always frees the player with a light penalty, never a permanent lock. Pause freezes the timer,
    /// and death, victory or returning to the menu close it cleanly.
    /// </summary>
    public sealed class BossRoarStunController : MonoBehaviour
    {
        [SerializeField, Min(1)] private int requiredCycles = 4;
        [SerializeField, Min(2f)] private float timeout = 6f;
        [SerializeField, Min(0)] private float timeoutPenalty = 6f;
        [SerializeField, Min(0.1f)] private float recoveredHold = 0.5f;

        private GameManager game;
        private PlayerController player;
        private KeyCode first, second;
        private float elapsed, holdUntil;
        private int presses;
        private Action<bool> onFinished;
        private GameObject panel;
        private Text title, counter, keyA, keyB, hint;
        private Image boxA, boxB, timerFill;

        public StunQteState State { get; private set; } = StunQteState.Idle;
        public int RequiredCycles { get => requiredCycles; set => requiredCycles = Mathf.Max(1, value); }
        public int CompletedCycles => presses / 2;
        public int Presses => presses;
        public KeyCode FirstKey => first;
        public KeyCode SecondKey => second;
        public KeyCode ExpectedKey => presses % 2 == 0 ? first : second;
        public bool Active => State == StunQteState.Stunned || State == StunQteState.Escaping;
        public float Timeout { get => timeout; set => timeout = Mathf.Max(2f, value); }

        public void Initialize(GameManager manager, PlayerController target)
        {
            if (game != null) game.StateChanged -= StateChanged;
            game = manager; player = target;
            if (game != null) game.StateChanged += StateChanged;
        }

        /// <summary>Begins the stun. Exactly two distinct keys from A, W, D.</summary>
        public void Begin(KeyCode keyOne, KeyCode keyTwo, Action<bool> finished)
        {
            if (!IsValid(keyOne) || !IsValid(keyTwo) || keyOne == keyTwo) { keyOne = KeyCode.A; keyTwo = KeyCode.D; }
            first = keyOne; second = keyTwo; presses = 0; elapsed = 0; onFinished = finished;
            State = StunQteState.Stunned;
            game?.SetPlayerControlLocked(true);
            if (player != null) { var b = player.GetComponent<Rigidbody2D>(); if (b != null) b.linearVelocity = new Vector2(0, Mathf.Min(0, b.linearVelocity.y)); }
            EnsureUi(); Refresh();
            panel.SetActive(true);
        }

        public static bool IsValid(KeyCode k) => k == KeyCode.A || k == KeyCode.W || k == KeyCode.D;

        private void Update()
        {
            if (State == StunQteState.Recovered || State == StunQteState.FailedTimeout)
            {
                if (Time.unscaledTime >= holdUntil) { State = StunQteState.Idle; if (panel != null) panel.SetActive(false); }
                return;
            }
            if (!Active) return;
            if (game == null || !game.IsPlaying) return; // pause freezes the stun (timer and input)
            if (player == null || player.Health.IsDead) { Close(); return; }
            elapsed += Time.deltaTime;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(ExpectedKey)) Press(ExpectedKey);
#endif
            if (Active && elapsed >= timeout) Finish(false);
            if (Active) Refresh();
        }

        /// <summary>Feeds one key-down (also used by automated tests). Wrong keys are ignored.</summary>
        public bool Press(KeyCode key)
        {
            if (!Active || key != ExpectedKey) return false;
            presses++;
            State = StunQteState.Escaping;
            if (player != null) PixelFx.Burst((Vector2)player.transform.position + Vector2.up * 1.1f, 4, new Color(1f, 0.95f, 0.6f), 2.5f, 0.25f, 0, 0.06f, 46);
            if (presses >= requiredCycles * 2) Finish(true); else Refresh();
            return true;
        }

        private void Finish(bool success)
        {
            State = success ? StunQteState.Recovered : StunQteState.FailedTimeout;
            holdUntil = Time.unscaledTime + recoveredHold;
            if (!success && player != null && !player.Health.IsDead && timeoutPenalty > 0)
            {
                // Light penalty that can never kill: at most down to 1 HP.
                float amount = Mathf.Min(timeoutPenalty, player.Health.Current - 1);
                if (amount > 0 && !player.Health.Invulnerable)
                {
                    float before = player.Health.Current;
                    player.Health.TakeDamage(new DamageInfo(amount, Team.Enemy));
                    if (player.Health.Current <= 0 && before > 1) player.Health.Heal(1);
                }
            }
            game?.SetPlayerControlLocked(false);
            Refresh();
            var callback = onFinished; onFinished = null;
            callback?.Invoke(success);
        }

        /// <summary>Death, victory, menu or restart: frees everything without callbacks into the boss.</summary>
        public void Close()
        {
            bool wasActive = Active;
            State = StunQteState.Idle; onFinished = null; presses = 0;
            if (panel != null) panel.SetActive(false);
            if (wasActive && game != null && player != null && !player.Health.IsDead && game.IsPlaying) game.SetPlayerControlLocked(false);
        }

        private void StateChanged(RunState state)
        {
            if (state == RunState.GameOver || state == RunState.Victory || state == RunState.MainMenu || state == RunState.ChoosingUpgrade) Close();
            if (panel != null && Active) panel.SetActive(state == RunState.Playing || state == RunState.Paused);
        }

        private void OnDestroy() { if (game != null) game.StateChanged -= StateChanged; if (panel != null) Destroy(panel); }

        // ------------------------------------------------------------------ UI
        private static string Label(KeyCode k) => k == KeyCode.A ? "A" : k == KeyCode.W ? "W" : k == KeyCode.D ? "D" : k.ToString();

        private void EnsureUi()
        {
            if (panel != null) return;
            var parent = game != null && game.UI != null ? game.UI.HudRoot : null;
            if (parent == null) { var canvas = FindAnyObjectByType<Canvas>(); parent = canvas != null ? canvas.transform : null; }
            if (parent == null) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = new GameObject("RoarStunQTE", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var pr = (RectTransform)panel.transform;
            // Centered slightly below the middle: never over the health bar (top-left) or the boss bar (top).
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.42f); pr.sizeDelta = new Vector2(560, 300);
            panel.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.09f, 0.72f);
            panel.GetComponent<Image>().raycastTarget = false;
            title = MakeText("Title", panel.transform, font, 30, new Vector2(0, 112), new Vector2(540, 44));
            hint = MakeText("Hint", panel.transform, font, 18, new Vector2(0, 76), new Vector2(540, 30));
            boxA = MakeBox("KeyA", panel.transform, new Vector2(-95, -5), out keyA, font);
            boxB = MakeBox("KeyB", panel.transform, new Vector2(95, -5), out keyB, font);
            counter = MakeText("Counter", panel.transform, font, 34, new Vector2(0, -100), new Vector2(300, 46));
            var bar = new GameObject("Timer", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(panel.transform, false);
            var br = (RectTransform)bar.transform; br.anchoredPosition = new Vector2(0, -136); br.sizeDelta = new Vector2(420, 10);
            bar.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(bar.transform, false);
            var fr = (RectTransform)fill.transform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(1, 1); fr.offsetMin = fr.offsetMax = Vector2.zero;
            timerFill = fill.GetComponent<Image>(); timerFill.color = new Color(1f, 0.55f, 0.3f, 0.9f);
            timerFill.type = Image.Type.Filled; timerFill.fillMethod = Image.FillMethod.Horizontal;
            timerFill.sprite = PixelFx.Pixel;
            panel.SetActive(false);
        }

        private static Text MakeText(string name, Transform parent, Font font, int size, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = pos; r.sizeDelta = box;
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        private static Image MakeBox(string name, Transform parent, Vector2 pos, out Text label, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = pos; r.sizeDelta = new Vector2(130, 130);
            var img = go.GetComponent<Image>(); img.raycastTarget = false;
            label = MakeText("Label", go.transform, font, 78, Vector2.zero, new Vector2(130, 130));
            label.fontStyle = FontStyle.Bold;
            return img;
        }

        private void Refresh()
        {
            if (panel == null) return;
            bool done = State == StunQteState.Recovered, failed = State == StunQteState.FailedTimeout;
            title.text = done ? "¡LIBRE!" : failed ? "TE LIBERAS A DURAS PENAS" : "¡ATURDIDO POR EL RUGIDO!";
            hint.text = done || failed ? "" : "Alterna las teclas para liberarte";
            keyA.text = Label(first); keyB.text = Label(second);
            bool expectA = Active && ExpectedKey == first && presses % 2 == 0;
            Color on = new Color(1f, 0.82f, 0.3f, 0.95f), off = new Color(0.25f, 0.28f, 0.36f, 0.9f);
            boxA.color = done ? new Color(0.4f, 1f, 0.6f, 0.9f) : expectA ? on : off;
            boxB.color = done ? new Color(0.4f, 1f, 0.6f, 0.9f) : Active && !expectA ? on : off;
            keyA.color = expectA ? Color.black : Color.white; keyB.color = Active && !expectA ? Color.black : Color.white;
            counter.text = Mathf.Min(CompletedCycles, requiredCycles) + "/" + requiredCycles;
            timerFill.fillAmount = Active ? 1 - Mathf.Clamp01(elapsed / timeout) : 0;
        }
    }
}
