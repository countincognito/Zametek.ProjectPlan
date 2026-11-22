using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A saved dock layout that the application cannot use goes to the log. The application then starts with the default
    /// layout, as before. Before, only Debug.WriteLine said why, and a Release build has none: a user whose layout reset
    /// at each start had nothing in the log file.
    /// </summary>
    /// <remarks>
    /// The constructor of the main view model restores the layout, and so each test looks at what the log has after it.
    /// </remarks>
    public class MainViewModelLayoutLogTests
    {
        private static readonly Factory s_Factory = new();

        [Fact]
        public void RestoreLayout_Given_ASavedLayoutThatCannotBeRead_Then_TheLogHasAWarningWithTheException()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            var failure = new InvalidOperationException(@"The layout is not one of this version.");
            var logger = new WarningLogger();
            using Harness harness = Harness.Make(
                logger,
                dockLayout: @"{ not a layout",
                deserialize: () => throw failure,
                createLayout: () => LayoutWith(@"Alpha"));

            logger.Warnings.ShouldHaveSingleItem().Exception.ShouldBe(failure);
            DockLayoutHelper.CollectToolIds(harness.Main.Layout).ShouldBe([@"Alpha"]);
        }

        [Fact]
        public void RestoreLayout_Given_ASavedLayoutWithAToolMissing_Then_TheLogHasAWarning()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            var logger = new WarningLogger();
            using Harness harness = Harness.Make(
                logger,
                dockLayout: @"{ }",
                deserialize: () => LayoutWith(@"Alpha"),
                createLayout: () => LayoutWith(@"Alpha", @"Beta"));

            logger.Warnings.ShouldHaveSingleItem().Exception.ShouldBeNull();
            DockLayoutHelper.CollectToolIds(harness.Main.Layout).ShouldBe([@"Alpha", @"Beta"], ignoreOrder: true);
        }

        [Fact]
        public void RestoreLayout_Given_ASavedLayoutWithEachTool_Then_TheLogHasNoWarning()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            var logger = new WarningLogger();
            using Harness harness = Harness.Make(
                logger,
                dockLayout: @"{ }",
                deserialize: () => LayoutWith(@"Alpha", @"Beta"),
                createLayout: () => LayoutWith(@"Alpha", @"Beta"));

            logger.Warnings.ShouldBeEmpty();
        }

        private static RootDock LayoutWith(params string[] toolIds)
        {
            List<IDockable> tools = [];

            foreach (string id in toolIds)
            {
                tools.Add(new Tool { Id = id, Title = id });
            }

            return new RootDock
            {
                VisibleDockables = s_Factory.CreateList<IDockable>(new ToolDock { VisibleDockables = s_Factory.CreateList<IDockable>([.. tools]) }),
            };
        }

        // Remembers what the main view model logs as a warning.
        private sealed class WarningLogger
            : ILogger<MainViewModel>
        {
            public List<(string Message, Exception? Exception)> Warnings { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    Warnings.Add((formatter(state, exception), exception));
                }
            }
        }

        /// <summary>
        /// Stands in for an interface: one member does what the test gives it, and each other member does nothing, and gives
        /// back the default of its type.
        /// </summary>
        public class OneMemberProxy
            : DispatchProxy
        {
            private string m_Member = string.Empty;
            private Func<object?>? m_Answer;

            public static T Of<T>(string member, Func<object?> answer)
                where T : class
            {
                T proxy = Create<T, OneMemberProxy>();
                var self = (OneMemberProxy)(object)proxy;
                self.m_Member = member;
                self.m_Answer = answer;
                return proxy;
            }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod!.Name == m_Member)
                {
                    return m_Answer!();
                }

                Type returnType = targetMethod.ReturnType;

                return returnType != typeof(void) && returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
            }
        }

        // A main view model whose settings hold a saved dock layout, over a serializer and a factory that the test scripts.
        private sealed class Harness
            : IDisposable
        {
            private readonly CoreViewModel m_Core;
            private readonly ProjectScenarioManagerViewModel m_Project;

            private Harness(CoreViewModel core, ProjectScenarioManagerViewModel project, MainViewModel main)
            {
                m_Core = core;
                m_Project = project;
                Main = main;
            }

            public MainViewModel Main { get; }

            public static Harness Make(
                ILogger<MainViewModel> logger,
                string dockLayout,
                Func<object?> deserialize,
                Func<object?> createLayout)
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                settings.DockLayout = dockLayout;
                IDialogService dialogs = CoreViewModelFixture.CreateDialogService();
                var project = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    dialogs,
                    new DateTimeCalculator(TimeProvider.System),
                    CoreViewModelFixture.CreateUIDispatcher());

                var main = new MainViewModel(
                    OneMemberProxy.Of<IFactory>(nameof(IFactory.CreateLayout), createLayout),
                    OneMemberProxy.Of<IDockSerializer>(nameof(IDockSerializer.Deserialize), deserialize),
                    DoNothingProxy.Of<IDataGridLayoutManager>(),
                    project,
                    core,
                    DoNothingProxy.Of<IProjectFileOpen>(),
                    DoNothingProxy.Of<IProjectFileSave>(),
                    settings,
                    dialogs,
                    DoNothingProxy.Of<IServiceProvider>(),
                    logger);

                return new Harness(core, project, main);
            }

            public void Dispose()
            {
                Main.Dispose();
                m_Project.Dispose();
                m_Core.Dispose();
            }
        }
    }
}
