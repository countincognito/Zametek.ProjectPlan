namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The rules for the three estimates of an activity's duration: the optimistic duration (O), the
    /// most likely duration (ML) and the pessimistic duration (P).
    /// </summary>
    /// <remarks>
    /// An activity holds its ML and, where the user gave them, its own O and P. An O or a P that the
    /// user did not give is null, and it follows ML. The rule for all of them is
    /// 0 &lt;= O &lt;= ML &lt;= P, and an activity with an ML of 0 (a milestone) has no uncertainty,
    /// so its O and P are 0 too.
    /// <para>
    /// The duration that the compiler schedules is the PERT expected duration, (O + 4 ML + P) / 6,
    /// rounded to the nearest whole day, with halves up. When O = ML = P it is ML, and so an activity
    /// with no estimates of its own is scheduled as it always was.
    /// </para>
    /// </remarks>
    public static class EstimateHelper
    {
        /// <summary>
        /// The PERT expected duration, rounded to the nearest whole day, with halves up.
        /// </summary>
        /// <remarks>
        /// Whole numbers only: (O + 4 ML + P + 3) / 6 rounds a non-negative sum to the nearest sixth,
        /// with halves up, and a floating-point sum could land either side of a half. The estimates
        /// must follow the rule above, and they are the output of <see cref="Effective"/>.
        /// </remarks>
        public static int ExpectedDuration(int optimistic, int mostLikely, int pessimistic)
        {
            return (optimistic + (4 * mostLikely) + pessimistic + 3) / 6;
        }

        /// <summary>
        /// The PERT standard deviation, (P - O) / 6.
        /// </summary>
        public static double StandardDeviation(int optimistic, int pessimistic)
        {
            return (pessimistic - optimistic) / 6.0;
        }

        /// <summary>
        /// The estimates that an activity has, from its ML and the O and P that it holds of its own.
        /// A value that is null follows ML, and a value outside the rule is moved to the nearest
        /// value that is inside it.
        /// </summary>
        public static (int Optimistic, int MostLikely, int Pessimistic) Effective(
            int mostLikely,
            int? ownOptimistic,
            int? ownPessimistic)
        {
            int ml = Math.Max(0, mostLikely);

            if (ml == 0)
            {
                return (0, 0, 0);
            }

            int o = Math.Clamp(ownOptimistic ?? ml, 0, ml);
            int p = Math.Max(ownPessimistic ?? ml, ml);
            return (o, ml, p);
        }

        /// <summary>
        /// The O and P that an activity keeps of its own when its ML becomes the given value: a value
        /// that ML has crossed moves to ML, and a value that is null stays null.
        /// </summary>
        public static (int? Optimistic, int? Pessimistic) PullToMostLikely(
            int mostLikely,
            int? ownOptimistic,
            int? ownPessimistic)
        {
            int ml = Math.Max(0, mostLikely);

            if (ml == 0)
            {
                return (ownOptimistic.HasValue ? 0 : null, ownPessimistic.HasValue ? 0 : null);
            }

            return (
                ownOptimistic.HasValue ? Math.Clamp(ownOptimistic.Value, 0, ml) : null,
                ownPessimistic.HasValue ? Math.Max(ownPessimistic.Value, ml) : null);
        }
    }
}
