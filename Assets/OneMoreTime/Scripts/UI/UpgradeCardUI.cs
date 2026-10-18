using System;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreTime
{
    [RequireComponent(typeof(Button))]
    public sealed class UpgradeCardUI : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text description;
        private Button button;
        private Action<int> callback;
        private int index;

        private void Awake()
        { button = GetComponent<Button>(); button.onClick.AddListener(Click); }

        public void Bind(UpgradeDefinition upgrade, int cardIndex, Action<int> selected, StatBlock current = null)
        {
            if (button == null) button = GetComponent<Button>();
            index = cardIndex; callback = selected;
            title.text = upgrade.TitleFor(current);
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 330);
            title.rectTransform.anchoredPosition = new Vector2(0, 115);
            title.rectTransform.sizeDelta = new Vector2(320, 65);
            title.fontSize = upgrade.Leveled ? 22 : 26;
            description.rectTransform.anchoredPosition = new Vector2(0, -35);
            description.rectTransform.sizeDelta = new Vector2(315, 220);
            title.color = new Color(1f, 0.86f, 0.55f);
            // Name, description, BUFF (green, +) and DEBUFF (red, -) with the effective before → after values.
            description.supportRichText = true;
            description.text = upgrade.Describe(current);
            description.fontSize = upgrade.Leveled ? 16 : 18;
            button.interactable = true;
        }

        public void Disable() { if (button != null) button.interactable = false; }
        private void Click() { callback?.Invoke(index); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Click); }
    }
}
