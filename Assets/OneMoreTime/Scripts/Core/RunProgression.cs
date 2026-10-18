using System;

namespace OneMoreTime
{
    public enum RunState { MainMenu, Playing, Paused, ChoosingUpgrade, GameOver, Victory }

    /// <summary>Rules independent of Unity: exactly five normal phases and one boss.</summary>
    public sealed class RunProgression
    {
        public const int NormalPhases = 5;
        public const int TotalPhases = 6;
        public RunState State { get; private set; } = RunState.MainMenu;
        public int Phase { get; private set; }
        public bool IsBossPhase => Phase == TotalPhases;

        public void StartRun() { Phase = 1; State = RunState.Playing; }

        public bool CompletePhase()
        {
            if (State != RunState.Playing) return false;
            State = IsBossPhase ? RunState.Victory : RunState.ChoosingUpgrade;
            return true;
        }

        public bool ChooseUpgrade()
        {
            if (State != RunState.ChoosingUpgrade || Phase >= TotalPhases) return false;
            Phase++;
            State = RunState.Playing;
            return true;
        }

        public bool TogglePause()
        {
            if (State != RunState.Playing && State != RunState.Paused) return false;
            State = State == RunState.Playing ? RunState.Paused : RunState.Playing;
            return true;
        }

        public bool Die()
        {
            if (State != RunState.Playing) return false;
            State = RunState.GameOver;
            return true;
        }

        public void ReturnToMenu() { Phase = 0; State = RunState.MainMenu; }
    }
}
