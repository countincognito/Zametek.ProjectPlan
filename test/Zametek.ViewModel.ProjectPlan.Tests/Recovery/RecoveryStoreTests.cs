using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The recovery copies as files: a run of the application has one copy, another run finds it only when the first run is
    /// no longer alive, and a write that fails leaves the copy before it.
    /// </summary>
    public sealed class RecoveryStoreTests
        : IDisposable
    {
        private static readonly DateTimeOffset s_Now = new(2026, 11, 18, 9, 30, 0, TimeSpan.FromHours(1));

        private readonly string m_Folder = Path.Combine(Path.GetTempPath(), $"zpp-recovery-{Guid.NewGuid():N}");

        public void Dispose()
        {
            try
            {
                Directory.Delete(m_Folder, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only. A folder that a test never made is one such case.
            }
        }

        private static RecoveryCopyModel CopyOf(string title, DateTimeOffset? writtenAt = null) =>
            new()
            {
                ProjectFile = $@"C:\plans\{title}.zpp",
                Title = title,
                WrittenAt = writtenAt ?? s_Now,
                Reason = RecoveryCopyReasons.Interval,
            };

        private static Func<Stream, Task> Text(string text) =>
            stream => stream.WriteAsync(Encoding.UTF8.GetBytes(text)).AsTask();

        // A run that ended: it wrote its copy, and it holds its lock file no longer.
        private async Task<RecoveryCopyModel> LeaveCopyAsync(string title, string content, DateTimeOffset? writtenAt = null)
        {
            using var ended = new RecoveryStore(m_Folder);
            ended.BeginSession();
            return await ended.WriteCopyAsync(CopyOf(title, writtenAt), Text(content));
        }

        [Fact]
        public async Task WriteCopyAsync_Given_ACopy_Then_ItIsAProjectFileInTheFolderAndTheStoreHasIt()
        {
            using var store = new RecoveryStore(m_Folder);
            store.BeginSession();
            store.Copy.ShouldBeNull();

            RecoveryCopyModel written = await store.WriteCopyAsync(CopyOf(@"wall"), Text(@"first"));

            Path.GetDirectoryName(written.Filename).ShouldBe(m_Folder);
            Path.GetExtension(written.Filename).ShouldBe($@".{Resource.ProjectPlan.Filters.Filter_ProjectFileExtension}");
            File.ReadAllText(written.Filename).ShouldBe(@"first");
            store.Copy.ShouldBe(written);
            written.ShouldBe(CopyOf(@"wall") with { Filename = written.Filename });
        }

        [Fact]
        public async Task WriteCopyAsync_Given_ASecondCopy_Then_ItTakesThePlaceOfTheFirst()
        {
            using var store = new RecoveryStore(m_Folder);
            store.BeginSession();
            RecoveryCopyModel first = await store.WriteCopyAsync(CopyOf(@"wall"), Text(@"first"));

            RecoveryCopyModel second = await store.WriteCopyAsync(CopyOf(@"wall", s_Now.AddMinutes(5)), Text(@"second"));

            second.Filename.ShouldBe(first.Filename);
            File.ReadAllText(second.Filename).ShouldBe(@"second");
            Directory.GetFiles(m_Folder, @"*.zpp").Length.ShouldBe(1);
            Directory.GetFiles(m_Folder, @"*.tmp").ShouldBeEmpty();
        }

        [Fact]
        public async Task WriteCopyAsync_Given_AProjectThatCannotBeWritten_Then_TheCopyBeforeItStays()
        {
            using var store = new RecoveryStore(m_Folder);
            store.BeginSession();
            RecoveryCopyModel first = await store.WriteCopyAsync(CopyOf(@"wall"), Text(@"first"));

            await Should.ThrowAsync<InvalidOperationException>(
                () => store.WriteCopyAsync(
                    CopyOf(@"wall", s_Now.AddMinutes(5)),
                    async stream =>
                    {
                        await stream.WriteAsync(Encoding.UTF8.GetBytes(@"a part"));
                        throw new InvalidOperationException(@"The project cannot be written.");
                    }));

            File.ReadAllText(first.Filename).ShouldBe(@"first");
            store.Copy.ShouldBe(first);
        }

        [Fact]
        public async Task DeleteCopy_Given_ACopy_Then_NoFileOfItStays()
        {
            using var store = new RecoveryStore(m_Folder);
            store.BeginSession();
            await store.WriteCopyAsync(CopyOf(@"wall"), Text(@"first"));

            store.DeleteCopy();

            store.Copy.ShouldBeNull();
            Directory.GetFiles(m_Folder, @"*.zpp").ShouldBeEmpty();
            Directory.GetFiles(m_Folder, @"*.json").ShouldBeEmpty();

            // A second time does nothing.
            store.DeleteCopy();
        }

        [Fact]
        public void FindLeftCopies_Given_NoFolder_Then_None()
        {
            using var store = new RecoveryStore(m_Folder);

            store.FindLeftCopies().ShouldBeEmpty();
            Directory.Exists(m_Folder).ShouldBeFalse(@"a look for copies made the folder");
        }

        [Fact]
        public async Task FindLeftCopies_Given_CopiesOfRunsThatEnded_Then_EachWithWhatItIsOfTheNewestFirst()
        {
            RecoveryCopyModel older = await LeaveCopyAsync(@"wall", @"older", s_Now.AddHours(-2));
            RecoveryCopyModel newer = await LeaveCopyAsync(@"roof", @"newer", s_Now);
            using var store = new RecoveryStore(m_Folder);
            store.BeginSession();

            store.FindLeftCopies().ShouldBe([newer, older]);
        }

        // Two runs of the application at one time: the copy of the other run is that run's, for as long as it is alive.
        [Fact]
        public async Task FindLeftCopies_Given_TheCopyOfARunThatIsAlive_Then_ItIsNotOneOfThem()
        {
            using var alive = new RecoveryStore(m_Folder);
            alive.BeginSession();
            RecoveryCopyModel copy = await alive.WriteCopyAsync(CopyOf(@"wall"), Text(@"alive"));
            using var store = new RecoveryStore(m_Folder);
            store.BeginSession();

            store.FindLeftCopies().ShouldBeEmpty();

            // The other run ends, with no chance to remove its copy.
            alive.Dispose();

            store.FindLeftCopies().ShouldBe([copy]);
        }

        [Fact]
        public async Task FindLeftCopies_Given_TheCopyOfThisRun_Then_ItIsNotOneOfThem()
        {
            using var store = new RecoveryStore(m_Folder);
            store.BeginSession();
            await store.WriteCopyAsync(CopyOf(@"wall"), Text(@"mine"));

            store.FindLeftCopies().ShouldBeEmpty();
        }

        // A run that ended between the copy and the file that says what the copy is of, or a file that a person damaged.
        [Theory]
        [InlineData(null)]
        [InlineData(@"not JSON")]
        [InlineData(@"[1, 2]")]
        [InlineData(@"{ ""Title"": 5, ""WrittenAt"": ""soon"" }")]
        public async Task FindLeftCopies_Given_ACopyWithNoWordOfWhatItIsOf_Then_ItIsACopyWithNoTitle(string? info)
        {
            RecoveryCopyModel left = await LeaveCopyAsync(@"wall", @"content");
            string infoFilename = Path.ChangeExtension(left.Filename, @".json");

            if (info is null)
            {
                File.Delete(infoFilename);
            }
            else
            {
                File.WriteAllText(infoFilename, info);
            }

            using var store = new RecoveryStore(m_Folder);
            RecoveryCopyModel found = store.FindLeftCopies().ShouldHaveSingleItem();

            found.Filename.ShouldBe(left.Filename);
            found.ProjectFile.ShouldBeEmpty();
            found.WrittenAt.UtcDateTime.ShouldBe(File.GetLastWriteTimeUtc(left.Filename));
        }

        // A run that ended during the write of a copy left the temporary file of the write. That file is not a copy, and it
        // goes when a later run looks for copies. The copy before it stays.
        [Fact]
        public async Task FindLeftCopies_Given_ATemporaryFileOfARunThatEnded_Then_ItGoesAndTheCopyBeforeItStays()
        {
            RecoveryCopyModel left = await LeaveCopyAsync(@"wall", @"first");
            string second = $@"{left.Filename}.tmp";
            File.WriteAllText(second, @"a part of a second copy");
            string first = Path.Combine(m_Folder, $@"{Guid.NewGuid():N}{Path.GetExtension(left.Filename)}.tmp");
            File.WriteAllText(first, @"a part of a first copy");
            using var store = new RecoveryStore(m_Folder);
            store.BeginSession();

            store.FindLeftCopies().ShouldBe([left]);

            File.Exists(second).ShouldBeFalse();
            File.Exists(first).ShouldBeFalse();
            File.ReadAllText(left.Filename).ShouldBe(@"first");
        }

        [Fact]
        public async Task FindLeftCopies_Given_ATemporaryFileOfARunThatIsAlive_Then_ItStays()
        {
            using var alive = new RecoveryStore(m_Folder);
            alive.BeginSession();
            RecoveryCopyModel copy = await alive.WriteCopyAsync(CopyOf(@"wall"), Text(@"alive"));
            string temporary = $@"{copy.Filename}.tmp";
            File.WriteAllText(temporary, @"a write that is on its way");
            using var store = new RecoveryStore(m_Folder);
            store.BeginSession();

            store.FindLeftCopies().ShouldBeEmpty();

            File.Exists(temporary).ShouldBeTrue();
        }

        [Fact]
        public async Task Remove_Given_ACopyThatARunLeft_Then_NoFileOfItStaysAndTheOthersDo()
        {
            RecoveryCopyModel first = await LeaveCopyAsync(@"wall", @"first", s_Now.AddHours(-2));
            RecoveryCopyModel second = await LeaveCopyAsync(@"roof", @"second");
            using var store = new RecoveryStore(m_Folder);

            store.Remove(first);

            store.FindLeftCopies().ShouldBe([second]);
            Directory.GetFiles(m_Folder).Select(Path.GetFileNameWithoutExtension).Distinct().ShouldHaveSingleItem();
        }

        // The name of a copy is a name in the folder of the store. A copy that says another folder removes nothing there.
        [Fact]
        public async Task Remove_Given_ACopyWithAFileInAnotherFolder_Then_ThatFileStays()
        {
            await LeaveCopyAsync(@"wall", @"first");
            string other = Path.Combine(m_Folder, @"other");
            Directory.CreateDirectory(other);
            string plan = Path.Combine(other, @"plan.zpp");
            File.WriteAllText(plan, @"a plan of the user");
            using var store = new RecoveryStore(m_Folder);

            store.Remove(new RecoveryCopyModel { Filename = plan });

            File.ReadAllText(plan).ShouldBe(@"a plan of the user");
        }
    }
}
