using System;
using UnityEngine;

namespace OneMoreTime
{
    [DisallowMultipleComponent]
    public sealed class UpgradeManager : MonoBehaviour
    {
        private UpgradeDeck deck;
        private UpgradeDefinition[] choices = Array.Empty<UpgradeDefinition>();
        private bool awaitingChoice;
        public event Action<UpgradeDefinition[]> Offered;

        public void ResetRun(int seed)
        {
            deck = new UpgradeDeck(seed);
            Cancel();
        }

        public void Offer(StatBlock current = null)
        {
            if (deck == null) throw new InvalidOperationException("Inicializa UpgradeManager antes de ofrecer cartas.");
            choices = deck.DrawThree(current);
            awaitingChoice = true;
            Offered?.Invoke((UpgradeDefinition[])choices.Clone());
        }

        public bool TryConsume(int index, out UpgradeDefinition selected)
        {
            selected = null;
            if (!awaitingChoice || index < 0 || index >= choices.Length) return false;
            awaitingChoice = false; // Block double clicks before applying any effects.
            selected = choices[index];
            return true;
        }

        public void Cancel() { awaitingChoice = false; choices = Array.Empty<UpgradeDefinition>(); }
    }
}
