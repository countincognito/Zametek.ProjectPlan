using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using System;
using System.Collections.Generic;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public class DockFactory
        : Factory
    {
        private IRootDock? m_RootDock;

        private readonly IDockable m_ActivitiesManagerViewModel;
        private readonly IDockable m_TrackingManagerViewModel;
        private readonly IDockable m_MetricManagerViewModel;
        private readonly IDockable m_OutputManagerViewModel;
        private readonly IDockable m_ArrowGraphManagerViewModel;
        private readonly IDockable m_ResourceChartManagerViewModel;
        private readonly IDockable m_GanttChartManagerViewModel;
        private readonly IDockable m_EarnedValueChartManagerViewModel;
        private readonly IDockable m_ArrowGraphSettingsManagerViewModel;
        private readonly IDockable m_ResourceSettingsManagerViewModel;

        public DockFactory(
            IActivitiesManagerViewModel activitiesManagerViewModel,
            ITrackingManagerViewModel trackingManagerViewModel,
            IMetricManagerViewModel metricManagerViewModel,
            IOutputManagerViewModel outputManagerViewModel,
            IArrowGraphManagerViewModel arrowGraphManagerViewModel,
            IResourceChartManagerViewModel resourceChartManagerViewModel,
            IGanttChartManagerViewModel ganttChartManagerViewModel,
            IEarnedValueChartManagerViewModel earnedValueChartManagerViewModel,
            IArrowGraphSettingsManagerViewModel arrowGraphSettingsManagerViewModel,
            IResourceSettingsManagerViewModel resourceSettingsManagerViewModel)
        {
            m_ActivitiesManagerViewModel = activitiesManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(activitiesManagerViewModel));
            m_TrackingManagerViewModel = trackingManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(trackingManagerViewModel));
            m_MetricManagerViewModel = metricManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(metricManagerViewModel));
            m_OutputManagerViewModel = outputManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(outputManagerViewModel));
            m_ArrowGraphManagerViewModel = arrowGraphManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(arrowGraphManagerViewModel));
            m_ResourceChartManagerViewModel = resourceChartManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(resourceChartManagerViewModel));
            m_GanttChartManagerViewModel = ganttChartManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(ganttChartManagerViewModel));
            m_EarnedValueChartManagerViewModel = earnedValueChartManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(earnedValueChartManagerViewModel));
            m_ArrowGraphSettingsManagerViewModel = arrowGraphSettingsManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(arrowGraphSettingsManagerViewModel));
            m_ResourceSettingsManagerViewModel = resourceSettingsManagerViewModel as IDockable ?? throw new ArgumentNullException(nameof(resourceSettingsManagerViewModel));
        }

        public override IRootDock CreateLayout()
        {
            var mainLayout = new ProportionalDock
            {
                Proportion = 1.0,
                Orientation = Orientation.Vertical,
                ActiveDockable = m_ActivitiesManagerViewModel,
                CanClose = false,
                CanFloat = false,
                CanPin = false,
                IsCollapsable = false,
                VisibleDockables = CreateList<IDockable>
                (
                    new ToolDock
                    {
                        Proportion = 0.65,
                        ActiveDockable = m_ActivitiesManagerViewModel,
                        CanClose = false,
                        CanFloat = false,
                        CanPin = false,
                        IsCollapsable = false,
                        VisibleDockables = CreateList(
                            m_ActivitiesManagerViewModel,
                            m_GanttChartManagerViewModel,
                            m_TrackingManagerViewModel,
                            m_ArrowGraphManagerViewModel,
                            m_ResourceChartManagerViewModel,
                            m_EarnedValueChartManagerViewModel),
                        Alignment = Alignment.Left,
                        GripMode = GripMode.Visible
                    },
                    new ProportionalDockSplitter()
                    {
                        Id = "Splitter1",
                        Title = "VerticalSplitter"
                    },
                    new ProportionalDock
                    {
                        Proportion = 0.35,
                        Orientation = Orientation.Horizontal,
                        ActiveDockable = m_MetricManagerViewModel,
                        CanClose = false,
                        CanFloat = false,
                        CanPin = false,
                        IsCollapsable = false,
                        VisibleDockables = CreateList<IDockable>
                        (
                            new ToolDock
                            {
                                Proportion = 0.6,
                                ActiveDockable = m_MetricManagerViewModel,
                                CanClose = false,
                                CanFloat = false,
                                CanPin = false,
                                IsCollapsable = false,
                                VisibleDockables = CreateList(
                                    m_MetricManagerViewModel,
                                    m_OutputManagerViewModel),
                                Alignment = Alignment.Left,
                                GripMode = GripMode.Visible
                            },
                            new ProportionalDockSplitter()
                            {
                                Id = "Splitter2",
                                Title = "VerticalSplitter"
                            },
                            new ToolDock
                            {
                                Proportion = 0.4,
                                ActiveDockable = m_ResourceSettingsManagerViewModel,
                                CanClose = false,
                                CanFloat = false,
                                CanPin = false,
                                IsCollapsable = false,
                                VisibleDockables = CreateList(
                                    m_ResourceSettingsManagerViewModel,
                                    m_ArrowGraphSettingsManagerViewModel),
                                Alignment = Alignment.Right,
                                GripMode = GripMode.Visible
                            }
                        )
                    }
                )
            };

            var rootDock = CreateRootDock();

            rootDock.IsCollapsable = false;
            rootDock.CanClose = false;
            rootDock.CanFloat = false;
            rootDock.CanPin = false;
            rootDock.ActiveDockable = mainLayout;
            rootDock.DefaultDockable = mainLayout;
            rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);

            m_RootDock = rootDock;

            return rootDock;
        }

        public override void InitLayout(IDockable layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            ContextLocator = new Dictionary<string, Func<object?>>
            {
            };

            DockableLocator = new Dictionary<string, Func<IDockable?>>()
            {
                ["Root"] = () => m_RootDock,
            };

            HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
            {
                [nameof(IDockWindow)] = () => new HostWindow()
            };

            base.InitLayout(layout);
        }
    }
}
