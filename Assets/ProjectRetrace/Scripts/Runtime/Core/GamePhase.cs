namespace ProjectRetrace
{
    public enum GamePhase
    {
        /// <summary>Phase 1: hunt the house for the keys, leaving a trail as you go.</summary>
        Search,

        /// <summary>Brief beat while the house resets and the player returns to spawn.</summary>
        Transition,

        /// <summary>Phase 2: find the re-hidden keys while the sentry retraces your route.</summary>
        Stealth,

        /// <summary>Won or caught. Debug view shows the patrol route beside your sneak route.</summary>
        Results
    }
}
