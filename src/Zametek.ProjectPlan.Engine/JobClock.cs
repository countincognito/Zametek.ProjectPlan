namespace Zametek.ProjectPlan.Engine
{
    // A job's clock: the host's, unless the job's request fixes the time it runs at, in which case every reading gives
    // that instant. Either way it keeps the host's time zone, and its timers and timestamps are the host's - only what
    // time it is can be fixed, not how time passes. Every job has its own, so no job's time reaches another.
    internal sealed class JobClock
        : TimeProvider
    {
        private readonly TimeProvider m_HostClock;
        private DateTimeOffset? m_Now;

        public JobClock(TimeProvider hostClock)
        {
            ArgumentNullException.ThrowIfNull(hostClock);
            m_HostClock = hostClock;
        }

        // The time the job runs at, or null to read the host's clock.
        public DateTimeOffset? Now
        {
            get => m_Now;
            set => m_Now = value;
        }

        public override DateTimeOffset GetUtcNow() => m_Now?.ToUniversalTime() ?? m_HostClock.GetUtcNow();

        public override TimeZoneInfo LocalTimeZone => m_HostClock.LocalTimeZone;

        public override long TimestampFrequency => m_HostClock.TimestampFrequency;

        public override long GetTimestamp() => m_HostClock.GetTimestamp();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            m_HostClock.CreateTimer(callback, state, dueTime, period);
    }
}
