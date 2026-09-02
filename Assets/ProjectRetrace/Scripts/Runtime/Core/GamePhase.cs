namespace ProjectRetrace
{
    public enum GamePhase
    {
        /// <summary>Start menu: pick single player or couch 2P. No run is live.</summary>
        Menu,

        /// <summary>Phase 1: hunt the house for the keys, leaving a trail as you go.</summary>
        Search,

        /// <summary>Brief beat while the house resets and the player returns to spawn.</summary>
        Transition,

        /// <summary>A stealth round: find the re-hidden keys while every past route's sentry
        /// patrols. Survive one and another begins, one sentry richer.</summary>
        Stealth,

        /// <summary>Out of lives. Shows how many rounds the run survived.</summary>
        Results
    }
}
