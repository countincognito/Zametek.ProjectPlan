using FakeItEasy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Zametek.Contract.ProjectPlan;
using Zametek.Manager.ProjectPlan;


namespace Zametek.Engine.ProjectPlan.Test
{
   public class ProjectManagerTests
    {
        private IGraphProcessingEngine _graphProcessingEngine;
        private IMetricAssessingEngine _metricAssessingEngine;
        private IProjectManager _projectManager;

        public ProjectManagerTests()
        {
            _graphProcessingEngine = A.Fake<IGraphProcessingEngine>();
            _metricAssessingEngine = A.Fake<IMetricAssessingEngine>();
            _projectManager = new ProjectManager(_graphProcessingEngine, _metricAssessingEngine);
        }

        [Fact]
        public void CalculateProjectMetricsReturnsArgumentExceptionsWhenOneOfTheArgumentsAreNull()
        {
            Assert.Throws<ArgumentNullException>(() => _projectManager.CalculateProjectMetrics(null, null));
            

        }
    }
}
