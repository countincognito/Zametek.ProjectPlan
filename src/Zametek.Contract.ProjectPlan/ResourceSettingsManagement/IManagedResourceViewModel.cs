﻿using System.ComponentModel;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedResourceViewModel
        : IDisposable, INotifyPropertyChanged, IKillSubscriptions, IMuteEdits
    {
        int Id { get; }

        string Name { get; set; }

        bool IsExplicitTarget { get; set; }

        bool IsInactive { get; set; }

        InterActivityAllocationType InterActivityAllocationType { get; set; }

        HashSet<int> InterActivityPhases { get; }

        double UnitCost { get; set; }

        double UnitBilling { get; set; }

        int AllocationOrder { get; set; }

        int DisplayOrder { get; set; }

        ColorFormatModel ColorFormat { get; set; }

        IWorkStreamSelectorViewModel WorkStreamSelector { get; }

        bool HasPhases { get; }

        IResourceTrackerSetViewModel TrackerSet { get; }

        bool IsEditing { get; }
    }
}
