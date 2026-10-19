using UnityEngine;

namespace OneMoreTime
{
    /// <summary>The same phase prefabs opened in the visual editor are instantiated during play.</summary>
    public sealed class StageLayoutLibrary : ScriptableObject
    {
        public const string ResourceName = "OMT_StageLayouts";
        public const string ContentName = "Plataformas y trampas";
        public const string PreviewName = "Referencia del terreno";
        public GameObject[] phasePrefabs = new GameObject[6];
        public GameObject ForPhase(int phase) => phasePrefabs != null && phase >= 1 && phase <= phasePrefabs.Length
            ? phasePrefabs[phase - 1] : null;
        public static StageLayoutLibrary Load() => Resources.Load<StageLayoutLibrary>(ResourceName);
    }
}
