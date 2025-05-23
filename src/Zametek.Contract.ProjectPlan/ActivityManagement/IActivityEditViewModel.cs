﻿using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IActivityEditViewModel
        : IDisposable
    {
        IResourceSelectorViewModel ResourceSelector { get; }
        bool IsResourceSelectorActive { get; set; }

        IWorkStreamSelectorViewModel WorkStreamSelector { get; }
        bool IsWorkStreamSelectorActive { get; set; }

        bool HasNoCost { get; set; }
        bool IsHasNoCostActive { get; set; }

        bool HasNoEffort { get; set; }
        bool IsHasNoEffortActive { get; set; }

        LogicalOperator TargetResourceOperator { get; set; }
        bool IsTargetResourceOperatorActive { get; set; }

        UpdateDependentActivityModel BuildUpdateModel();
    }
}
