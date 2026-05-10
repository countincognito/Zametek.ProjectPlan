using Shouldly;
using System;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the DateTimeHelper extension methods. All six public methods
    /// are pure comparisons, so every branch can be exercised with simple values.
    /// </summary>
    public class DateTimeHelperTests
    {
        #region DateOnly.IsBeforeOrOn

        [Fact]
        public void DateOnly_IsBeforeOrOn_When_Equal_Returns_True()
        {
            var d = new DateOnly(2025, 6, 10);
            d.IsBeforeOrOn(d).ShouldBeTrue();
        }

        [Fact]
        public void DateOnly_IsBeforeOrOn_When_Before_Returns_True()
        {
            var earlier = new DateOnly(2025, 6, 9);
            var later   = new DateOnly(2025, 6, 10);
            earlier.IsBeforeOrOn(later).ShouldBeTrue();
        }

        [Fact]
        public void DateOnly_IsBeforeOrOn_When_After_Returns_False()
        {
            var earlier = new DateOnly(2025, 6, 9);
            var later   = new DateOnly(2025, 6, 10);
            later.IsBeforeOrOn(earlier).ShouldBeFalse();
        }

        #endregion

        #region DateOnly.IsAfterOrOn

        [Fact]
        public void DateOnly_IsAfterOrOn_When_Equal_Returns_True()
        {
            var d = new DateOnly(2025, 6, 10);
            d.IsAfterOrOn(d).ShouldBeTrue();
        }

        [Fact]
        public void DateOnly_IsAfterOrOn_When_After_Returns_True()
        {
            var earlier = new DateOnly(2025, 6, 9);
            var later   = new DateOnly(2025, 6, 10);
            later.IsAfterOrOn(earlier).ShouldBeTrue();
        }

        [Fact]
        public void DateOnly_IsAfterOrOn_When_Before_Returns_False()
        {
            var earlier = new DateOnly(2025, 6, 9);
            var later   = new DateOnly(2025, 6, 10);
            earlier.IsAfterOrOn(later).ShouldBeFalse();
        }

        #endregion

        #region DateTime.IsBeforeOrOn

        [Fact]
        public void DateTime_IsBeforeOrOn_When_Equal_Returns_True()
        {
            var dt = new DateTime(2025, 6, 10, 12, 0, 0);
            dt.IsBeforeOrOn(dt).ShouldBeTrue();
        }

        [Fact]
        public void DateTime_IsBeforeOrOn_When_Before_Returns_True()
        {
            var earlier = new DateTime(2025, 6, 10, 11, 59, 59);
            var later   = new DateTime(2025, 6, 10, 12, 0, 0);
            earlier.IsBeforeOrOn(later).ShouldBeTrue();
        }

        [Fact]
        public void DateTime_IsBeforeOrOn_When_After_Returns_False()
        {
            var earlier = new DateTime(2025, 6, 10, 11, 59, 59);
            var later   = new DateTime(2025, 6, 10, 12, 0, 0);
            later.IsBeforeOrOn(earlier).ShouldBeFalse();
        }

        #endregion

        #region DateTime.IsAfterOrOn

        [Fact]
        public void DateTime_IsAfterOrOn_When_Equal_Returns_True()
        {
            var dt = new DateTime(2025, 6, 10, 12, 0, 0);
            dt.IsAfterOrOn(dt).ShouldBeTrue();
        }

        [Fact]
        public void DateTime_IsAfterOrOn_When_After_Returns_True()
        {
            var earlier = new DateTime(2025, 6, 10, 11, 0, 0);
            var later   = new DateTime(2025, 6, 10, 12, 0, 0);
            later.IsAfterOrOn(earlier).ShouldBeTrue();
        }

        [Fact]
        public void DateTime_IsAfterOrOn_When_Before_Returns_False()
        {
            var earlier = new DateTime(2025, 6, 10, 11, 0, 0);
            var later   = new DateTime(2025, 6, 10, 12, 0, 0);
            earlier.IsAfterOrOn(later).ShouldBeFalse();
        }

        #endregion

        #region DateTimeOffset.IsBefore

        [Fact]
        public void DateTimeOffset_IsBefore_When_Before_Returns_True()
        {
            var earlier = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
            var later   = new DateTimeOffset(2025, 6, 11, 0, 0, 0, TimeSpan.Zero);
            earlier.IsBefore(later).ShouldBeTrue();
        }

        [Fact]
        public void DateTimeOffset_IsBefore_When_Equal_Returns_False()
        {
            var d = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
            d.IsBefore(d).ShouldBeFalse();
        }

        [Fact]
        public void DateTimeOffset_IsBefore_When_After_Returns_False()
        {
            var earlier = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
            var later   = new DateTimeOffset(2025, 6, 11, 0, 0, 0, TimeSpan.Zero);
            later.IsBefore(earlier).ShouldBeFalse();
        }

        #endregion

        #region DateTimeOffset.IsAfter

        [Fact]
        public void DateTimeOffset_IsAfter_When_After_Returns_True()
        {
            var earlier = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
            var later   = new DateTimeOffset(2025, 6, 11, 0, 0, 0, TimeSpan.Zero);
            later.IsAfter(earlier).ShouldBeTrue();
        }

        [Fact]
        public void DateTimeOffset_IsAfter_When_Equal_Returns_False()
        {
            var d = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
            d.IsAfter(d).ShouldBeFalse();
        }

        [Fact]
        public void DateTimeOffset_IsAfter_When_Before_Returns_False()
        {
            var earlier = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
            var later   = new DateTimeOffset(2025, 6, 11, 0, 0, 0, TimeSpan.Zero);
            earlier.IsAfter(later).ShouldBeFalse();
        }

        #endregion

        #region DateTimeOffset.IsAfterOrOn

        [Fact]
        public void DateTimeOffset_IsAfterOrOn_When_Equal_Returns_True()
        {
            var d = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
            d.IsAfterOrOn(d).ShouldBeTrue();
        }

        [Fact]
        public void DateTimeOffset_IsAfterOrOn_When_After_Returns_True()
        {
            var earlier = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
            var later   = new DateTimeOffset(2025, 6, 11, 0, 0, 0, TimeSpan.Zero);
            later.IsAfterOrOn(earlier).ShouldBeTrue();
        }

        [Fact]
        public void DateTimeOffset_IsAfterOrOn_When_Before_Returns_False()
        {
            var earlier = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
            var later   = new DateTimeOffset(2025, 6, 11, 0, 0, 0, TimeSpan.Zero);
            earlier.IsAfterOrOn(later).ShouldBeFalse();
        }

        #endregion
    }
}
