using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.View.ProjectPlan;

namespace Zametek.Shell.ProjectPlan
{
    public class AppModule
       : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var regionManager = containerProvider.Resolve<IRegionManager>();




            regionManager.RegisterViewWithRegion(RegionNames.ProjectPlanActivitiesRegion, typeof(ActivitiesManagerView));
            //regionManager.RegisterViewWithRegion(RegionNames.ProjectPlanMetricsRegion, typeof(MetricsManagerView));
            //regionManager.RegisterViewWithRegion(RegionNames.ProjectPlanGanttChartRegion, typeof(GanttChartManagerView));
            //regionManager.RegisterViewWithRegion(RegionNames.ProjectPlanArrowGraphRegion, typeof(ArrowGraphManagerView));
            //regionManager.RegisterViewWithRegion(RegionNames.ProjectPlanResourceChartRegion, typeof(ResourceChartManagerView));
            //regionManager.RegisterViewWithRegion(RegionNames.ProjectPlanEarnedValueChartRegion, typeof(EarnedValueChartManagerView));




            //IRegion mainregion = regionManager.Regions[Constants.MainRegion];
            //var bottomAnchorableView = containerProvider.Resolve<BottomAnchorableView>();
            //mainregion.Add(bottomAnchorableView);

            //var rightAnchorableView = containerProvider.Resolve<RightAnchorableView>();
            //mainregion.Add(rightAnchorableView);

            //var leftAnchorableView = containerProvider.Resolve<LeftAnchorableView>();
            //mainregion.Add(leftAnchorableView);
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
        }
    }
}
