using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class ObservableExtensions
    {
        /// <summary>
        /// Suppresses emissions from the source sequence while the latest value of the
        /// gate sequence is true. The most recent suppressed emission (if any) is replayed
        /// once when the gate reverts to false. Emissions pass straight through while the
        /// gate is false.
        /// </summary>
        /// <typeparam name="T">The type of the source sequence.</typeparam>
        /// <param name="source">The sequence to be muted.</param>
        /// <param name="gate">The sequence controlling the mute; true mutes, false unmutes.</param>
        /// <returns>The gated sequence.</returns>
        public static IObservable<T> MuteWhile<T>(
            this IObservable<T> source,
            IObservable<bool> gate)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(gate);
            return Observable.Create<T>(observer =>
            {
                Lock @lock = new();
                bool muted = false;
                bool hasPendingValue = false;
                T? pendingValue = default;

                // Subscribe to the gate first so that its initial value, if any,
                // is in place before the source produces its initial emission.
                IDisposable gateSubscription = gate
                    .DistinctUntilChanged()
                    .Subscribe(isMuted =>
                    {
                        T? replayValue = default;
                        bool replay = false;
                        lock (@lock)
                        {
                            muted = isMuted;
                            if (!muted && hasPendingValue)
                            {
                                replayValue = pendingValue;
                                pendingValue = default;
                                hasPendingValue = false;
                                replay = true;
                            }
                        }
                        if (replay)
                        {
                            observer.OnNext(replayValue!);
                        }
                    });

                IDisposable sourceSubscription = source.Subscribe(
                    value =>
                    {
                        bool emit = false;
                        lock (@lock)
                        {
                            if (muted)
                            {
                                pendingValue = value;
                                hasPendingValue = true;
                            }
                            else
                            {
                                emit = true;
                            }
                        }
                        if (emit)
                        {
                            observer.OnNext(value);
                        }
                    },
                    observer.OnError,
                    observer.OnCompleted);

                return new CompositeDisposable(gateSubscription, sourceSubscription);
            });
        }
    }
}
