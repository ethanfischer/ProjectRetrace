using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// Tuning for the trail recording and the sentry. sentrySpeed, visionRange and visionAngle
    /// are the difficulty curve -- they are the numbers worth fiddling with during playtests.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectRetrace/Retrace Settings", fileName = "RetraceSettings")]
    public class RetraceSettings : ScriptableObject
    {
        [Header("Trail")]
        [Tooltip("Metres of travel between breadcrumbs. Smaller = the patrol follows the player's phase-1 route more faithfully.")]
        [Min(0.1f)]
        public float dotSpacing = 1.5f;

        [Header("Sentry")]
        [Tooltip("Patrol speed. Kept below the player's walk speed (3.4) so being followed stays escapable.")]
        [Min(0.1f)]
        public float sentrySpeed = 2.8f;

        [Tooltip("Speed once the player has been spotted. The catch is already decided at the moment of detection -- the chase only sells it.")]
        [Min(0.1f)]
        public float chaseSpeed = 5.5f;

        [Tooltip("Distance at which a chasing sentry catches the player.")]
        [Min(0.1f)]
        public float catchDistance = 1.1f;

        [Header("Vision")]
        [Tooltip("How far the sentry can see. Indoors the walls do most of the limiting via line of sight, so this mostly matters down corridors.")]
        [Min(0.5f)]
        public float visionRange = 11f;

        [Tooltip("Total horizontal cone angle in degrees.")]
        [Range(10f, 180f)]
        public float visionAngle = 80f;

        [Tooltip("Seconds after the stealth phase starts before the sentry can spot the player. Covers degenerate short routes where the patrol begins near spawn.")]
        [Min(0f)]
        public float graceSeconds = 3f;

        [Header("Dwell")]
        [Tooltip("Staying within this radius counts as one continuous stop.")]
        [Min(0.1f)]
        public float dwellRadius = 0.9f;

        [Tooltip("Seconds of standing still before the stop is recorded into the patrol.")]
        [Min(0.1f)]
        public float dwellSeconds = 2f;

        [Tooltip("How long the sentry looks around at a recorded stop. Fixed on purpose: replaying the player's actual pause lengths would let them camp in phase 1 to pad the patrol out.")]
        [Min(0.1f)]
        public float lookAroundSeconds = 3f;

        [Header("Debug")]
        [Tooltip("Escape hatch while playtesting: instantly wins the stealth phase.")]
        public Key manualFinishKey = Key.Enter;

        public Key debugToggleKey = Key.F3;

        /// <summary>
        /// Runtime fallback so a missing asset never blocks a playtest -- every consumer
        /// calls this rather than dereferencing a possibly-null settings reference.
        /// </summary>
        public static RetraceSettings CreateDefault()
        {
            var settings = CreateInstance<RetraceSettings>();
            settings.name = "RetraceSettings (runtime default)";
            return settings;
        }
    }
}
