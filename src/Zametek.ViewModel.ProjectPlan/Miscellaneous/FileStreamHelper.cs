using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // The file end of the stream-based file layer. Opening, saving, importing and exporting all read and write streams,
    // and the callers that start from a file name - the desktop's dialogs and zpp's options - open the file here. Shared
    // by the view models and the command line; the graph library keeps its own copy of the save, so that it stays
    // standalone.
    public static class FileStreamHelper
    {
        // Opens a file to import. An MS Project plan is opened sharing both reading and writing, as MPXJ opened it when it
        // was handed the file name, so that a plan MS Project still has open can be imported.
        public static FileStream OpenImportFile(string filename, ProjectScenarioImportFormat format)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);

            return format == ProjectScenarioImportFormat.MicrosoftProject
                ? new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                : File.OpenRead(filename);
        }

        public static void Save(string filename, Action<Stream> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(write);

            using FileStream stream = CreateFile(filename);
            write(stream);
        }

        public static async Task SaveAsync(string filename, Func<Stream, Task> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(write);

            await using FileStream stream = CreateFile(filename);
            await write(stream);
        }

        // Replaces any existing file, and lets others read it while it is written.
        private static FileStream CreateFile(string filename) =>
            new(filename, FileMode.Create, FileAccess.Write, FileShare.Read);
    }
}
