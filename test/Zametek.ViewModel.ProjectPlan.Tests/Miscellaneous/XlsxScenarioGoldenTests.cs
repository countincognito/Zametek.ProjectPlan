using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Two files that hold what the exporter and the importer did when the files were made: workbooks that the exporter wrote of
    /// the sample plan, and what the importer made of each (the snapshots, in JSON). They are in the folder TestFiles/Xlsx.
    /// </summary>
    /// <remarks>
    /// A workbook of the exporter has the same content however it is written, and so a new library that writes the workbooks
    /// must give workbooks that say what these say. And a workbook that has already been written, by anyone, must give the same
    /// plan as it gave: the importer reads these as it reads the files of the people who use the application. A change that is
    /// meant is made by making the files again: set the variable ZPP_REGENERATE_XLSX_GOLDENS to 1 and run the test Regenerate.
    /// </remarks>
    public class XlsxScenarioGoldenTests
    {
        private const string c_Regenerate = @"ZPP_REGENERATE_XLSX_GOLDENS";

        // The workbooks that the exporter makes of the sample: in days, with dates, and with the settings and a result of the
        // simulation.
        public static IEnumerable<object[]> Exports =>
        [
            [@"sample-days", false, false],
            [@"sample-dates", true, false],
            [@"sample-simulation", false, true],
        ];

        // The workbooks that are imported, and what the importer made of them. The last is the file of the documents: the sample
        // that a person is given to look at.
        public static IEnumerable<object[]> Imports =>
        [
            [@"sample-days"],
            [@"sample-dates"],
            [@"sample-simulation"],
            [@"docs-sample"],
        ];

        private static string FolderToRead => Path.Combine(@"TestFiles", @"Xlsx");

        // The folder of the source, which is where a new file is written.
        private static string FolderToWrite
        {
            get
            {
                DirectoryInfo? directory = new(AppContext.BaseDirectory);

                while (directory is not null && !directory.GetFiles(@"Zametek.ProjectPlan.slnx").Any())
                {
                    directory = directory.Parent;
                }

                return directory is null
                    ? throw new DirectoryNotFoundException(@"The folder of the solution was not found above the tests.")
                    : Path.Combine(directory.FullName, @"test", @"Zametek.ViewModel.ProjectPlan.Tests", @"TestFiles", @"Xlsx");
            }
        }

        private static readonly JsonSerializerSettings s_Json = new()
        {
            Formatting = Formatting.Indented,
            Converters = [new StringEnumConverter(), new DayAndTimeConverter()],
        };

        // A date of the importer has the offset of the machine, and that is not the same on every machine. The snapshot holds the
        // day and the time that the date says.
        private sealed class DayAndTimeConverter : JsonConverter<DateTimeOffset>
        {
            public override void WriteJson(JsonWriter writer, DateTimeOffset value, JsonSerializer serializer) =>
                writer.WriteValue(value.DateTime.ToString(@"yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));

            public override DateTimeOffset ReadJson(JsonReader reader, Type objectType, DateTimeOffset existingValue, bool hasExistingValue, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }

        private static string Snapshot(ProjectScenarioImportModel import) =>
            JsonConvert.SerializeObject(import, s_Json).ReplaceLineEndings("\n") + "\n";

        private static ProjectScenarioImportModel Import(byte[] workbook)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(XlsxSampleExport.Culture);
                using var stream = new MemoryStream(workbook);
                return new XlsxScenarioFileImporter(new DateTimeCalculator(XlsxSampleExport.Clock)).ImportProjectScenarioXlsxFile(stream);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static async Task<byte[]> ExportAsync(bool showDates, bool simulation)
        {
            var (scenario, resources, tracking) = await XlsxSampleExport.SampleAsync();
            return XlsxSampleExport.Export(simulation ? XlsxSampleExport.WithSimulation(scenario) : scenario, resources, tracking, showDates);
        }

        [Theory]
        [MemberData(nameof(Exports))]
        public async Task Export_Given_TheSample_Then_TheWorkbookSaysWhatTheGoldenWorkbookSays(string name, bool showDates, bool simulation)
        {
            XlsxBook golden = XlsxPackageReader.Read(File.ReadAllBytes(Path.Combine(FolderToRead, $@"{name}.xlsx")));

            XlsxBook actual = XlsxPackageReader.Read(await ExportAsync(showDates, simulation));

            // The sheets, and every cell that holds a value. The format of a date is the short date of the machine's culture.
            string difference = XlsxBook.Difference(golden.Describe(dateFormats: false), actual.Describe(dateFormats: false));
            difference.ShouldBeEmpty();
        }

        [Theory]
        [MemberData(nameof(Imports))]
        public void Import_Given_AWorkbookThatWasMadeBefore_Then_ThePlanIsTheOneThatItGave(string name)
        {
            ProjectScenarioImportModel import = Import(File.ReadAllBytes(Path.Combine(FolderToRead, $@"{name}.xlsx")));

            Snapshot(import).ShouldBe(File.ReadAllText(Path.Combine(FolderToRead, $@"{name}.import.json")).ReplaceLineEndings("\n"));
        }

        // Makes the files again, from what the exporter and the importer do now. It does nothing unless the variable says so.
        [Fact]
        public async Task Regenerate_Given_TheVariable_Then_TheFilesAreMadeAgain()
        {
            if (Environment.GetEnvironmentVariable(c_Regenerate) != @"1")
            {
                return;
            }

            Directory.CreateDirectory(FolderToWrite);

            foreach (object[] export in Exports)
            {
                File.WriteAllBytes(Path.Combine(FolderToWrite, $@"{export[0]}.xlsx"), await ExportAsync((bool)export[1], (bool)export[2]));
            }

            foreach (object[] import in Imports.Where(x => (string)x[0] != @"docs-sample"))
            {
                byte[] workbook = File.ReadAllBytes(Path.Combine(FolderToWrite, $@"{import[0]}.xlsx"));
                File.WriteAllText(Path.Combine(FolderToWrite, $@"{import[0]}.import.json"), Snapshot(Import(workbook)));
            }

            // The file of the documents is not made here, and so its snapshot is made from the file itself.
            string docs = Path.Combine(Path.GetDirectoryName(FolderToWrite)!, @"..", @"..", @"..", @"docs", @"assets", @"sample.xlsx");
            File.WriteAllText(Path.Combine(FolderToWrite, @"docs-sample.import.json"), Snapshot(Import(File.ReadAllBytes(docs))));
        }
    }
}
