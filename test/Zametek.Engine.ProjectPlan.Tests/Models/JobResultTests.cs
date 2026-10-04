using Shouldly;
using Xunit;

namespace Zametek.Engine.ProjectPlan.Tests
{
    public class JobResultTests
    {
        private static JobResult Failed(params JobCompilationError[] errors)
        {
            return new JobResult
            {
                Status = JobStatus.CompilationErrors,
                CompilationOutput = @">Compilation errors",
                CompilationErrors = errors,
            };
        }

        [Fact]
        public void Equals_Given_TheSameErrorsInTwoLists_Then_AreEqualWithTheSameHashCode()
        {
            // The lists are two, with the errors one by one the same: the results say the same thing.
            JobResult first = Failed(new JobCompilationError(@"P0010", @"One"), new JobCompilationError(@"P0020", @"Two"));
            JobResult second = Failed(new JobCompilationError(@"P0010", @"One"), new JobCompilationError(@"P0020", @"Two"));

            first.ShouldBe(second);
            first.GetHashCode().ShouldBe(second.GetHashCode());
        }

        [Fact]
        public void Equals_Given_ADifferentError_Then_AreNotEqual()
        {
            JobResult first = Failed(new JobCompilationError(@"P0010", @"One"));

            first.ShouldNotBe(Failed(new JobCompilationError(@"P0010", @"Other")));
            first.ShouldNotBe(Failed(new JobCompilationError(@"P0011", @"One")));
        }

        [Fact]
        public void Equals_Given_ErrorsInAnotherOrderOrAnotherNumber_Then_AreNotEqual()
        {
            var one = new JobCompilationError(@"P0010", @"One");
            var two = new JobCompilationError(@"P0020", @"Two");

            Failed(one, two).ShouldNotBe(Failed(two, one));
            Failed(one, two).ShouldNotBe(Failed(one));
        }

        [Fact]
        public void Equals_Given_AnotherStatusOutputOrMetrics_Then_AreNotEqual()
        {
            var result = new JobResult { Status = JobStatus.Succeeded, CompilationOutput = @"a", Metrics = new JobMetrics { ActivityRisk = 1 } };

            result.ShouldBe(new JobResult { Status = JobStatus.Succeeded, CompilationOutput = @"a", Metrics = new JobMetrics { ActivityRisk = 1 } });
            result.ShouldNotBe(result with { Status = JobStatus.CompletedWithErrors });
            result.ShouldNotBe(result with { CompilationOutput = @"b" });
            result.ShouldNotBe(result with { Metrics = new JobMetrics { ActivityRisk = 2 } });
            result.ShouldNotBe(result with { Metrics = null });
        }

        [Fact]
        public void Equals_Given_Null_Then_IsNotEqual()
        {
            new JobResult().Equals((JobResult?)null).ShouldBeFalse();
        }
    }
}
