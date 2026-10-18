using UnityEngine;

namespace OneMoreTime
{
    public sealed class BackgroundDirector : MonoBehaviour
    {
        [SerializeField] private ParallaxBackground background;
        [Tooltip("Seis entradas: fases 1 a 5 y arena del boss. Se pueden reutilizar imágenes.")]
        [SerializeField] private Sprite[] phaseBackgrounds;
        [SerializeField] private SerpentSurface surface;
        public SerpentSurface Surface => surface;

        public void SetPhase(int phase, float start = 0, float end = 48)
        {
            if (surface != null) { surface.Configure(phase, start, end); return; }
            if (background == null || phaseBackgrounds == null || phaseBackgrounds.Length == 0) return;
            int index = Mathf.Clamp(phase - 1, 0, phaseBackgrounds.Length - 1);
            background.SetSprite(phaseBackgrounds[index]);
        }
    }
}
