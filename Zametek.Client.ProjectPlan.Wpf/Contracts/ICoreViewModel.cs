using System;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface ICoreViewModel
        : IPropertyChangedPubSubViewModel
    {
        DateTime ProjectStart
        {
            get;
            set;
        }

        bool ShowDates
        {
            get;
            set;
        }

        bool UseBusinessDays
        {
            get;
            set;
        }

        bool HasStaleOutputs
        {
            get;
            set;
        }

        bool HasCompilationErrors
        {
            get;
            set;
        }

        GraphCompilation<int, IDependentActivity<int>> GraphCompilation
        {
            get;
            set;
        }
    }
}
