using Prism.Modularity;
using Prism.Regions;
using System;
using Zametek.Common.ProjectPlan;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ClientWpfModule
        : IModule
    {
        #region Fields

        IRegionManager m_RegionManager;

        #endregion

        #region Ctors

        public ClientWpfModule(IRegionManager regionManager)
        {
            m_RegionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        }

        #endregion

        #region IModule

        public void Initialize()
        {
            m_RegionManager.RegisterViewWithRegion(RegionNames.ProjectPlanMainRegion, typeof(MainView));
            m_RegionManager.RegisterViewWithRegion(RegionNames.ProjectPlanActivitiesRegion, typeof(ActivitiesManagerView));
            m_RegionManager.RegisterViewWithRegion(RegionNames.ProjectPlanArrowGraphRegion, typeof(ArrowGraphManagerView));
            m_RegionManager.RegisterViewWithRegion(RegionNames.ProjectPlanResourceChartsRegion, typeof(ResourceChartsManagerView));
            m_RegionManager.RegisterViewWithRegion(RegionNames.ProjectPlanEarnedValueChartRegion, typeof(EarnedValueChartManagerView));
        }

        #endregion
    }
}
