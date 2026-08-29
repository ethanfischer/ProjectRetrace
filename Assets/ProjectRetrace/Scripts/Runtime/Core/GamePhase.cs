namespace ProjectRetrace
{
    public enum GamePhase
    {
        /// <summary>Phase 1: hunt the house for the keys, dropping breadcrumbs as you go.</summary>
        Search,

        /// <summary>Brief beat while the house resets and the player returns to spawn.</summary>
        Transition,

        /// <summary>Phase 2: walk the route again, blind, collecting those breadcrumbs.</summary>
        Retrace,

        /// <summary>Scored. Debug view shows which marks were hit and which were missed.</summary>
        Results
    }
}
