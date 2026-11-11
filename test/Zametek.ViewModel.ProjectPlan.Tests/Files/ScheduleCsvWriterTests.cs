using Shouldly;
using System;
using System.IO;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The data of a compiled scenario as CSV files: the table of the critical path method, the series of the resources and the
    /// series of the earned value. The numbers are in the invariant culture, a line ends with a line feed, and a field with a
    /// comma or a quote is quoted.
    /// </summary>
    public class ScheduleCsvWriterTests
    {
        private static string Write(Action<TextWriter> write)
        {
            using var writer = new StringWriter();
            write(writer);
            return writer.ToString();
        }

        private static string[] Lines(string text) => text.TrimEnd('\n').Split('\n');

        [Fact]
        public void WriteSchedule_Given_ActivitiesOutOfOrder_Then_ARowForEachInTheOrderOfTheIdsWithEachValueOfTheMethod()
        {
            DependentActivityModel[] activities =
            [
                new DependentActivityModel
                {
                    Activity = new ActivityModel
                    {
                        Id = 3,
                        Name = "Build, test",
                        Duration = 4,
                        EarliestStartTime = 5,
                        EarliestFinishTime = 9,
                        LatestStartTime = 6,
                        LatestFinishTime = 10,
                        TotalSlack = 1,
                        FreeSlack = 1,
                        AllocatedToResources = [2, 1],
                        MaximumLatestFinishTime = 12,
                    },
                    Dependencies = [2, 1],
                    ResourceDependencies = [7],
                },
                new DependentActivityModel
                {
                    Activity = new ActivityModel
                    {
                        Id = 1,
                        Name = "Design",
                        Duration = 5,
                        EarliestStartTime = 0,
                        EarliestFinishTime = 5,
                        LatestStartTime = 0,
                        LatestFinishTime = 5,
                        TotalSlack = 0,
                        FreeSlack = 0,
                        MinimumEarliestStartTime = 0,
                    },
                },
            ];

            string[] lines = Lines(Write(w => ScheduleCsvWriter.WriteSchedule(w, activities)));

            lines.ShouldBe(
            [
                "Id,Name,Duration,EarliestStartTime,EarliestFinishTime,LatestStartTime,LatestFinishTime,TotalSlack,FreeSlack,Dependencies,ResourceDependencies,AllocatedToResources,MinimumEarliestStartTime,MaximumLatestFinishTime",
                "1,Design,5,0,5,0,5,0,0,,,,0,",
                "3,\"Build, test\",4,5,9,6,10,1,1,1 2,7,1 2,,12",
            ]);
        }

        [Fact]
        public void WriteSchedule_Given_AnActivityThatDidNotCompile_Then_ItsTimesAreEmpty()
        {
            DependentActivityModel[] activities = [new DependentActivityModel { Activity = new ActivityModel { Id = 1, Name = "A", Duration = 2 } }];

            Lines(Write(w => ScheduleCsvWriter.WriteSchedule(w, activities)))[1].ShouldBe("1,A,2,,,,,,,,,,,");
        }

        [Fact]
        public void WriteResourceSeries_Given_TwoResources_Then_ARowForEachDayAndResourceInTheDisplayOrder()
        {
            var seriesSet = new ResourceSeriesSetModel
            {
                Combined =
                [
                    new ResourceSeriesModel
                    {
                        Title = "Tester",
                        DisplayOrder = 2,
                        UnitCost = 12.5,
                        ResourceSchedule = new ResourceScheduleModel
                        {
                            Resource = new ResourceModel { Id = 2 },
                            ResourceAllocation = [true, true],
                            ActivityAllocation = [false, true],
                            EffortAllocation = [false, true],
                            CostAllocation = [true, true],
                        },
                    },
                    new ResourceSeriesModel
                    {
                        Title = "Unscheduled",
                        DisplayOrder = 1,
                        UnitCost = 10.0,
                        ResourceSchedule = new ResourceScheduleModel
                        {
                            ResourceAllocation = [true],
                            ActivityAllocation = [true],
                            EffortAllocation = [true],
                            CostAllocation = [true],
                        },
                    },
                ],
            };

            string[] lines = Lines(Write(w => ScheduleCsvWriter.WriteResourceSeries(w, seriesSet)));

            // The days are those of the longest series, and a shorter series has nothing on the days after its end. The series
            // of no resource of the plan has no id.
            lines.ShouldBe(
            [
                "Time,ResourceId,Resource,Allocated,Activity,Effort,Cost",
                "0,,Unscheduled,true,true,true,10",
                "0,2,Tester,true,false,false,12.5",
                "1,,Unscheduled,false,false,false,0",
                "1,2,Tester,true,true,true,12.5",
            ]);
        }

        [Fact]
        public void WriteResourceSeries_Given_NoSeries_Then_OnlyTheTitles()
        {
            Lines(Write(w => ScheduleCsvWriter.WriteResourceSeries(w, new ResourceSeriesSetModel()))).ShouldBe(
                ["Time,ResourceId,Resource,Allocated,Activity,Effort,Cost"]);
        }

        [Fact]
        public void WriteEarnedValueSeries_Given_ThePlanTheEffortAndTheProgress_Then_EachRowNamesItsSeries()
        {
            var trackingSeriesSet = new TrackingSeriesSetModel
            {
                Plan =
                [
                    new TrackingPointModel { Time = 0, ActivityId = 1, ActivityName = "Design", Value = 2.5, ValuePercentage = 25.0 },
                    new TrackingPointModel { Time = 1, ActivityId = 3, ActivityName = "Build, test", Value = 10.0, ValuePercentage = 100.0 },
                ],
                Effort = [new TrackingPointModel { Time = 0, ActivityId = 1, ActivityName = "Design", Value = 1.0, ValuePercentage = 10.0 }],
                Progress = [new TrackingPointModel { Time = 0, ActivityId = 1, ActivityName = "Design", Value = 2.0, ValuePercentage = 20.0 }],
                PlanProjection = [new TrackingPointModel { Time = 9, ActivityId = 9, ActivityName = "Not written" }],
            };

            string[] lines = Lines(Write(w => ScheduleCsvWriter.WriteEarnedValueSeries(w, trackingSeriesSet)));

            lines.ShouldBe(
            [
                "Series,Time,ActivityId,ActivityName,Value,ValuePercentage",
                "Plan,0,1,Design,2.5,25",
                "Plan,1,3,\"Build, test\",10,100",
                "Effort,0,1,Design,1,10",
                "Progress,0,1,Design,2,20",
            ]);
        }
    }
}
