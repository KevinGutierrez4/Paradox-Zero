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

        public void Bind(UpgradeDefinition upgrade, int cardIndex, Action<int> selected)
        {
            if (button == null) button = GetComponent<Button>();
            index = cardIndex; callback = selected;
            title.text = upgrade.Title; description.text = upgrade.Description;
            button.interactable = true;
        }

        public void Disable() { if (button != null) button.interactable = false; }
        private void Click() { callback?.Invoke(index); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Click); }
    }
}
