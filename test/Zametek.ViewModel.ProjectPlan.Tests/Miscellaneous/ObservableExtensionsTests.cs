using Shouldly;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for ObservableExtensions.MuteWhile. All sequences are driven
    /// synchronously with subjects, so no scheduler is required.
    /// </summary>
    public class ObservableExtensionsTests
    {
        [Fact]
        public void MuteWhile_GateNeverEmits_ValuesPassThrough()
        {
            using var source = new Subject<int>();
            using var gate = new Subject<bool>();
            List<int> results = [];

            using IDisposable sub = source.MuteWhile(gate).Subscribe(results.Add);

            source.OnNext(1);
            source.OnNext(2);

            results.ShouldBe([1, 2]);
        }

        [Fact]
        public void MuteWhile_GateIsFalse_ValuesPassThrough()
        {
            using var source = new Subject<int>();
            using var gate = new BehaviorSubject<bool>(false);
            List<int> results = [];

            using IDisposable sub = source.MuteWhile(gate).Subscribe(results.Add);

            source.OnNext(1);
            source.OnNext(2);

            results.ShouldBe([1, 2]);
        }

        [Fact]
        public void MuteWhile_GateIsTrue_ValuesAreSuppressed()
        {
            using var source = new Subject<int>();
            using var gate = new BehaviorSubject<bool>(true);
            List<int> results = [];

            using IDisposable sub = source.MuteWhile(gate).Subscribe(results.Add);

            source.OnNext(1);
            source.OnNext(2);

            results.ShouldBeEmpty();
        }

        [Fact]
        public void MuteWhile_GateRevertsToFalse_LatestSuppressedValueIsReplayedOnce()
        {
            using var source = new Subject<int>();
            using var gate = new BehaviorSubject<bool>(false);
            List<int> results = [];

            using IDisposable sub = source.MuteWhile(gate).Subscribe(results.Add);

            source.OnNext(1);
            gate.OnNext(true);
            source.OnNext(2);
            source.OnNext(3);
            gate.OnNext(false);

            results.ShouldBe([1, 3]);
        }

        [Fact]
        public void MuteWhile_GateRevertsToFalseWithNoSuppressedValues_NothingIsReplayed()
        {
            using var source = new Subject<int>();
            using var gate = new BehaviorSubject<bool>(false);
            List<int> results = [];

            using IDisposable sub = source.MuteWhile(gate).Subscribe(results.Add);

            source.OnNext(1);
            gate.OnNext(true);
            gate.OnNext(false);

            results.ShouldBe([1]);
        }

        [Fact]
        public void MuteWhile_AfterUnmute_ValuesPassThroughAgain()
        {
            using var source = new Subject<int>();
            using var gate = new BehaviorSubject<bool>(false);
            List<int> results = [];

            using IDisposable sub = source.MuteWhile(gate).Subscribe(results.Add);

            gate.OnNext(true);
            source.OnNext(1);
            gate.OnNext(false);
            source.OnNext(2);

            results.ShouldBe([1, 2]);
        }

        [Fact]
        public void MuteWhile_RepeatedGateValues_DoNotReplayTwice()
        {
            using var source = new Subject<int>();
            using var gate = new BehaviorSubject<bool>(false);
            List<int> results = [];

            using IDisposable sub = source.MuteWhile(gate).Subscribe(results.Add);

            gate.OnNext(true);
            source.OnNext(1);
            gate.OnNext(false);
            gate.OnNext(false);

            results.ShouldBe([1]);
        }

        [Fact]
        public void MuteWhile_RepeatedMuteCycles_EachReplaysItsOwnLatestValue()
        {
            using var source = new Subject<int>();
            using var gate = new BehaviorSubject<bool>(false);
            List<int> results = [];

            using IDisposable sub = source.MuteWhile(gate).Subscribe(results.Add);

            gate.OnNext(true);
            source.OnNext(1);
            source.OnNext(2);
            gate.OnNext(false);

            gate.OnNext(true);
            source.OnNext(3);
            source.OnNext(4);
            gate.OnNext(false);

            results.ShouldBe([2, 4]);
        }

        [Fact]
        public void MuteWhile_NullSource_Throws()
        {
            using var gate = new Subject<bool>();
            IObservable<int> source = null!;

            Should.Throw<ArgumentNullException>(() => source.MuteWhile(gate));
        }

        [Fact]
        public void MuteWhile_NullGate_Throws()
        {
            using var source = new Subject<int>();

            Should.Throw<ArgumentNullException>(() => source.MuteWhile(null!));
        }
    }
}
