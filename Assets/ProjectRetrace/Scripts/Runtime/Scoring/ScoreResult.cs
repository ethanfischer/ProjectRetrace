namespace ProjectRetrace
{
    /// <summary>
    /// The outcome of one full run. Kept as a plain struct and separated from the UI so the
    /// IMGUI results screen can be swapped for real uGUI without touching the scoring.
    /// </summary>
    public struct ScoreResult
    {
        public int Collected;
        public int Total;
        public float Phase1Distance;
        public float Phase2Distance;

        /// <summary>Fraction of breadcrumbs collected, 0..1. Punishes shortcutting.</summary>
        public float Coverage;

        /// <summary>clamp01(phase1Distance / phase2Distance), 0..1. Punishes wandering.</summary>
        public float Efficiency;

        /// <summary>Coverage * Efficiency, 0..1.</summary>
        public float Final;

        public int Percent => ToPercent(Final);

        public string Grade
        {
            get
            {
                if (Final >= 0.90f) return "S";
                if (Final >= 0.80f) return "A";
                if (Final >= 0.65f) return "B";
                if (Final >= 0.50f) return "C";
                if (Final >= 0.30f) return "D";
                return "F";
            }
        }

        private static int ToPercent(float value)
        {
            return UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Clamp01(value) * 100f);
        }
    }
}
