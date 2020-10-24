using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ProjectPlan.Tests
{
    public class ProjectScenarioTests
    {
        [Fact]
        public void Can_generate_resourcing_scenarios()
        {
            var devCount = 10;
            var project = PrepareTestData(devCount);
            var scenarios = ResourceScenarioBuilder.Build(project.ResourceSettings);

            Assert.Equal(10, scenarios.Count);
            Assert.DoesNotContain(scenarios, x => x.Resources.All(x => x.IsExplicitTarget));

            for (var i = 1; i < devCount; i++)
            {
                Assert.Contains(scenarios, x => x.Resources.Count(x => !x.IsExplicitTarget) == i);
            }
        }

        private static ProjectPlanModel PrepareTestData(int devCount)
        {
            if (devCount < 2)
            {
                throw new ArgumentException("At least two devs are required.");
            }

            var project = new ProjectPlanModel();
            var settings = project.ResourceSettings = new ResourceSettingsModel { DefaultUnitCost = 1 };
            var resources = settings.Resources = new List<ResourceModel>();

            var testData = new[]
            {
                new { Explicit = true, Direct = false, Name = "Arch" },
                new { Explicit = true, Direct = false, Name = "PM" },
                new { Explicit = true, Direct = false, Name = "EM" },

                new { Explicit = true, Direct = true, Name = "UX1" },
                new { Explicit = true, Direct = true, Name = "UX2" },
                new { Explicit = true, Direct = true, Name = "SDET1"},
                new { Explicit = true, Direct = true, Name = "SDET2"},
            }.ToList();

            for (var i = 0; i < devCount; i++)
            {
                testData.Add(new { Explicit = false, Direct = true, Name = $"Dev {i + 1}" });
            }

            for (var i = 0; i < testData.Count; i++)
            {
                var item = testData[i];
                resources.Add(new ResourceModel
                {
                    Id = i + 1,
                    Name = item.Name,
                    IsExplicitTarget = item.Explicit,
                    InterActivityAllocationType = item.Direct
                        ? InterActivityAllocationType.Direct
                        : InterActivityAllocationType.Indirect,
                    UnitCost = 1,
                    DisplayOrder = i + 1
                });
            }

            return project;
        }
    }
}
