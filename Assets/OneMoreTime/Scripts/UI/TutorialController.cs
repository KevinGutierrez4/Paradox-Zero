using UnityEngine;
using UnityEngine.UI;

namespace OneMoreTime
{
    /// <summary>
    /// Interactive tutorial at the start of the first phase, with large on-screen keys. Each step completes only
    /// when the player really performs the action: move (A/D), jump the crate (Space), attack (J), defend (K) and
    /// dodge the spike trap (L / Shift). Tab skips it. Completion is saved, so it never repeats in later phases
    /// or runs. Enemy spawning waits while it is active. The panel sits at the bottom, away from the health bar.
    /// </summary>
    public sealed class TutorialController : MonoBehaviour
    {
        public const string DoneKey = "OMT.TutorialDone.v1";
        public enum Step { Move, Jump, Attack, Defend, Dodge, Done }

        /// <summary>Automated acceptance runs disable the tutorial (they drive the player directly).</summary>
        public static bool Suppressed;

        private GameManager game;
        private PlayerController player;
        private GameObject panel;
        private Text title, body, counter;
        private readonly Text[] keyLabels = new Text[3];
        private readonly Image[] keyBoxes = new Image[3];
        private float startX, defendTime, doneUntil;
        private int startJumps, startDodges, swings;
        private bool wasAttacking;

        public Step Current { get; private set; } = Step.Done;
        public bool Active => Current != Step.Done;
        public bool Persist { get; set; } = true;
        public static bool Completed => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        public void Initialize(GameManager manager, PlayerController target) { game = manager; player = target; }

        /// <summary>Called when a phase begins. Starts only in phase 1 and only if never completed.</summary>
        public void OnPhaseBegun(int phase)
        {
            if (phase != 1 || Suppressed || (Persist && Completed)) { Hide(); return; }
            Begin();
        }

        public void Begin()
        {
            EnsureUi();
            Current = Step.Move; startX = player.transform.position.x; swings = 0; defendTime = 0;
            startJumps = player.JumpCount; startDodges = player.DodgeCount;
            if (panel != null) panel.SetActive(true);
            Refresh();
        }

        public void Skip() { Finish(); }

        private void Finish()
        {
            Current = Step.Done;
            if (Persist) { PlayerPrefs.SetInt(DoneKey, 1); PlayerPrefs.Save(); }
            doneUntil = Time.unscaledTime + 1.6f;
            Refresh();
        }

        private void Hide() { Current = Step.Done; if (panel != null) panel.SetActive(false); }

