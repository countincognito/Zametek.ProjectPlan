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
        /// <remarks>
        /// <para>
        /// Conceptually the operator merges two incoming streams - source values and gate
        /// values - into one outgoing stream of source values. The gate stream never
        /// produces output itself; it only changes how source values are treated:
        /// </para>
        /// <para>
        /// - Gate is false (unmuted): each source value is forwarded to the observer
        ///   immediately, on the thread that emitted it.
        /// </para>
        /// <para>
        /// - Gate is true (muted): source values are not forwarded. Instead, only the most
        ///   recent one is remembered, each new arrival overwriting the last (conflation).
        /// </para>
        /// <para>
        /// - Gate transitions true to false (falling edge): if any source value arrived
        ///   while muted, the remembered one is forwarded once, on the thread that changed
        ///   the gate. This acts as the single "active trigger" at the end of a mute
        ///   window, so downstream subscribers catch up without one call per suppressed
        ///   emission. If nothing arrived while muted, the falling edge forwards nothing.
        /// </para>
        /// <para>
        /// All decisions are made at emission time, i.e. synchronously on the thread that
        /// pushes each value, NOT when a downstream handler eventually runs. To gate a
        /// deferred pipeline correctly, place this operator BEFORE any ObserveOn call;
        /// placed after, the mute check would run at delivery time, when the mute window
        /// may already have closed.
        /// </para>
        /// <para>
        /// Note that the replayed value is a snapshot taken at its original emission time.
        /// Downstream handlers that need current state (as the Build* manager subscriptions
        /// do) should read it live rather than rely on the replayed payload.
        /// </para>
        /// </remarks>
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

            // Observable.Create runs this factory once per subscription, so each
            // subscriber gets its own private copy of the state below.
            return Observable.Create<T>(observer =>
            {
                // Guards the three state fields below. Source values and gate changes
                // can arrive concurrently on different threads (e.g. a taskpool Build*
                // raising properties while the load thread ends a bulk update), so every
                // read-modify-write of the state must be atomic. The lock is only ever
                // held for those few field operations - never while calling out to
                // observer.OnNext - so it cannot participate in a deadlock.
                Lock @lock = new();

                // The latest gate value: true while emissions are being suppressed.
                // Written by the gate subscription; read by the source subscription
                // to decide whether to forward or conflate each value.
                bool muted = false;

                // True when pendingValue holds a real suppressed emission. Needed as a
                // separate flag because default(T) is indistinguishable from a genuine
                // value; it also tells the falling edge whether there is anything to
                // replay at all.
                bool hasPendingValue = false;

                // The most recent value emitted by the source while muted. Each new
                // arrival overwrites the previous one (only the latest matters, since
                // downstream Build* handlers read live state anyway). Cleared once
                // replayed so a later mute/unmute cycle cannot resurrect a stale value.
                T? pendingValue = default;

                // Subscribe to the gate first so that its initial value, if any (e.g.
                // a BehaviorSubject or WhenAnyValue seed), is in place before the source
                // produces its initial emission; otherwise that first source value could
                // slip past a gate that should already be closed.
                IDisposable gateSubscription = gate
                    // Only react to actual transitions; a gate that re-raises true (or
                    // false) repeatedly must not replay the pending value twice.
                    .DistinctUntilChanged()
                    .Subscribe(isMuted =>
                    {
                        // Captures the value to replay on a falling edge. The value is
                        // copied out of the shared state under the lock, then forwarded
                        // after the lock is released.
                        T? replayValue = default;

                        // True when this gate change is a falling edge with a pending
                        // value to forward.
                        bool replay = false;

                        lock (@lock)
                        {
                            muted = isMuted;

                            // Falling edge: take the pending value (if one arrived while
                            // muted) and reset the conflation state.
                            if (!muted && hasPendingValue)
                            {
                                replayValue = pendingValue;
                                pendingValue = default;
                                hasPendingValue = false;
                                replay = true;
                            }
                        }

                        // Forward outside the lock: observer.OnNext runs arbitrary
                        // downstream code (schedulers, handlers) that must never execute
                        // while this operator's internal lock is held.
                        if (replay)
                        {
                            observer.OnNext(replayValue!);
                        }
                    });

                IDisposable sourceSubscription = source.Subscribe(
                    value =>
                    {
                        // True when the gate is open and this value should be forwarded
                        // as-is. Decided under the lock; acted on after it is released.
                        bool emit = false;

                        lock (@lock)
                        {
                            if (muted)
                            {
                                // Muted: conflate. Remember only this latest value and
                                // swallow the emission.
                                pendingValue = value;
                                hasPendingValue = true;
                            }
                            else
                            {
                                // Unmuted: pass straight through.
                                emit = true;
                            }
                        }

                        // As above: never call into the observer while holding the lock.
                        if (emit)
                        {
                            observer.OnNext(value);
                        }
                    },
                    observer.OnError,
                    observer.OnCompleted);

                // Disposing the outer subscription tears down both inner ones.
                return new CompositeDisposable(gateSubscription, sourceSubscription);
            });
        }
    }
}
