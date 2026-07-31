using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Dock.Model.ReactiveUI.Core;
using ReactiveUI.Builder;
using Shouldly;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for DockLayoutHelper, which gathers the tool Ids reachable from a
    /// dock layout. MainViewModel uses it to detect persisted layouts from
    /// older app versions that lack tools the current version expects, so the
    /// traversal must see tools wherever they can end up: visible, pinned,
    /// hidden, or floated into separate windows.
    /// </summary>
    public class DockLayoutHelperTests
    {
        static DockLayoutHelperTests()
        {
            // The ReactiveUI-based Dock model classes cannot be constructed until
            // ReactiveUI 23's explicit initialization has run. There is no UI
            // platform here, so initialize the core services directly - the same
            // approach the headless CommandLine app uses.
            RxAppBuilder.CreateReactiveUIBuilder()
                .WithCoreServices()
                .BuildApp();
        }

        private static readonly Factory s_Factory = new();

        private static Tool CreateTool(string id) => new() { Id = id, Title = id };

        private static ToolDock CreateToolDock(params Tool[] tools) => new()
        {
            VisibleDockables = s_Factory.CreateList<IDockable>([.. tools]),
        };

        [Fact]
        public void CollectToolIds_Given_Null_Then_Empty()
        {
            DockLayoutHelper.CollectToolIds(null).ShouldBeEmpty();
        }

        [Fact]
        public void CollectToolIds_Given_NestedVisibleDockables_Then_AllToolIdsWithoutDocksOrSplitters()
        {
            var root = new RootDock
            {
                Id = @"Root",
                VisibleDockables = s_Factory.CreateList<IDockable>(
                    new ProportionalDock
                    {
                        Id = @"Proportional",
                        VisibleDockables = s_Factory.CreateList<IDockable>(
                            CreateToolDock(CreateTool(@"Alpha"), CreateTool(@"Beta")),
                            new ProportionalDockSplitter { Id = @"Splitter" },
                            CreateToolDock(CreateTool(@"Gamma"))),
                    }),
            };

            DockLayoutHelper.CollectToolIds(root).ShouldBe([@"Alpha", @"Beta", @"Gamma"], ignoreOrder: true);
        }

        [Fact]
        public void CollectToolIds_Given_PinnedAndHiddenTools_Then_Included()
        {
            var root = new RootDock
            {
                VisibleDockables = s_Factory.CreateList<IDockable>(CreateToolDock(CreateTool(@"Visible"))),
                PinnedDock = CreateToolDock(CreateTool(@"PinnedDock")),
                HiddenDockables = s_Factory.CreateList<IDockable>(CreateTool(@"Hidden")),
                LeftPinnedDockables = s_Factory.CreateList<IDockable>(CreateTool(@"Left")),
                RightPinnedDockables = s_Factory.CreateList<IDockable>(CreateTool(@"Right")),
                TopPinnedDockables = s_Factory.CreateList<IDockable>(CreateTool(@"Top")),
                BottomPinnedDockables = s_Factory.CreateList<IDockable>(CreateTool(@"Bottom")),
            };

            DockLayoutHelper.CollectToolIds(root).ShouldBe(
                [@"Visible", @"PinnedDock", @"Hidden", @"Left", @"Right", @"Top", @"Bottom"],
                ignoreOrder: true);
        }

        [Fact]
        public void CollectToolIds_Given_FloatedWindowTools_Then_Included()
        {
            var root = new RootDock
            {
                VisibleDockables = s_Factory.CreateList<IDockable>(CreateToolDock(CreateTool(@"Docked"))),
                Windows = s_Factory.CreateList<IDockWindow>(
                    new DockWindow
                    {
                        Layout = new RootDock
                        {
                            VisibleDockables = s_Factory.CreateList<IDockable>(CreateToolDock(CreateTool(@"Floated"))),
                        },
                    }),
            };

            DockLayoutHelper.CollectToolIds(root).ShouldBe([@"Docked", @"Floated"], ignoreOrder: true);
        }

        [Fact]
        public void CollectToolIds_Given_LayoutMissingTool_Then_SubsetCheckFails()
        {
            var reference = new RootDock
            {
                VisibleDockables = s_Factory.CreateList<IDockable>(
                    CreateToolDock(CreateTool(@"Alpha"), CreateTool(@"Beta"))),
            };
            var persisted = new RootDock
            {
                VisibleDockables = s_Factory.CreateList<IDockable>(CreateToolDock(CreateTool(@"Alpha"))),
            };

            DockLayoutHelper.CollectToolIds(reference)
                .IsSubsetOf(DockLayoutHelper.CollectToolIds(persisted))
                .ShouldBeFalse();
            DockLayoutHelper.CollectToolIds(persisted)
                .IsSubsetOf(DockLayoutHelper.CollectToolIds(reference))
                .ShouldBeTrue();
        }
    }
}
