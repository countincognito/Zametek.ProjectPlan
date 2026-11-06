using Shouldly;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Export
{
    // The Save-As path writes the file the user picked through FileStreamHelper, so a graph that fails to export part-way
    // must leave the file already there as it was.
    public class FileStreamHelperTests
        : IDisposable
    {
        private readonly string m_TempDirectory;

        public FileStreamHelperTests()
        {
            m_TempDirectory = Path.Combine(Path.GetTempPath(), $@"graphs-file-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_TempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(m_TempDirectory, recursive: true);
        }

        [Fact]
        public async Task A_failed_save_leaves_the_file_already_there_as_it_was()
        {
            string path = Path.Combine(m_TempDirectory, @"graph.svg");
            File.WriteAllBytes(path, [9, 9, 9]);

            await Should.ThrowAsync<InvalidOperationException>(() => FileStreamHelper.SaveAsync(path, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1, 2 });
                throw new InvalidOperationException();
            }));

            File.ReadAllBytes(path).ShouldBe(new byte[] { 9, 9, 9 });
        }

        [Fact]
        public async Task A_save_replaces_a_longer_file_whole()
        {
            string path = Path.Combine(m_TempDirectory, @"graph.svg");
            File.WriteAllBytes(path, new byte[1000]);

            await FileStreamHelper.SaveAsync(path, stream => stream.WriteAsync(new byte[] { 1, 2, 3 }).AsTask());

            File.ReadAllBytes(path).ShouldBe(new byte[] { 1, 2, 3 });
        }
    }
}
