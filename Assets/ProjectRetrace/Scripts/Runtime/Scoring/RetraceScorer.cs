using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Overlap scoring between the two trails: coverage x precision.
    ///
    /// The two terms pin the retrace from both sides. Coverage asks "did you pass over the
    /// original path?" -- shortcut the route and it drops. Precision asks "did you drop marks
    /// anywhere else?" -- wander or take a different route and phase 2 accumulates unmatched
    /// breadcrumbs, and it drops. Coverage alone cannot see wandering because passing over
    /// marks is monotonic: extra motion never costs anything there.
    /// </summary>
    public static class RetraceScorer
    {
        public static ScoreResult Score(int matched1, int total1, int matched2, int total2, float phase1Distance, float phase2Distance)
        {
            var result = new ScoreResult
            {
                Matched1 = matched1,
                Total1 = total1,
                Matched2 = matched2,
                Total2 = total2,
                Phase1Distance = phase1Distance,
                Phase2Distance = phase2Distance,
                Coverage = total1 > 0 ? Mathf.Clamp01((float)matched1 / total1) : 0f,
                Precision = total2 > 0 ? Mathf.Clamp01((float)matched2 / total2) : 0f,
            };

            result.Final = result.Coverage * result.Precision;
            return result;
        }
    }
}
