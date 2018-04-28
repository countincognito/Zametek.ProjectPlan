using FakeItEasy;
using System;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.Project;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Manager.ProjectPlan.Tests
{
    public class ProjectManagerShould
    {
        private IGraphProcessingEngine m_GraphProcessingEngine;
        private IAssessingEngine m_AssessingEngine;
        private IProjectManager m_ProjectManager;

        public ProjectManagerShould()
        {
            m_GraphProcessingEngine = A.Fake<IGraphProcessingEngine>();
            m_AssessingEngine = A.Fake<IAssessingEngine>();
            m_ProjectManager = new ProjectManager(m_GraphProcessingEngine, m_AssessingEngine);
        }

        [Fact]
        public void ThrowArgumentNullExceptionsWhenCalculatingProjectMetricsWithNullActivities()
        {
            Assert.Throws<ArgumentNullException>(() => m_ProjectManager.CalculateProjectMetrics(null, A.Fake<IList<ActivitySeverityDto>>()));
        }

        [Fact]
        public void ThrowArgumentNullExceptionsWhenCalculatingProjectMetricsWithNullActivitySeverities()
        {
            Assert.Throws<ArgumentNullException>(() => m_ProjectManager.CalculateProjectMetrics(A.Fake<IList<IActivity<int>>>(), null));
        }

        [Fact]
        public void ThrowArgumentNullExceptionsWhenCalculatingResourceSeriesSetWithNullResourceSchedules()
        {
            Assert.Throws<ArgumentNullException>(() => m_ProjectManager.CalculateResourceSeriesSet(null, A.Fake<IList<ResourceDto>>(), 0.0));
        }

        [Fact]
        public void ThrowArgumentNullExceptionsWhenCalculatingResourceSeriesSetWithNullResources()
        {
            Assert.Throws<ArgumentNullException>(() => m_ProjectManager.CalculateResourceSeriesSet(A.Fake<IList<IResourceSchedule<int>>>(), null, 0.0));
        }

        [Fact]
        public void ThrowArgumentNullExceptionsWhenCalculatingProjectCostsWithNullResourceSeriesSet()
        {
            Assert.Throws<ArgumentNullException>(() => m_ProjectManager.CalculateProjectCosts(null));
        }

        [Fact]
        public void ThrowArgumentNullExceptionsWhenExportingArrowGraphWithNullDiagram()
        {
            Assert.Throws<ArgumentNullException>(() => m_ProjectManager.ExportArrowGraphToDiagram(null));
        }
    }
}
