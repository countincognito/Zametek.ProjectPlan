using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface ICoreViewModel
        : IPropertyChangedPubSubViewModel
    {
        ObservableCollection<ManagedActivityViewModel> Activities
        {
            get;
        }

        bool DisableResources
        {
            get;
            set;
        }

        IList<ResourceDto> ResourceDtos
        {
            get;
        }

        MetricsDto MetricsDto
        {
            get;
            set;
        }

        //string ProjectTitle
        //{
        //    get;
        //}

        //bool IsProjectUpdated
        //{
        //    get;
        //}

        //DateTime ProjectStart
        //{
        //    get;
        //    set;
        //}

        //bool ShowDates
        //{
        //    get;
        //    set;
        //}

        //bool UseBusinessDays
        //{
        //    get;
        //    set;
        //}

        //bool AutoCompile
        //{
        //    get;
        //    set;
        //}

        GraphCompilation<int, IDependentActivity<int>> GraphCompilation
        {
            get;
            set;
        }

        string CompilationOutput
        {
            get;
            set;
        }

        bool HasCompilationErrors
        {
            get;
            set;
        }

        //void ResetProject();
    }
}
