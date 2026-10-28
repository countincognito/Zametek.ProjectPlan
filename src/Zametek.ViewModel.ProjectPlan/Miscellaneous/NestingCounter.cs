namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Counts the sections of an operation that start inside each other, for a signal that has to change only when the
    /// outermost section starts and when it ends: IsBusy and IsBulkUpdating. The count takes no lock, and it never goes
    /// below zero.
    /// </summary>
    /// <remarks>
    /// Always pair Begin with End, and put End in a finally block.
    /// </remarks>
    internal sealed class NestingCounter
    {
        private int m_Level;

        /// <summary>
        /// True while at least one section is open. The getter takes no lock: the code that raises a signal reads it
        /// again, synchronously, on the thread that raised the signal.
        /// </summary>
        public bool IsActive => Volatile.Read(ref m_Level) > 0;

        /// <summary>
        /// Starts a section. The result is true when it is the outermost one, and then the signal has to change.
        /// </summary>
        public bool Begin() => Interlocked.Increment(ref m_Level) == 1;

        /// <summary>
        /// Ends a section. The result is true when it is the outermost one, and then the signal has to change. A call
        /// when no section is open changes nothing, and its result is false.
        /// </summary>
        public bool End()
        {
            // Decrement defensively, clamping at zero: pairing is expected (Begin
            // at the start of a try block, End in its finally), but an unmatched
            // call must never drive the count negative, which would silently break
            // the signal for the remainder of the session.
            //
            // This is a standard lock-free compare-and-swap retry loop. The initial
            // volatile read is only a first guess and may be stale by the time the
            // CompareExchange runs; that is fine, because CompareExchange writes the
            // decrement only if the field still holds the guessed value, atomically,
            // and otherwise writes nothing and returns the actual current value, with
            // which the loop simply tries again. A stale read can therefore only ever
            // cause a retry, never an incorrect write.
            int currentLevel = Volatile.Read(ref m_Level);
            while (currentLevel > 0)
            {
                int originalLevel = Interlocked.CompareExchange(ref m_Level, currentLevel - 1, currentLevel);
                if (originalLevel == currentLevel)
                {
                    return currentLevel == 1;
                }
                currentLevel = originalLevel;
            }

            return false;
        }
    }
}
