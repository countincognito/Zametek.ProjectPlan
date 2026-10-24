using System.Collections.Concurrent;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // The jobs that the server runs now, each by its id (the trace id of its request), with how far it has got. A job is in the
    // list from the moment it begins to the moment it ends, and the server keeps nothing of it after that. The list is as long as
    // the jobs that run at one time, which the limits bound, and so a caller cannot make it grow.
    internal sealed class RunningJobs
    {
        private readonly ConcurrentDictionary<string, Registration> m_Jobs = new(StringComparer.Ordinal);
        private readonly TimeProvider m_TimeProvider;

        public RunningJobs(TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            m_TimeProvider = timeProvider;
        }

        // The number of jobs that run now.
        public int Count => m_Jobs.Count;

        // A job begins. What it returns tells the list how far the job has got, and removes the job from the list when it is
        // disposed of. Two jobs can have the same id: a caller can send two requests in one trace. The first to begin has the id
        // for as long as it runs, and the other is not in the list.
        public Registration Begin(string id)
        {
            ArgumentNullException.ThrowIfNull(id);

            var registration = new Registration(this, id, m_TimeProvider.GetUtcNow(), m_TimeProvider.GetTimestamp(), m_TimeProvider);
            registration.IsListed = m_Jobs.TryAdd(id, registration);
            return registration;
        }

        // The status of the job with this id, or null when none runs now.
        public JobStatusResponse? Find(string id)
        {
            ArgumentNullException.ThrowIfNull(id);

            return m_Jobs.TryGetValue(id, out Registration? registration) ? registration.ToStatus() : null;
        }

        private void End(Registration registration)
        {
            if (registration.IsListed)
            {
                // Only the entry of this job: a job that began later with the same id has its own.
                m_Jobs.TryRemove(new KeyValuePair<string, Registration>(registration.Id, registration));
            }
        }

        // One job in the list. It is the progress that the job reports to.
        public sealed class Registration
            : IProgress<JobProgress>, IDisposable
        {
            private readonly RunningJobs m_Owner;
            private readonly DateTimeOffset m_StartedAt;
            private readonly long m_StartedTimestamp;
            private readonly TimeProvider m_TimeProvider;

            // The last report, which a reader reads whole.
            private JobProgress? m_Progress;
            private int m_Ended;

            internal Registration(
                RunningJobs owner,
                string id,
                DateTimeOffset startedAt,
                long startedTimestamp,
                TimeProvider timeProvider)
            {
                m_Owner = owner;
                Id = id;
                m_StartedAt = startedAt;
                m_StartedTimestamp = startedTimestamp;
                m_TimeProvider = timeProvider;
            }

            public string Id { get; }

            // Whether the job is in the list: it is not when another job with the same id is.
            internal bool IsListed { get; set; }

            // The job reports from the threads of its simulation.
            public void Report(JobProgress value)
            {
                Volatile.Write(ref m_Progress, value);
            }

            internal JobStatusResponse ToStatus()
            {
                JobProgress? progress = Volatile.Read(ref m_Progress);

                return new JobStatusResponse(
                    Id,
                    JobState.Running,
                    m_StartedAt,
                    m_TimeProvider.GetElapsedTime(m_StartedTimestamp),
                    progress is null ? null : new JobProgressResponse(progress.Done, progress.Total));
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref m_Ended, 1) == 0)
                {
                    m_Owner.End(this);
                }
            }
        }
    }
}
