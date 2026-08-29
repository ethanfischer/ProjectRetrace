using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>How the retrace phase is allowed to end.</summary>
    public enum RetraceEndMode
    {
        /// <summary>Ends when the player picks the key up again. The default.</summary>
        KeyPickup,

        /// <summary>Ends after phase-1 duration * <see cref="RetraceSettings.timeLimitMultiplier"/>.</summary>
        TimeLimit,

        /// <summary>Ends when the player presses <see cref="RetraceSettings.manualFinishKey"/>.</summary>
        Manual
    }

    /// <summary>
    /// Tuning for the breadcrumb trail and the score. dotSpacing and collectRadius are the
    /// difficulty curve -- they are the two numbers worth fiddling with during playtests.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectRetrace/Retrace Settings", fileName = "RetraceSettings")]
    public class RetraceSettings : ScriptableObject
    {
        [Header("Trail")]
        [Tooltip("Metres of travel between breadcrumbs. Smaller = finer-grained, harsher scoring.")]
        [Min(0.1f)]
        public float dotSpacing = 1.5f;

        [Tooltip("How close the player must pass to collect a breadcrumb. Larger = more forgiving. " +
                 "Above ~1.5m coverage saturates at 100% for any vaguely-close route and stops " +
                 "discriminating, leaving efficiency to carry the whole score -- so widen this only " +
                 "if playtests feel harsh.")]
        [Min(0.1f)]
        public float collectRadius = 1.0f;

        [Header("Ending the retrace")]
        public RetraceEndMode endMode = RetraceEndMode.KeyPickup;

        [Tooltip("TimeLimit mode only: phase 2 gets phase-1 duration * this.")]
        [Min(0.1f)]
        public float timeLimitMultiplier = 1.5f;

        [Tooltip("Manual mode only, and always available as an escape hatch while playtesting.")]
        public Key manualFinishKey = Key.Enter;

        [Header("Debug")]
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