        private void Update()
        {
            if (panel != null && !Active && panel.activeSelf && Time.unscaledTime > doneUntil) panel.SetActive(false);
            if (!Active || game == null || player == null) return;
            if (panel != null) panel.SetActive(game.State == RunState.Playing);
            if (!game.IsPlaying) return;
            if (game.Phase != 1) { Hide(); return; }
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Tab)) { Skip(); return; }
#endif
            var melee = player.GetComponent<MeleeWeapon>();
            switch (Current)
            {
                case Step.Move:
                    if (Mathf.Abs(player.transform.position.x - startX) > 2f) Advance();
                    break;
                case Step.Jump:
                    var crate = game.Stage != null ? FindCrate() : null;
                    bool past = crate == null ? player.JumpCount > startJumps : player.transform.position.x > crate.position.x + 0.9f;
                    if (past && player.JumpCount > startJumps) Advance();
                    break;
                case Step.Attack:
                    bool attacking = melee != null && melee.IsAttacking;
                    if (attacking && !wasAttacking) swings++;
                    wasAttacking = attacking;
                    if (swings >= 2) Advance();
                    break;
                case Step.Defend:
                    if (player.Guard != null && player.Guard.IsBlocking) defendTime += Time.deltaTime;
                    if (defendTime >= 0.5f) Advance();
                    break;
                case Step.Dodge:
                    if (player.DodgeCount > startDodges) Finish();
                    break;
            }
            Refresh();
        }

        private Transform FindCrate()
        {
            foreach (var go in game.Stage.Props) if (go != null && go.name == "TutorialCrate") return go.transform;
            return null;
        }

        private void Advance()
        {
            Current++;
            startJumps = player.JumpCount; startDodges = player.DodgeCount;
            PixelFx.Burst((Vector2)player.transform.position + Vector2.up * 1.2f, 10, new Color(1f, 0.9f, 0.5f), 3f, 0.4f, 0, 0.07f, 46);
            if (Current == Step.Done) Finish();
        }

        /// <summary>For automated tests: completes the current step as if the player performed it.</summary>
        public void ForceAdvance() { if (Active) { if (Current == Step.Dodge) Finish(); else Advance(); Refresh(); } }

        // ------------------------------------------------------------------ UI
        private void EnsureUi()
        {
            if (panel != null) return;
            var parent = game != null && game.UI != null ? game.UI.HudRoot : null;
            if (parent == null) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = new GameObject("Tutorial", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var r = (RectTransform)panel.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0, 28); r.sizeDelta = new Vector2(620, 210);
            var bg = panel.GetComponent<Image>(); bg.color = new Color(0.03f, 0.05f, 0.1f, 0.7f); bg.raycastTarget = false;
            title = Text("Title", font, 26, new Vector2(0, 80), new Vector2(600, 36));
            body = Text("Body", font, 18, new Vector2(0, -62), new Vector2(600, 30));
            counter = Text("Counter", font, 15, new Vector2(0, -88), new Vector2(600, 24));
            counter.color = new Color(1, 1, 1, 0.65f);
            for (int i = 0; i < 3; i++)
            {
                var box = new GameObject("Key" + i, typeof(RectTransform), typeof(Image)); box.transform.SetParent(panel.transform, false);
                var br = (RectTransform)box.transform; br.anchoredPosition = new Vector2((i - 1) * 150, 10); br.sizeDelta = new Vector2(120, 92);
                keyBoxes[i] = box.GetComponent<Image>(); keyBoxes[i].raycastTarget = false;
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text)); label.transform.SetParent(box.transform, false);
                var lr = (RectTransform)label.transform; lr.sizeDelta = new Vector2(120, 92);
                keyLabels[i] = label.GetComponent<Text>(); keyLabels[i].font = font; keyLabels[i].fontSize = 40; keyLabels[i].fontStyle = FontStyle.Bold;
                keyLabels[i].alignment = TextAnchor.MiddleCenter; keyLabels[i].color = Color.black; keyLabels[i].raycastTarget = false;
            }
            panel.SetActive(false);
        }

        private Text Text(string name, Font font, int size, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(panel.transform, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = pos; r.sizeDelta = box;
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            return t;
        }

        private void Keys(params string[] keys)
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i < keys.Length;
                keyBoxes[i].gameObject.SetActive(on);
                if (!on) continue;
                keyLabels[i].text = keys[i];
                keyLabels[i].fontSize = keys[i].Length > 3 ? 24 : 40;
                keyBoxes[i].color = new Color(1f, 0.85f, 0.4f, 0.95f);
                var br = (RectTransform)keyBoxes[i].transform;
                br.anchoredPosition = new Vector2((i - (keys.Length - 1) * 0.5f) * 150, 10);
                br.sizeDelta = new Vector2(keys[i].Length > 3 ? 170 : 120, 92);
            }
        }

        private void Refresh()
        {
            if (panel == null) return;
            int index = Mathf.Min((int)Current, 4) + 1;
            counter.text = Active ? $"Tutorial {index}/5  ·  Tab: saltar tutorial" : "";
            switch (Current)
            {
                case Step.Move: title.text = "MOVERSE"; Keys("A", "D"); body.text = "Camina a izquierda y derecha"; break;
                case Step.Jump: title.text = "SALTAR"; Keys("ESPACIO"); body.text = "Salta por encima de la caja"; break;
                case Step.Attack: title.text = "ATACAR"; Keys("J"); body.text = "Golpea con el hacha (o clic izquierdo) · " + swings + "/2"; break;
                case Step.Defend: title.text = "DEFENDERSE"; Keys("K"); body.text = "Mantén la guardia (o clic derecho)"; break;
                case Step.Dodge: title.text = "ESQUIVAR"; Keys("L", "SHIFT"); body.text = "Esquiva a través de las púas: eres invulnerable un instante"; break;
                default: title.text = "¡LISTO, GUERRERO!"; Keys(); body.text = "Derrota a los enemigos y cruza el portal rúnico"; break;
            }
        }
    }
}
