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

        // The save writes a temporary file next to the file, which then takes the place of the file. The cases of a
        // file that is not a plain file are in the tests of the application's copy of the helper: the two copies write
        // a file in the same way.
        [Fact]
        public async Task A_save_leaves_no_temporary_file()
        {
            string path = Path.Combine(m_TempDirectory, @"graph.svg");
            File.WriteAllBytes(path, [9, 9, 9]);
            File.WriteAllBytes($@"{path}.tmp", [7]);

            await FileStreamHelper.SaveAsync(path, stream => stream.WriteAsync(new byte[] { 1, 2, 3 }).AsTask());

            File.ReadAllBytes(path).ShouldBe(new byte[] { 1, 2, 3 });
            Directory.GetFiles(m_TempDirectory).ShouldBe([path]);
        }

        [Fact]
        public void A_write_that_fails_part_way_leaves_the_file_already_there_as_it_was()
        {
            string path = Path.Combine(m_TempDirectory, @"graph.svg");
            File.WriteAllBytes(path, [9, 9, 9]);

            Should.Throw<IOException>(() => FileStreamHelper.Write(
                path,
                stream =>
                {
                    stream.Write([1, 2]);
                    stream.Flush();
                    throw new IOException(@"The disk is full.");
                },
                TimeSpan.FromMilliseconds(300)));

            File.ReadAllBytes(path).ShouldBe(new byte[] { 9, 9, 9 });
            Directory.GetFiles(m_TempDirectory).ShouldBe([path]);
        }

        [WindowsFact]
        public void A_write_to_a_hidden_file_works_and_the_file_stays_hidden()
        {
            string path = Path.Combine(m_TempDirectory, @"graph.svg");
            File.WriteAllBytes(path, [9, 9, 9]);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);

            FileStreamHelper.Write(path, stream => stream.Write([1, 2, 3]), TimeSpan.FromMilliseconds(300));

            File.ReadAllBytes(path).ShouldBe(new byte[] { 1, 2, 3 });
            File.GetAttributes(path).HasFlag(FileAttributes.Hidden).ShouldBeTrue();
            File.SetAttributes(path, FileAttributes.Normal);
        }

        // A program holds the file and lets others write it and not delete it: nothing can take the place of the file,
        // and the write goes in place, as the save did before.
        [WindowsFact]
        public void A_write_to_a_file_that_a_program_holds_without_delete_sharing_goes_in_place()
        {
            string path = Path.Combine(m_TempDirectory, @"graph.svg");
            File.WriteAllBytes(path, [9, 9, 9]);

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                FileStreamHelper.Write(path, stream => stream.Write([1, 2, 3]), TimeSpan.FromMilliseconds(300));
            }

            File.ReadAllBytes(path).ShouldBe(new byte[] { 1, 2, 3 });
            Directory.GetFiles(m_TempDirectory).ShouldBe([path]);
        }
    }
}
