using UnityEngine;

namespace OneMoreTime
{
    public sealed class BackgroundDirector : MonoBehaviour
    {
        [SerializeField] private ParallaxBackground background;
        [Tooltip("Seis entradas: fases 1 a 5 y arena del boss. Se pueden reutilizar imágenes.")]
        [SerializeField] private Sprite[] phaseBackgrounds;

        public void SetPhase(int phase)
        {
            if (background == null || phaseBackgrounds == null || phaseBackgrounds.Length == 0) return;
            int index = Mathf.Clamp(phase - 1, 0, phaseBackgrounds.Length - 1);
            background.SetSprite(phaseBackgrounds[index]);
        }
    }
}
