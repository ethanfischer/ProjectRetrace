using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Coverage x efficiency scoring.
    ///
    /// The two terms pin the path from both sides: shortcut the route and coverage drops;
    /// wander -- sprint the whole house hoovering up breadcrumbs -- and efficiency drops.
    /// You have to do both to score well. Coverage alone cannot see wandering, because
    /// collecting breadcrumbs is monotonic: extra motion never costs anything.
    /// </summary>
    public static class RetraceScorer
    {
        /// <summary>Distances below this are treated as "did not move" rather than divided by.</summary>
        private const float MinimumDistance = 0.01f;

        public static ScoreResult Score(int collected, int total, float phase1Distance, float phase2Distance)
        {
            var result = new ScoreResult
            {
                Collected = collected,
                Total = total,
                Phase1Distance = phase1Distance,
                Phase2Distance = phase2Distance,
                Coverage = total > 0 ? Mathf.Clamp01((float)collected / total) : 0f,
                Efficiency = ComputeEfficiency(phase1Distance, phase2Distance)
            };

            result.Final = result.Coverage * result.Efficiency;
            return result;
        }

        /// <summary>
        /// Walking further than the original route scales the score down proportionally.
        /// Walking *less* clamps to 1 rather than rewarding a shortcut -- the missing
        /// breadcrumbs already cost you on the coverage side, so it must not pay twice.
        /// </summary>
        private static float ComputeEfficiency(float phase1Distance, float phase2Distance)
        {
            if (phase2Distance <= MinimumDistance)
            {
                // Standing still must not divide by ~0 and score a perfect 1.
                return phase1Distance <= MinimumDistance ? 1f : 0f;
            }

            return Mathf.Clamp01(phase1Distance / phase2Distance);
        }
    }
}
