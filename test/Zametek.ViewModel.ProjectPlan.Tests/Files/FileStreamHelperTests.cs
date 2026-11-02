using Shouldly;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The file end of the stream-based file layer, used by the desktop's dialogs and zpp's options.
    public class FileStreamHelperTests
        : IDisposable
    {
        private readonly string m_TempDirectory;

        public FileStreamHelperTests()
        {
            m_TempDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-file-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_TempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(m_TempDirectory, recursive: true);
        }

        private string FilePath(string name) => Path.Combine(m_TempDirectory, name);

        [Fact]
        public async Task SaveAsync_Given_ALongerFileAlreadyThere_Then_ReplacesItWhole()
        {
            string path = FilePath(@"output.bin");
            File.WriteAllBytes(path, new byte[1000]);

            await FileStreamHelper.SaveAsync(path, stream => stream.WriteAsync(new byte[] { 1, 2, 3 }).AsTask());

            File.ReadAllBytes(path).ShouldBe(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void Save_Given_ALongerFileAlreadyThere_Then_ReplacesItWhole()
        {
            string path = FilePath(@"output.bin");
            File.WriteAllBytes(path, new byte[1000]);

            FileStreamHelper.Save(path, stream => stream.Write(new byte[] { 1, 2, 3 }));

            File.ReadAllBytes(path).ShouldBe(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public async Task SaveAsync_Given_AWriterThatFails_Then_TheFileAlreadyThereIsLeftAsItWas()
        {
            // A save that fails part-way - a render that throws, a serialiser that trips over the plan - must not cost the
            // user the file they already had.
            string path = FilePath(@"project.zpp");
            File.WriteAllBytes(path, new byte[] { 9, 9, 9 });

            await Should.ThrowAsync<InvalidOperationException>(() => FileStreamHelper.SaveAsync(path, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1, 2 });
                throw new InvalidOperationException();
            }));

            File.ReadAllBytes(path).ShouldBe(new byte[] { 9, 9, 9 });
        }

        [Fact]
        public void Save_Given_AWriterThatFails_Then_TheFileAlreadyThereIsLeftAsItWas()
        {
            string path = FilePath(@"scenario.xlsx");
            File.WriteAllBytes(path, new byte[] { 9, 9, 9 });

            Should.Throw<InvalidOperationException>(() => FileStreamHelper.Save(path, stream =>
            {
                stream.Write(new byte[] { 1, 2 });
                throw new InvalidOperationException();
            }));

            File.ReadAllBytes(path).ShouldBe(new byte[] { 9, 9, 9 });
        }

        [Fact]
        public async Task SaveAsync_Given_AWriterThatFails_Then_NoFileIsLeftBehind()
        {
            string path = FilePath(@"chart.png");

            await Should.ThrowAsync<InvalidOperationException>(() => FileStreamHelper.SaveAsync(path, _ => throw new InvalidOperationException()));

            File.Exists(path).ShouldBeFalse();
        }

        [Fact]
        public void OpenImportFile_Given_AnMsProjectPlanOpenForWritingElsewhere_Then_OpensIt()
        {
            // MS Project keeps a plan it is editing open for writing. MPXJ, handed the file name, opened the file sharing
            // both reading and writing, so such a plan could be imported, and opening it here must not take that away.
            // Windows is where this has teeth: a reader that shared reading alone would be refused there.
            string path = FilePath(@"plan.mpp");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            using var msProject = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

            using FileStream stream = FileStreamHelper.OpenImportFile(path, ProjectScenarioImportFormat.MicrosoftProject);

            stream.ReadByte().ShouldBe(1);
        }
    }
}
