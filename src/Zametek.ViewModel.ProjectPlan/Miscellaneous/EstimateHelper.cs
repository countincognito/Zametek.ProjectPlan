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

        /// <summary>
        /// The estimates that an activity has when a drag of the end of its bar on the Gantt chart asks for the given
        /// duration. The drag moves O, ML and P by the same number of days. Thus, the expected duration moves by that number
        /// of days too, and the spread of the estimates stays as it was. An O or a P that follows ML stays null, and so it
        /// moves with ML.
        /// </summary>
        /// <remarks>
        /// The move stops where O reaches 0, or where ML reaches 1, and so a drag never changes the spread. A duration that is
        /// shorter gives the shortest duration that the estimates can have (<see cref="ShortestDurationOfADrag"/>). For an
        /// activity with no estimates of its own, this sets ML to the duration, and not to less than 1, as the drag did before
        /// the estimates existed.
        /// </remarks>
        public static (int MostLikely, int? Optimistic, int? Pessimistic) MoveForDuration(
            int mostLikely,
            int? ownOptimistic,
            int? ownPessimistic,
            int duration)
        {
            (int optimistic, int ml, int pessimistic) = Effective(mostLikely, ownOptimistic, ownPessimistic);
            int days = Math.Max(duration, ShortestDurationOfADrag(mostLikely, ownOptimistic, ownPessimistic))
                - ExpectedDuration(optimistic, ml, pessimistic);

            return (
                ml + days,
                ownOptimistic.HasValue ? optimistic + days : null,
                ownPessimistic.HasValue ? pessimistic + days : null);
        }

        /// <summary>
        /// The shortest duration that a drag of the end of the bar of an activity can give (<see cref="MoveForDuration"/>):
        /// the expected duration where O reaches 0, or where ML reaches 1. It is never less than 1.
        /// </summary>
        public static int ShortestDurationOfADrag(
            int mostLikely,
            int? ownOptimistic,
            int? ownPessimistic)
        {
            (int optimistic, int ml, int pessimistic) = Effective(mostLikely, ownOptimistic, ownPessimistic);

            // Each day that the three estimates move down takes one day off the expected duration. O can move down to 0, and
            // ML to 1. A milestone (ML = 0) can only move up, to 1 day at the least.
            return ExpectedDuration(optimistic, ml, pessimistic) - Math.Min(optimistic, ml - 1);
        }
    }
}
