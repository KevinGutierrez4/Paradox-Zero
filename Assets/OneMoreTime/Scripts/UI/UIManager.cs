using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneMoreTime
{
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        private const string VolumeKey = "OMT.Volume.v1";
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject hud;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button playButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button optionsBackButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseMenuButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button resultMenuButton;
        [SerializeField] private UpgradeCardUI[] cards;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Slider bossBar;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text healthText;
        [SerializeField] private Text phaseText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text bossText;
        [SerializeField] private Text resultTitle;
        [SerializeField] private Text resultDetails;
        [SerializeField] private Text menuRecord;
        private GameManager game;
        private float nextRefresh;
        private CanvasGroup upgradeNotice;
        private Text upgradeNoticeText;
        private float noticeUntil;
        private Text guardHint;

        public void Initialize(GameManager manager)
        {
            game = manager;
            playButton.onClick.AddListener(game.StartNewRun);
            retryButton.onClick.AddListener(game.StartNewRun);
            optionsButton.onClick.AddListener(OpenOptions);
            optionsBackButton.onClick.AddListener(CloseOptions);
            quitButton.onClick.AddListener(Quit);
            resumeButton.onClick.AddListener(game.TogglePause);
            pauseMenuButton.onClick.AddListener(game.ReturnToMenu);
            resultMenuButton.onClick.AddListener(game.ReturnToMenu);
            volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKey, 0.8f));
            AudioListener.volume = volumeSlider.value;
            volumeSlider.onValueChanged.AddListener(ChangeVolume);
            game.StateChanged += OnStateChanged;
            game.Player.Health.Changed += OnHealthChanged;
            game.Player.Stats.Changed += RefreshHUD;
            game.Player.Stats.UpgradeApplied += ShowUpgradeNotice;
            CreateUpgradeNotice();
            var controls = mainMenu.transform.Find("Controls")?.GetComponent<Text>();
            if (controls != null) controls.text = "A/D o flechas: correr · Espacio: saltar · S/↓: agacharse\nJ / clic izquierdo: hacha · K / clic derecho: bloquear (65% menos daño)\nEsc: pausa";
            var hint = new GameObject("GuardHint", typeof(RectTransform), typeof(Text)); hint.transform.SetParent(hud.transform, false);
            var hintRect = hint.GetComponent<RectTransform>(); hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0); hintRect.anchoredPosition = new Vector2(0, 22); hintRect.sizeDelta = new Vector2(1100, 30);
            guardHint = hint.GetComponent<Text>(); guardHint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); guardHint.fontSize = 18; guardHint.alignment = TextAnchor.MiddleCenter;
            guardHint.color = new Color(0.7f, 0.92f, 1); guardHint.raycastTarget = false;
            OnStateChanged(game.State);
        }

        private void Update()
        {
            if (upgradeNotice != null && upgradeNotice.gameObject.activeSelf)
            {
                float remaining = noticeUntil - Time.unscaledTime;
                upgradeNotice.alpha = Mathf.Clamp01(remaining / 0.35f);
                if (remaining <= 0) upgradeNotice.gameObject.SetActive(false);
            }
            // Unscaled time: menu and upgrade UI continue responding while gameplay is paused.
            if (game == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            if (hud.activeSelf) RefreshHUD();
        }

        private void OnStateChanged(RunState state)
        {
            if (upgradeNotice != null && (state == RunState.MainMenu || state == RunState.GameOver || state == RunState.Victory || state == RunState.ChoosingUpgrade))
                upgradeNotice.gameObject.SetActive(false);
            mainMenu.SetActive(state == RunState.MainMenu);
            optionsPanel.SetActive(false);
            hud.SetActive(state == RunState.Playing || state == RunState.Paused || state == RunState.ChoosingUpgrade);
            pausePanel.SetActive(state == RunState.Paused);
            upgradePanel.SetActive(state == RunState.ChoosingUpgrade);
            bool ended = state == RunState.GameOver || state == RunState.Victory;
            resultPanel.SetActive(ended);
            menuRecord.text = "Récord: " + game.BestScore;
            if (ended)
            {
                resultTitle.text = state == RunState.Victory ? "¡LÍNEA TEMPORAL RESTAURADA!" : "SOLO UNA VEZ MÁS";
                resultDetails.text = $"Fase {game.Phase}/6 · {game.Kills} derrotados\nPuntos: {game.Score} · Récord: {game.BestScore}\nTiempo: {FormatTime(game.RunTime)} · Semilla: {game.Seed}\nPrueba otra combinación de mejoras.";
            }
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                if (state == RunState.MainMenu) playButton.Select();
                else if (state == RunState.Paused) resumeButton.Select();
                else if (ended) retryButton.Select();
            }
            RefreshHUD();
        }

        public void ShowUpgradeCards(UpgradeDefinition[] offered)
        {
            if (cards == null || cards.Length != 3 || offered.Length != 3)
                throw new InvalidOperationException("Asigna exactamente tres UpgradeCardUI.");
            for (int i = 0; i < cards.Length; i++) cards[i].Bind(offered[i], i, SelectCard, game.Player.Stats.Current);
            cards[0].GetComponent<Button>().Select();
        }

        private void SelectCard(int index)
        {
            if (game.State != RunState.ChoosingUpgrade) return;
            foreach (var card in cards) card.Disable();
            game.SelectUpgrade(index);
        }

        private void OnHealthChanged(Health ignored) { RefreshHUD(); }

        private void RefreshHUD()
        {
            if (game == null || game.Player == null) return;
            var hp = game.Player.Health;
            var stats = game.Player.Stats.Current;
            if (guardHint != null) guardHint.text = game.Player.Guard != null && game.Player.Guard.IsBlocking
                ? "BLOQUEANDO · Recibes 65% menos daño · Suelta K / clic derecho para atacar" : "K / clic derecho: bloquear · J / clic izquierdo: hachazo";
            healthBar.value = hp.Maximum > 0 ? hp.Current / hp.Maximum : 0;
            healthText.text = $"VIDA {Mathf.CeilToInt(hp.Current - 0.0001f)}/{Mathf.CeilToInt(hp.Maximum - 0.0001f)}";
            phaseText.text = game.Phase == 6 ? "FASE 6/6 · BOSS" : $"FASE {game.Phase}/6";
            statsText.fontSize = 20;
            statsText.text = $"Daño {stats.damage:0.#}   Vel. {stats.moveSpeed:0.#}   Ataques {stats.fireRate:0.#}/s   Resiste {(1 - stats.damageTaken) * 100:0.#}%   Cura/baja {stats.killHealing:0.#}";
            progressText.text = game.Phase == 6 ? "Derrota a la serpiente" : $"Enemigos {game.Spawner.SpawnedCount}/{game.Spawner.Budget} · Vivos {game.Spawner.AliveCount} · Avanza hasta la fisura >";
            scoreText.text = $"{game.Score:000000} PTS   {FormatTime(game.RunTime)}";
            var boss = game.Spawner.CurrentBoss;
            bool showBoss = game.Phase == 6 && boss != null && boss.IsAlive;
            bossBar.gameObject.SetActive(showBoss); bossText.gameObject.SetActive(showBoss);
            if (showBoss)
            {
                bossBar.value = boss.Health.Current / boss.Health.Maximum;
                bossText.text = (boss.Enraged ? "SERPIENTE · FURIA · " : "SERPIENTE · ") + boss.PatternName;
            }
        }

        private void CreateUpgradeNotice()
        {
            var obj = new GameObject("UpgradeNotice", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
            obj.transform.SetParent(transform, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.sizeDelta = new Vector2(780, 105);
            obj.GetComponent<UnityEngine.UI.Image>().color = new Color(0.025f, 0.06f, 0.09f, 0.92f);
            obj.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            upgradeNotice = obj.GetComponent<CanvasGroup>(); upgradeNotice.blocksRaycasts = false; upgradeNotice.interactable = false;
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text)); textObject.transform.SetParent(obj.transform, false);
            var tr = textObject.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(14, 6); tr.offsetMax = new Vector2(-14, -6);
            upgradeNoticeText = textObject.GetComponent<Text>(); upgradeNoticeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            upgradeNoticeText.fontSize = 22; upgradeNoticeText.alignment = TextAnchor.MiddleCenter; upgradeNoticeText.raycastTarget = false;
            obj.SetActive(false);
        }
        private void ShowUpgradeNotice(UpgradeDefinition upgrade)
        {
            upgradeNoticeText.text = (upgrade.HasTradeoff ? "PACTO: " : "MEJORA: ") + upgrade.Title + "\n" + upgrade.Description;
            upgradeNoticeText.color = upgrade.HasTradeoff ? new Color(1, 0.73f, 0.55f) : new Color(0.5f, 1, 0.75f);
            upgradeNotice.gameObject.SetActive(true); upgradeNotice.transform.SetAsLastSibling();
            upgradeNotice.alpha = 1; noticeUntil = Time.unscaledTime + 2.4f;
        }

        private static string FormatTime(float time)
        { int seconds = Mathf.FloorToInt(time); return $"{seconds / 60:00}:{seconds % 60:00}"; }

        private void OpenOptions()
        { mainMenu.SetActive(false); optionsPanel.SetActive(true); optionsBackButton.Select(); }
        private void CloseOptions()
        { PlayerPrefs.Save(); optionsPanel.SetActive(false); mainMenu.SetActive(true); optionsButton.Select(); }
        private void ChangeVolume(float value)
        { AudioListener.volume = value; PlayerPrefs.SetFloat(VolumeKey, value); }
        private static void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (game == null) return;
            playButton.onClick.RemoveListener(game.StartNewRun);
            retryButton.onClick.RemoveListener(game.StartNewRun);
            optionsButton.onClick.RemoveListener(OpenOptions);
            optionsBackButton.onClick.RemoveListener(CloseOptions);
            quitButton.onClick.RemoveListener(Quit);
            resumeButton.onClick.RemoveListener(game.TogglePause);
            pauseMenuButton.onClick.RemoveListener(game.ReturnToMenu);
            resultMenuButton.onClick.RemoveListener(game.ReturnToMenu);
            volumeSlider.onValueChanged.RemoveListener(ChangeVolume);
            game.StateChanged -= OnStateChanged;
            if (game.Player != null && game.Player.Health != null)
                game.Player.Health.Changed -= OnHealthChanged;
            if (game.Player != null && game.Player.Stats != null)
            {
                game.Player.Stats.Changed -= RefreshHUD;
                game.Player.Stats.UpgradeApplied -= ShowUpgradeNotice;
            }
        }
    }
}
