using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for TimesheetHelper. The classification thresholds pin the day
    /// total colouring behaviour: no bookings at all is neutral, anything
    /// below a full day is under-booked (including an explicit zero), exactly
    /// a full day is full, and anything beyond is over-booked.
    /// </summary>
    public class TimesheetHelperTests
    {
        [Fact]
        public void Classify_Given_Null_Then_None()
        {
            TimesheetHelper.Classify(null).ShouldBe(TimesheetDayLoad.None);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(99)]
        public void Classify_Given_BelowFullDay_Then_Under(int total)
        {
            TimesheetHelper.Classify(total).ShouldBe(TimesheetDayLoad.Under);
        }

        [Fact]
        public void Classify_Given_ExactlyFullDay_Then_Full()
        {
            TimesheetHelper.Classify(TimesheetHelper.c_FullDayPercentage).ShouldBe(TimesheetDayLoad.Full);
        }

        [Theory]
        [InlineData(101)]
        [InlineData(150)]
        [InlineData(200)]
        public void Classify_Given_BeyondFullDay_Then_Over(int total)
        {
            TimesheetHelper.Classify(total).ShouldBe(TimesheetDayLoad.Over);
        }

        [Fact]
        public void BuildActivityLabel_Given_Name_Then_IdAndName()
        {
            TimesheetHelper.BuildActivityLabel(12, @"Backend").ShouldBe(@"12 - Backend");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void BuildActivityLabel_Given_NoName_Then_IdOnly(string? name)
        {
            TimesheetHelper.BuildActivityLabel(12, name).ShouldBe(@"12");
        }
    }
}
