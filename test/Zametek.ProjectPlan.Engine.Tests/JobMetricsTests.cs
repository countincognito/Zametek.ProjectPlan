using Shouldly;
using System.Reflection;
using Xunit;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.Engine.Tests
{
    public class JobMetricsTests
    {
        [Fact]
        public void From_Given_MetricManager_Then_CopiesEachValueFromThePropertyOfTheSameName()
        {
            // Every metric the view-model offers has a value of its own here, so a
            // value copied from the wrong property shows up as a mismatch.
            IMetricManagerViewModel metrics = DistinctMetrics.Create();

            JobMetrics snapshot = JobMetrics.From(metrics);

            foreach (PropertyInfo property in typeof(JobMetrics).GetProperties())
            {
                PropertyInfo? source = typeof(IMetricManagerViewModel).GetProperty(property.Name);
                source.ShouldNotBeNull(property.Name);
                source.PropertyType.ShouldBe(property.PropertyType, property.Name);
                property.GetValue(snapshot).ShouldBe(source.GetValue(metrics), property.Name);
            }
        }

        // An IMetricManagerViewModel whose properties each return a value no other
        // property returns: its position in the interface, or its name for text.
        public class DistinctMetrics
            : DispatchProxy
        {
            private static readonly List<string> s_PropertyNames = [.. typeof(IMetricManagerViewModel).GetProperties().Select(x => x.Name)];

            public static IMetricManagerViewModel Create() => Create<IMetricManagerViewModel, DistinctMetrics>();

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                ArgumentNullException.ThrowIfNull(targetMethod);

                string name = targetMethod.Name.Replace(@"get_", string.Empty);
                int position = s_PropertyNames.IndexOf(name) + 1;
                Type type = targetMethod.ReturnType;

                if (type == typeof(double?))
                {
                    return (double?)position;
                }
                if (type == typeof(int?))
                {
                    return (int?)position;
                }
                if (type == typeof(string))
                {
                    return name;
                }
                if (type == typeof(bool))
                {
                    return false;
                }
                if (type == typeof(Task))
                {
                    return Task.CompletedTask;
                }
                return null;
            }
        }
    }
}
