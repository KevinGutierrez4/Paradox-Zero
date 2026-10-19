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

        [Tooltip("Tinte suave por invierno de Fimbulvetr (fases 1-2, 3-4, 5-jefe). Blanco = arte original.")]
        [SerializeField] private Color[] winterTints = { Color.white, new Color(0.9f, 0.93f, 1f), new Color(1f, 0.88f, 0.86f) };

        public static int WinterOf(int phase) => phase <= 2 ? 1 : phase <= 4 ? 2 : 3;

        public void SetPhase(int phase, float start = 0, float end = 48)
        {
            Color tint = winterTints != null && winterTints.Length >= 3 ? winterTints[WinterOf(phase) - 1] : Color.white;
            if (background != null) { var r = background.GetComponent<SpriteRenderer>(); if (r != null) r.color = tint; }
            if (surface != null) { surface.Configure(phase, start, end); var r = surface.GetComponent<SpriteRenderer>(); if (r != null) r.color = tint; return; }
            if (background == null || phaseBackgrounds == null || phaseBackgrounds.Length == 0) return;
            int index = Mathf.Clamp(phase - 1, 0, phaseBackgrounds.Length - 1);
            background.SetSprite(phaseBackgrounds[index]);
        }
    }
}
