using Dock.Model.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A save clears the marks of a change only when no change came while it built and wrote the file. A change that comes
    /// after the save took its copy of the project is not in the file. If the save cleared its mark, the title showed no
    /// change, and the close did not ask.
    /// </summary>
    public class MainViewModelSaveTests
    {
        [Fact]
        public async Task SaveProjectFileAsync_Given_NoChangeDuringTheWrite_Then_TheMarksOfAChangeGo()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            harness.Project.IsProjectUpdated = true;

            await harness.Main.SaveProjectFileAsync();

            harness.Dialogs.Errors.ShouldBeEmpty();
            File.Exists(harness.Filename).ShouldBeTrue(@"the save wrote no file");
            harness.Core.IsProjectScenarioUpdated.ShouldBeFalse();
            harness.Project.IsProjectUpdated.ShouldBeFalse();
        }

        [Fact]
        public async Task SaveProjectFileAsync_Given_AChangeToTheScenarioDuringTheWrite_Then_TheMarksOfAChangeStay()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            harness.Project.IsProjectUpdated = true;

            // The scenario has its mark already, and so the change during the write does not change the mark.
            harness.Saver.DuringTheWrite = () => harness.Core.IsProjectScenarioUpdated = true;

            await harness.Main.SaveProjectFileAsync();

            harness.Dialogs.Errors.ShouldBeEmpty();
            File.Exists(harness.Filename).ShouldBeTrue(@"the save wrote no file");
            harness.Core.IsProjectScenarioUpdated.ShouldBeTrue(@"the change that is not in the file lost its mark");
        }

        [Fact]
        public async Task SaveProjectFileAsync_Given_AChangeToTheProjectDuringTheWrite_Then_TheMarksOfAChangeStay()
        {
            using Harness harness = Harness.Make();
            harness.Project.IsProjectUpdated = true;
            harness.Saver.DuringTheWrite = () => harness.Project.IsProjectUpdated = true;

            await harness.Main.SaveProjectFileAsync();

            harness.Dialogs.Errors.ShouldBeEmpty();
            harness.Project.IsProjectUpdated.ShouldBeTrue(@"the change that is not in the file lost its mark");
        }

        [Fact]
        public async Task SaveProjectFileAsync_Given_AChangeDuringTheWrite_Then_TheNextSaveClearsTheMarks()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            harness.Saver.DuringTheWrite = () => harness.Core.IsProjectScenarioUpdated = true;
            await harness.Main.SaveProjectFileAsync();
            harness.Saver.DuringTheWrite = null;

            await harness.Main.SaveProjectFileAsync();

            harness.Core.IsProjectScenarioUpdated.ShouldBeFalse();
            harness.Project.IsProjectUpdated.ShouldBeFalse();
        }

        // Writes a line in place of the project, and lets a test change the plan while the save writes.
        private sealed class ScriptedProjectFileSave
            : IProjectFileSave
        {
            public Action? DuringTheWrite { get; set; }

            public async Task SaveProjectFileAsync(ProjectModel project, Stream stream)
            {
                DuringTheWrite?.Invoke();

                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteLineAsync(@"The scripted save writes this line.");
            }
        }

        // A main view model whose project has the name of a file in a folder of the test, so that a save writes there.
        private sealed class Harness
            : IDisposable
        {
            private readonly string m_Directory;

            private Harness(
                CoreViewModel core,
                ProjectScenarioManagerViewModel project,
                ScriptedDialogService dialogs,
                ScriptedProjectFileSave saver,
                MainViewModel main,
                string directory)
            {
                Core = core;
                Project = project;
                Dialogs = dialogs;
                Saver = saver;
                Main = main;
                m_Directory = directory;
                Filename = Path.Combine(directory, $@"plan.{Resource.ProjectPlan.Filters.Filter_ProjectFileExtension}");
            }

            public CoreViewModel Core { get; }

            public ProjectScenarioManagerViewModel Project { get; }

            public ScriptedDialogService Dialogs { get; }

            public ScriptedProjectFileSave Saver { get; }

            public MainViewModel Main { get; }

            public string Filename { get; }

            public static Harness Make()
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                var dialogs = new ScriptedDialogService();
                var saver = new ScriptedProjectFileSave();
                var project = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    dialogs,
                    new DateTimeCalculator(TimeProvider.System),
                    CoreViewModelFixture.CreateUIDispatcher());

                var main = new MainViewModel(
                    DoNothingProxy.Of<IFactory>(),
                    DoNothingProxy.Of<IDockSerializer>(),
                    DoNothingProxy.Of<IDataGridLayoutManager>(),
                    project,
                    core,
                    DoNothingProxy.Of<IProjectFileOpen>(),
                    saver,
                    settings,
                    dialogs,
                    DoNothingProxy.Of<IServiceProvider>(),
                    NullLogger<MainViewModel>.Instance);

                string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"zpp-save-{Guid.NewGuid():N}")).FullName;
                var harness = new Harness(core, project, dialogs, saver, main, directory);
                settings.SetProjectFilePath(harness.Filename, bindTitleToFilename: true);
                return harness;
            }

            public void Dispose()
            {
                Main.Dispose();
                Project.Dispose();
                Core.Dispose();

                try
                {
                    Directory.Delete(m_Directory, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup only.
                }
            }
        }
    }
}
