namespace ProjectRetrace
{
    public enum GamePhase
    {
        /// <summary>Start menu: pick single player, couch 2P, or online. No run is live.</summary>
        Menu,

        /// <summary>Phase 1: hunt the house for the keys, leaving a trail as you go.</summary>
        Search,

        /// <summary>Brief beat while the house resets and the player returns to spawn.</summary>
        Transition,

        /// <summary>A stealth round: find the re-hidden keys while every past route's sentry
        /// patrols. Survive one and another begins, one sentry richer.</summary>
        Stealth,

        /// <summary>Online: the opponent's round, watched live. Nothing is simulated here --
        /// the turn owner's client is the only truth and this one just draws it.</summary>
        Spectate,

        /// <summary>Out of lives. Shows how many rounds the run survived.</summary>
        Results
    }
}
