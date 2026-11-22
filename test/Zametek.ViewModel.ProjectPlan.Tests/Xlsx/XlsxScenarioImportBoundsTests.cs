using Shouldly;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;
using Zametek.Common.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.XlsxCellSpec;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// How much of a workbook the import reads. A workbook is a file that anyone can make, and a small file can say that it
    /// has a very large sheet. The import reads the rows and the cells that the file holds, and no row and no cell that the
    /// file only has a place for. It refuses a workbook that holds more than a plan needs, at once, with one problem.
    /// </summary>
    public class XlsxScenarioImportBoundsTests
    {
        private static readonly TimeProvider s_Clock =
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

        private static readonly string s_Activities = Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities;
        private static readonly string s_ActivityTracker = Resource.ProjectPlan.Reporting.Reporting_WorksheetActivityTracker;
        private static readonly string[] s_Known = [nameof(ActivityModel.Id), nameof(ActivityModel.Name)];

        // A part that holds nothing of a workbook.
        private const string c_PaddingPart = @"xl/media/padding.bin";

        #region Helpers

        private static ProjectScenarioImportModel Import(
            byte[] workbook,
            CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream(workbook);
            return new XlsxScenarioFileImporter(new DateTimeCalculator(s_Clock)).ImportProjectScenarioXlsxFile(stream, cancellationToken);
        }

        private static ScenarioImportException ImportFailure(byte[] workbook) =>
            Should.Throw<ScenarioImportException>(() => Import(workbook));

        // The titles of the first columns: Id, and then a title for each other column.
        private static (string Column, XlsxCellSpec Cell)[] Titles(int count) =>
            [.. Enumerable.Range(1, count).Select(i => (XlsxPackageReader.ColumnLetters(i), TitleOf(i == 1 ? nameof(ActivityModel.Id) : $@"T{i}")))];

        // A sheet with the title Id, and under it a row for each Id from 1.
        private static void Ids(XlsxSheetSpec sheet, int count)
        {
            sheet.Row(1, ("A", TitleOf(nameof(ActivityModel.Id))));

            for (int i = 0; i < count; i++)
            {
                sheet.Row(i + 2, ("A", NumberOf(i + 1)));
            }
        }

        // The workbook with one more part, which holds nothing of a workbook and packs to almost nothing.
        private static byte[] WithPadding(byte[] workbook, long length)
        {
            using var package = new MemoryStream();
            package.Write(workbook);

            using (var zip = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true))
            {
                using Stream entry = zip.CreateEntry(@"xl/media/padding.bin", CompressionLevel.Fastest).Open();
                var block = new byte[1 << 20];

                for (long written = 0; written < length; written += block.Length)
                {
                    entry.Write(block, 0, (int)Math.Min(block.Length, length - written));
                }
            }

            return package.ToArray();
        }

        // The workbook with one more part, which the file stores as it is, without compression. The records of the part
        // say that it holds nothing when it is unpacked. The reader of the package reads the stored bytes of such a part,
        // whatever the records say.
        private static byte[] WithStoredPadding(byte[] workbook, long length)
        {
            using var package = new MemoryStream();

            using (var source = new ZipArchive(new MemoryStream(workbook)))
            using (var zip = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (ZipArchiveEntry part in source.Entries)
                {
                    using Stream from = part.Open();
                    using Stream to = zip.CreateEntry(part.FullName).Open();
                    from.CopyTo(to);
                }

                using Stream entry = zip.CreateEntry(c_PaddingPart, CompressionLevel.NoCompression).Open();
                var block = new byte[1 << 20];

                for (long written = 0; written < length; written += block.Length)
                {
                    entry.Write(block, 0, (int)Math.Min(block.Length, length - written));
                }
            }

            byte[] bytes = package.ToArray();
            SayThatThePartHoldsNothing(bytes, c_PaddingPart);

            using (var check = new ZipArchive(new MemoryStream(bytes)))
            {
                ZipArchiveEntry padding = check.GetEntry(c_PaddingPart).ShouldNotBeNull();
                (padding.Length, padding.CompressedLength).ShouldBe((0, length));
            }

            return bytes;
        }

        // Puts 0 as the unpacked size of a stored part in its two records: its entry in the central directory, and its
        // local header.
        private static void SayThatThePartHoldsNothing(byte[] zip, string name)
        {
            // The record at the end of the central directory gives the number of entries and the place of the directory.
            int end = zip.Length - 22;

            while (BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(end)) != 0x06054b50)
            {
                end--;
            }

            int entries = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(end + 10));
            int place = (int)BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(end + 16));
            int found = 0;

            for (int i = 0; i < entries; i++)
            {
                BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(place)).ShouldBe(0x02014b50u);
                int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(place + 28));
                int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(place + 30));
                int commentLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(place + 32));

                if (Encoding.UTF8.GetString(zip, place + 46, nameLength) == name)
                {
                    // Method 0: stored.
                    BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(place + 10)).ShouldBe((ushort)0);
                    BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(place + 24), 0);

                    int local = (int)BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(place + 42));
                    BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(local)).ShouldBe(0x04034b50u);
                    BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(local + 22), 0);
                    found++;
                }

                place += 46 + nameLength + extraLength + commentLength;
            }

            found.ShouldBe(1);
        }

        #endregion

        #region The rows and the cells that the file holds

        // A sheet says where its last row is. The rows between are not in the file, and the import does not go to them.
        [Fact]
        public void Table_Given_OneValueInTheLastRowOfASheet_Then_OnlyTheCellsOfThatRowAreRead()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Row(1, Titles(300)).Row(1_048_576, ("A", NumberOf(7))))
                .Build();
            var issues = new ImportIssueCollector();
            using var stream = new MemoryStream(workbook);
            using XlsxWorkbookReader reader = XlsxWorkbookReader.Open(stream, issues);

            ImportTable table = reader.Table(s_Activities, s_Known).ShouldNotBeNull();

            table.Rows.Select(x => x.Number).ShouldBe([1_048_576]);
            table.Rows[0].RequiredId(nameof(ActivityModel.Id)).ShouldBe(7);
            issues.Count.ShouldBe(0);

            // The 300 titles, and the one cell of the row.
            reader.CellsRead.ShouldBe(301);
        }

        // A row is read up to its last cell that holds something, and not up to the last title.
        [Fact]
        public void Table_Given_ManyTitlesAndShortRows_Then_TheCellsAfterTheLastValueOfARowAreNotRead()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_ActivityTracker, s =>
                {
                    s.Row(1, Titles(16_384));
                    s.Row(2, ("A", NumberOf(1)));
                    s.Row(3, ("A", NumberOf(2)), ("C", NumberOf(50)));
                })
                .Build();
            var issues = new ImportIssueCollector();
            using var stream = new MemoryStream(workbook);
            using XlsxWorkbookReader reader = XlsxWorkbookReader.Open(stream, issues);

            ImportTable table = reader.Table(s_ActivityTracker, s_Known).ShouldNotBeNull();

            table.Titles.Count.ShouldBe(16_384);
            table.Rows.Count.ShouldBe(2);
            reader.CellsRead.ShouldBe(16_384 + 1 + 3);

            // A cell after the last value of a row is a blank cell, by its place and by its title.
            table.Rows[1].IntAt(2).ShouldBe(50);
            table.Rows[1].IntAt(1).ShouldBeNull();
            table.Rows[1].IntAt(3).ShouldBeNull();
            table.Rows[0].IntAt(16_383).ShouldBeNull();
            table.Rows[0].Cell(@"T16384").IsEmpty.ShouldBeTrue();
            issues.Count.ShouldBe(0);
        }

        // A row that holds values only in columns that have no title is not a row of the table, as before.
        [Fact]
        public void Table_Given_ARowWithValuesOnlyAfterTheTitles_Then_ItIsNotARow()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, Titles(2))
                    .Row(2, ("A", NumberOf(1)))
                    .Row(3, ("F", NumberOf(9)))
                    .Row(5, ("B", TextOf(@"Wall"))))
                .Build();
            var issues = new ImportIssueCollector();
            using var stream = new MemoryStream(workbook);
            using XlsxWorkbookReader reader = XlsxWorkbookReader.Open(stream, issues);

            ImportTable table = reader.Table(s_Activities, s_Known).ShouldNotBeNull();

            table.Rows.Select(x => x.Number).ShouldBe([2, 5]);
        }

        [Fact]
        public void Import_Given_AWideTrackerSheetOfShortRows_Then_TheWorkbookIsImported()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => Ids(s, 2_000))
                .Sheet(s_ActivityTracker, s =>
                {
                    s.Row(1, Titles(16_384));

                    for (int i = 0; i < 2_000; i++)
                    {
                        s.Row(i + 2, ("A", NumberOf(i + 1)), ("C", NumberOf(50)));
                    }
                })
                .Build();

            ProjectScenarioImportModel model = Import(workbook);

            model.DependentActivities.Count.ShouldBe(2_000);
            model.DependentActivities.ShouldAllBe(x => x.Activity.Trackers.Count == 1 && x.Activity.Trackers[0].Time == 1);
        }

        #endregion

        #region The bounds

        [Fact]
        public void Import_Given_ASheetWithTheMostRows_Then_TheWorkbookIsImported()
        {
            byte[] workbook = new XlsxPackageBuilder().Sheet(s_Activities, s => Ids(s, XlsxWorkbookReader.MaximumRows)).Build();

            Import(workbook).DependentActivities.Count.ShouldBe(XlsxWorkbookReader.MaximumRows);
        }

        [Fact]
        public void Import_Given_ASheetWithMoreRowsThanTheMost_Then_TheImportStopsWithOneProblemForTheSheet()
        {
            byte[] workbook = new XlsxPackageBuilder().Sheet(s_Activities, s => Ids(s, XlsxWorkbookReader.MaximumRows + 1)).Build();

            ScenarioImportException failure = ImportFailure(workbook);

            failure.TotalCount.ShouldBe(1);
            ImportIssue issue = failure.Issues.Single();
            (issue.Code, issue.Sheet, issue.Row).ShouldBe((ImportIssueCodes.TooManyRows, s_Activities, null));
            issue.Message.ShouldBe(@"The sheet has more than 10,000 rows that hold a value. The import reads no sheet that has more.");
        }

        // Each row holds a value in its first cell and in its last one, and so the import must read all the cells between.
        [Fact]
        public void Import_Given_MoreCellsThanTheMost_Then_TheImportStopsWithOneProblemForTheWorkbook()
        {
            int rows = (int)(XlsxWorkbookReader.MaximumCells / 16_384) + 1;
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => Ids(s, rows))
                .Sheet(s_ActivityTracker, s =>
                {
                    s.Row(1, Titles(16_384));

                    for (int i = 0; i < rows; i++)
                    {
                        s.Row(i + 2, ("A", NumberOf(i + 1)), ("XFD", NumberOf(50)));
                    }
                })
                .Build();

            ScenarioImportException failure = ImportFailure(workbook);

            failure.TotalCount.ShouldBe(1);
            ImportIssue issue = failure.Issues.Single();
            (issue.Code, issue.Sheet, issue.Row).ShouldBe((ImportIssueCodes.TooManyCells, null, null));
            issue.Message.ShouldBe(@"The workbook has more cells than the import reads (10,000,000).");
        }

        [Fact]
        public void Import_Given_PartsOfTheLargestSize_Then_TheWorkbookIsImported()
        {
            byte[] small = new XlsxPackageBuilder().Sheet(s_Activities, s => Ids(s, 3)).Build();
            long parts;

            using (var zip = new ZipArchive(new MemoryStream(small)))
            {
                parts = zip.Entries.Sum(x => Math.Max(x.Length, x.CompressedLength));
            }

            byte[] workbook = WithPadding(small, XlsxWorkbookReader.MaximumUnpackedBytes - parts);

            Import(workbook).DependentActivities.Count.ShouldBe(3);
        }

        // The file is small, and its parts are large when they are unpacked. The import does not unpack them.
        [Fact]
        public void Import_Given_PartsLargerThanTheLargestSize_Then_TheImportStopsWithOneProblemBeforeItReadsTheWorkbook()
        {
            byte[] small = new XlsxPackageBuilder().Sheet(s_Activities, s => Ids(s, 3)).Build();
            byte[] workbook = WithPadding(small, XlsxWorkbookReader.MaximumUnpackedBytes);
            workbook.Length.ShouldBeLessThan(1 << 20);

            ScenarioImportException failure = ImportFailure(workbook);

            failure.TotalCount.ShouldBe(1);
            ImportIssue issue = failure.Issues.Single();
            (issue.Code, issue.Sheet, issue.Row).ShouldBe((ImportIssueCodes.WorkbookTooLarge, null, null));
            issue.Message.ShouldBe(@"The workbook holds 101 MB when it is unpacked. The import reads no workbook that holds more than 100 MB.");
        }

        // A part that the file stores without compression is as large as its stored bytes, whatever its records say. The
        // bound counted the records only, and the import read such a workbook.
        [Fact]
        public void Import_Given_StoredPartsLargerThanTheLargestSizeWhoseRecordsSayNothing_Then_TheImportStopsWithOneProblemBeforeItReadsTheWorkbook()
        {
            byte[] small = new XlsxPackageBuilder().Sheet(s_Activities, s => Ids(s, 3)).Build();
            byte[] workbook = WithStoredPadding(small, XlsxWorkbookReader.MaximumUnpackedBytes);

            ScenarioImportException failure = ImportFailure(workbook);

            failure.TotalCount.ShouldBe(1);
            ImportIssue issue = failure.Issues.Single();
            (issue.Code, issue.Sheet, issue.Row).ShouldBe((ImportIssueCodes.WorkbookTooLarge, null, null));
            issue.Message.ShouldBe(@"The workbook holds 101 MB when it is unpacked. The import reads no workbook that holds more than 100 MB.");
        }

        // Such a part does not make the workbook unreadable: the bound only counts it.
        [Fact]
        public void Import_Given_AStoredPartWhoseRecordsSayNothing_Then_TheWorkbookIsImported()
        {
            byte[] small = new XlsxPackageBuilder().Sheet(s_Activities, s => Ids(s, 3)).Build();

            Import(WithStoredPadding(small, 1 << 20)).DependentActivities.Count.ShouldBe(3);
        }

        #endregion

        #region A stop

        [Fact]
        public void Import_Given_ACancellationBeforeTheImport_Then_TheImportStopsAndTheWorkbookIsNotRead()
        {
            byte[] workbook = new XlsxPackageBuilder().Sheet(s_Activities, s => Ids(s, 3)).Build();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Should.Throw<OperationCanceledException>(() => Import(workbook, cancellation.Token));
        }

        // The reader asks before the titles of a sheet.
        [Fact]
        public void Table_Given_ACancellationAfterTheWorkbookIsOpen_Then_TheSheetIsNotRead()
        {
            byte[] workbook = new XlsxPackageBuilder().Sheet(s_Activities, s => Ids(s, 3)).Build();
            using var cancellation = new CancellationTokenSource();
            var issues = new ImportIssueCollector();
            using var stream = new MemoryStream(workbook);
            using XlsxWorkbookReader reader = XlsxWorkbookReader.Open(stream, issues, cancellation.Token);
            cancellation.Cancel();

            Should.Throw<OperationCanceledException>(() => reader.Table(s_Activities, s_Known));

            reader.CellsRead.ShouldBe(0);
        }

        // The stop comes between two rows: the reader asks before each row.
        [Fact]
        public void Table_Given_ACancellationAfterTheFirstRow_Then_TheNextRowsAreNotRead()
        {
            byte[] workbook = new XlsxPackageBuilder().Sheet(s_Activities, s => Ids(s, 3)).Build();
            using var cancellation = new CancellationTokenSource();
            var issues = new ImportIssueCollector();
            using var stream = new MemoryStream(workbook);
            using XlsxWorkbookReader reader = XlsxWorkbookReader.Open(stream, issues, cancellation.Token);
            List<int> rowsRead = [];
            reader.RowRead = row =>
            {
                rowsRead.Add(row);
                cancellation.Cancel();
            };

            Should.Throw<OperationCanceledException>(() => reader.Table(s_Activities, s_Known));

            rowsRead.ShouldBe([2]);

            // The title, and the one cell of the first row.
            reader.CellsRead.ShouldBe(2);
        }

        #endregion
    }
}
