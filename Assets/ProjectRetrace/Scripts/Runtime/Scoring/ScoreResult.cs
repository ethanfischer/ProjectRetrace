namespace ProjectRetrace
{
    /// <summary>
    /// The outcome of one full run. Kept as a plain struct and separated from the UI so the
    /// IMGUI results screen can be swapped for real uGUI without touching the scoring.
    /// </summary>
    public struct ScoreResult
    {
        public int Matched1;
        public int Total1;
        public int Matched2;
        public int Total2;
        public float Phase1Distance;
        public float Phase2Distance;

        /// <summary>Fraction of round-1 marks the retrace passed over, 0..1. Punishes missing
        /// parts of the original route.</summary>
        public float Coverage;

        /// <summary>Fraction of round-2 marks that landed on the original path, 0..1. Punishes
        /// extra breadcrumbs from wandering or taking a different route.</summary>
        public float Precision;

        /// <summary>Coverage * Precision, 0..1.</summary>
        public float Final;

        public int Percent => ToPercent(Final);

        public string Grade
        {
            get
            {
                return Final switch
                {
                    >= 1f => "A+",
                    >= 0.90f => "A",
                    >= 0.80f => "B",
                    >= 0.70f => "C",
                    >= 0.60f => "D",
                    _ => "F"
                };
            }
        }

        private static int ToPercent(float value)
        {
            return UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Clamp01(value) * 100f);
        }
    }
}
