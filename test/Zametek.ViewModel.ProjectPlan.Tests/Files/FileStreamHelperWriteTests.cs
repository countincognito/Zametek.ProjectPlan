using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A save writes a temporary file next to its target, and then puts that file in the place of the target. Thus, a write
    /// that does not finish leaves the file that was there. These tests give the targets that are not plain files: one that
    /// nobody may write, a hidden one, one that another program holds open, and a link. Some of them are cases of Windows
    /// only, and some of Linux only: a test of another system passes with no check.
    /// </summary>
    public sealed class FileStreamHelperWriteTests
        : IDisposable
    {
        // Short, so that a test of a file that stays held does not wait for long.
        private static readonly TimeSpan s_RetryTime = TimeSpan.FromMilliseconds(300);

        private static readonly byte[] s_Old = [9, 9, 9];
        private static readonly byte[] s_New = [1, 2, 3, 4];

        private readonly string m_Folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-write-tests-{Guid.NewGuid():N}")).FullName;
        private readonly List<(string File, Exception Reason)> m_InPlace = [];

        public void Dispose()
        {
            foreach (string file in Directory.EnumerateFiles(m_Folder, @"*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            if (!OperatingSystem.IsWindows())
            {
                foreach (string folder in Directory.EnumerateDirectories(m_Folder, @"*", SearchOption.AllDirectories))
                {
                    File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }

            Directory.Delete(m_Folder, recursive: true);
        }

        private string FilePath(string name) => Path.Combine(m_Folder, name);

        private string Existing(string name = @"plan.zpp")
        {
            string path = FilePath(name);
            File.WriteAllBytes(path, s_Old);
            return path;
        }

        private void Write(string path) =>
            FileStreamHelper.Write(path, stream => stream.Write(s_New), s_RetryTime, (file, reason) => m_InPlace.Add((file, reason)));

        // The bytes of a file that another handle can hold open.
        private static byte[] Read(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        private string[] Files() => [.. Directory.EnumerateFiles(m_Folder, @"*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];

        #region Plain files

        [Fact]
        public void Write_Given_NoFileThere_Then_TheFileAndNoTemporaryFile()
        {
            string path = FilePath(@"plan.zpp");

            Write(path);

            Read(path).ShouldBe(s_New);
            Files().ShouldBe([path]);
            m_InPlace.ShouldBeEmpty();
        }

        [Fact]
        public void Write_Given_AFileThere_Then_TheNewContentInItsPlaceAndNoTemporaryFile()
        {
            string path = Existing();
            DateTime created = File.GetCreationTimeUtc(path);

            Write(path);

            Read(path).ShouldBe(s_New);
            Files().ShouldBe([path]);
            m_InPlace.ShouldBeEmpty();

            // The file is the file that was there, with new content: Windows keeps the time of its creation.
            if (OperatingSystem.IsWindows())
            {
                File.GetCreationTimeUtc(path).ShouldBe(created);
            }
        }

        [Fact]
        public void Write_Given_AWriterThatFailsPartWay_Then_TheFileThatWasThereIsWholeAndNoTemporaryFile()
        {
            string path = Existing();

            Should.Throw<IOException>(() => FileStreamHelper.Write(
                path,
                stream =>
                {
                    stream.Write(s_New.AsSpan(0, 2));
                    stream.Flush();
                    throw new IOException(@"The disk is full.");
                },
                s_RetryTime,
                (file, reason) => m_InPlace.Add((file, reason))));

            Read(path).ShouldBe(s_Old);
            Files().ShouldBe([path]);
            m_InPlace.ShouldBeEmpty();
        }

        [Fact]
        public void Write_Given_ATemporaryFileThatAnEarlierWriteLeft_Then_TheFileAndNoTemporaryFile()
        {
            string path = Existing();
            File.WriteAllBytes($@"{path}.tmp", [7]);

            Write(path);

            Read(path).ShouldBe(s_New);
            Files().ShouldBe([path]);
        }

        [Fact]
        public void Write_Given_AFolderThatIsNotThere_Then_TheFailureOfTheSystemAndNoFile()
        {
            string path = Path.Combine(m_Folder, @"no such folder", @"plan.zpp");

            Should.Throw<DirectoryNotFoundException>(() => Write(path));

            Files().ShouldBeEmpty();
            m_InPlace.ShouldBeEmpty();
        }

        #endregion

        #region A file that nobody may write, and a hidden file

        // The save fails, as it failed before, and the file is whole.
        [Fact]
        public void Write_Given_AReadOnlyFile_Then_TheSaveFailsAndTheFileIsAsItWas()
        {
            string path = Existing();
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

            // A user with all rights (root, in a container) writes each file, and the system refuses nothing.
            if (!OperatingSystem.IsWindows()
                && Environment.UserName == @"root")
            {
                return;
            }

            Should.Throw<UnauthorizedAccessException>(() => Write(path));

            Read(path).ShouldBe(s_Old);
            File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly).ShouldBeTrue();
            Files().ShouldBe([path]);
            m_InPlace.ShouldBeEmpty();
        }

        // Before, a save to a hidden file failed. The save works now, and the file stays hidden.
        [Fact]
        public void Write_Given_AHiddenFile_Then_TheNewContentAndTheFileStaysHidden()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = Existing();
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);

            Write(path);

            Read(path).ShouldBe(s_New);
            File.GetAttributes(path).HasFlag(FileAttributes.Hidden).ShouldBeTrue();
            m_InPlace.ShouldBeEmpty();
        }

        // Linux: the file keeps the permissions that it had.
        [Fact]
        public void Write_Given_AFileWithPermissionsOfItsOwn_Then_TheFileKeepsThem()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            string path = Existing();
            UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;
            File.SetUnixFileMode(path, mode);

            Write(path);

            Read(path).ShouldBe(s_New);
            File.GetUnixFileMode(path).ShouldBe(mode);
        }

        // Linux: a file that the user may write, in a folder where the user may make no file. A save in place works
        // there, and so the save writes in place, as it did before.
        [Fact]
        public void Write_Given_AFolderWhereNoFileCanBeMade_Then_TheSaveWritesInPlaceAndSaysSo()
        {
            if (OperatingSystem.IsWindows()
                || Environment.UserName == @"root")
            {
                return;
            }

            string folder = Directory.CreateDirectory(Path.Combine(m_Folder, @"closed")).FullName;
            string path = Path.Combine(folder, @"plan.zpp");
            File.WriteAllBytes(path, s_Old);
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            Write(path);

            Read(path).ShouldBe(s_New);
            m_InPlace.ShouldHaveSingleItem().File.ShouldBe(path);
        }

        #endregion

        #region A file that another program holds open (Windows)

        // A sync client and a virus scanner let others read, write and delete the file that they hold.
        [Fact]
        public void Write_Given_AFileHeldByAProgramThatSharesAll_Then_TheNewContentInItsPlace()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = Existing();

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                Write(path);
            }

            Read(path).ShouldBe(s_New);
            Files().ShouldBe([path]);
            m_InPlace.ShouldBeEmpty();
        }

        // A program that lets others write the file, and not delete it. Nothing can take the place of such a file, and a
        // save in place works, as it did before. The save tries for a time, then it writes in place, and it says so.
        [Fact]
        public void Write_Given_AFileHeldByAProgramThatDoesNotShareDelete_Then_TheSaveWritesInPlaceAndSaysSo()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = Existing();

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Write(path);
            }

            Read(path).ShouldBe(s_New);
            Files().ShouldBe([path]);
            (string file, Exception reason) = m_InPlace.ShouldHaveSingleItem();
            file.ShouldBe(path);
            reason.ShouldBeAssignableTo<Exception>();
        }

        // A program that lets nobody write the file. The save failed before, and it fails now, with the file whole.
        [Fact]
        public void Write_Given_AFileHeldByAProgramThatSharesOnlyReading_Then_TheSaveFailsAndTheFileIsAsItWas()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = Existing();

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Should.Throw<IOException>(() => Write(path));
            }

            Read(path).ShouldBe(s_Old);
            Files().ShouldBe([path]);

            // The write in place failed too: nothing was written in place, and nothing says that it was.
            m_InPlace.ShouldBeEmpty();
        }

        // The program lets go of the file while the save tries: the save puts the file in place, and writes nothing in place.
        [Fact]
        public async Task Write_Given_AFileThatAProgramLetsGoOfInTime_Then_TheNewContentInItsPlace()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = Existing();
            var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Task release = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300));
                holder.Dispose();
            });

            FileStreamHelper.Write(path, stream => stream.Write(s_New), TimeSpan.FromSeconds(20), (file, reason) => m_InPlace.Add((file, reason)));
            await release;

            Read(path).ShouldBe(s_New);
            m_InPlace.ShouldBeEmpty();
        }

        #endregion

        #region A link

        // The save writes the file that the link points to, and the link stays.
        [Fact]
        public void Write_Given_ALinkToAFile_Then_TheFileBehindTheLinkHasTheNewContentAndTheLinkStays()
        {
            string real = Path.Combine(Directory.CreateDirectory(Path.Combine(m_Folder, @"real")).FullName, @"plan.zpp");
            File.WriteAllBytes(real, s_Old);
            string link = FilePath(@"plan.zpp");

            try
            {
                File.CreateSymbolicLink(link, real);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // This user cannot make a link (Windows without the right to do it).
                return;
            }

            Write(link);

            new FileInfo(link).LinkTarget.ShouldNotBeNull(@"the save put a file in the place of the link");
            Read(real).ShouldBe(s_New);
            Read(link).ShouldBe(s_New);
            Files().ShouldBe(new[] { link, real }.Order(StringComparer.Ordinal));
        }

        #endregion

        #region The saves of the application

        [Fact]
        public void Save_Given_AHeldFileThatSharesAll_Then_TheNewContent()
        {
            string path = Existing();

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                FileStreamHelper.Save(path, stream => stream.Write(s_New));
            }

            Read(path).ShouldBe(s_New);
            Files().ShouldBe([path]);
        }

        [Fact]
        public async Task SaveAsync_Given_AFileThere_Then_TheNewContentAndNoTemporaryFile()
        {
            string path = Existing();

            await FileStreamHelper.SaveAsync(path, stream => stream.WriteAsync(s_New).AsTask());

            Read(path).ShouldBe(s_New);
            Files().ShouldBe([path]);
        }

        [Fact]
        public void Replace_Given_AHiddenFile_Then_TheNewContentAndTheFileStaysHidden()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = Existing(@"UserSettings.json");
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);

            FileStreamHelper.Replace(path, stream => stream.Write(s_New));

            Read(path).ShouldBe(s_New);
            File.GetAttributes(path).HasFlag(FileAttributes.Hidden).ShouldBeTrue();
        }

        #endregion
    }
}
