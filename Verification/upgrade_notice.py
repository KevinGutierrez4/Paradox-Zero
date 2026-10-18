from pathlib import Path
p=Path('Assets/OneMoreTime/Scripts/UI/UIManager.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('private float nextRefresh;','private float nextRefresh;\n        private CanvasGroup upgradeNotice;\n        private Text upgradeNoticeText;\n        private float noticeUntil;')
s=s.replace('game.Player.Stats.Changed += RefreshHUD;','game.Player.Stats.Changed += RefreshHUD;\n            game.Player.Stats.UpgradeApplied += ShowUpgradeNotice;\n            CreateUpgradeNotice();')
s=s.replace('''            // Unscaled time: menu''','''            if (upgradeNotice != null && upgradeNotice.gameObject.activeSelf)
            {
                float remaining = noticeUntil - Time.unscaledTime;
                upgradeNotice.alpha = Mathf.Clamp01(remaining / 0.35f);
                if (remaining <= 0) upgradeNotice.gameObject.SetActive(false);
            }
            // Unscaled time: menu''')
s=s.replace('''            mainMenu.SetActive(state == RunState.MainMenu);''','''            if (upgradeNotice != null && (state == RunState.MainMenu || state == RunState.GameOver || state == RunState.Victory))
                upgradeNotice.gameObject.SetActive(false);
            mainMenu.SetActive(state == RunState.MainMenu);''')
s=s.replace('cards[i].Bind(offered[i], i, SelectCard);','cards[i].Bind(offered[i], i, SelectCard, game.Player.Stats.Current);')
s=s.replace('''            statsText.text = $"Daño/golpe {stats.damage:0.#}   Vel. {stats.moveSpeed:0.#}   Ataques {stats.fireRate:0.#}/s";''','''            statsText.fontSize = 20;
            statsText.text = $"Daño {stats.damage:0.#}   Vel. {stats.moveSpeed:0.#}   Ataques {stats.fireRate:0.#}/s   Resiste {(1 - stats.damageTaken) * 100:0.#}%   Cura/baja {stats.killHealing:0.#}";''')
pos=s.index('        private static string FormatTime(')
s=s[:pos]+'''        private void CreateUpgradeNotice()
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
            upgradeNoticeText.text = (upgrade.HasTradeoff ? "PACTO: " : "MEJORA: ") + upgrade.Title + "\\n" + upgrade.Description;
            upgradeNoticeText.color = upgrade.HasTradeoff ? new Color(1, 0.73f, 0.55f) : new Color(0.5f, 1, 0.75f);
            upgradeNotice.gameObject.SetActive(true); upgradeNotice.transform.SetAsLastSibling();
            upgradeNotice.alpha = 1; noticeUntil = Time.unscaledTime + 2.4f;
        }

'''+s[pos:]
s=s.replace('''            game.Player.Stats.Changed -= RefreshHUD;''','''            game.Player.Stats.Changed -= RefreshHUD;
            game.Player.Stats.UpgradeApplied -= ShowUpgradeNotice;''')
p.write_text(s,encoding='utf-8')
