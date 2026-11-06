using System;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // A clock that always reads the same instant, in the time zone it is given - so a test can say what time it is,
    // and where, rather than depend on the machine it runs on.
    internal sealed class FixedTimeProvider(DateTimeOffset now, TimeZoneInfo localTimeZone)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => localTimeZone;
    }
}
