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

        // The saves build the whole file in memory and write it only once that has succeeded, so a failure part-way
        // through leaves the file already there as it was, rather than truncated or half written.

        public static void Save(string filename, Action<Stream> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(write);

            using var buffer = new MemoryStream();
            write(buffer);

            using FileStream stream = CreateFile(filename);
            buffer.WriteTo(stream);
        }

        public static async Task SaveAsync(string filename, Func<Stream, Task> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(write);

            using var buffer = new MemoryStream();
            await write(buffer);

            await using FileStream stream = CreateFile(filename);
            buffer.Position = 0;
            await buffer.CopyToAsync(stream);
        }

        // Writes a file by way of a temporary file in the same folder, which then takes the place of the file. A write that
        // fails, or a process that stops part way, leaves the file that was there whole. The saves above empty the file
        // before they copy into it, and so they can leave a part of a file. This is for the small files of the settings,
        // which the desktop writes at each change.
        public static void Replace(string filename, Action<Stream> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(write);
            string temporary = $@"{filename}.tmp";

            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    write(stream);

                    // On the disk before the move, so that a stop of the computer does not leave an empty file.
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporary, filename, overwrite: true);
            }
            catch
            {
                DeleteIfPossible(temporary);
                throw;
            }
        }

        // Removes what a write that failed left. The failure of the write is what the caller hears of.
        private static void DeleteIfPossible(string filename)
        {
            try
            {
                File.Delete(filename);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The temporary file stays. The next write takes its place.
            }
        }

        // Replaces any existing file, and lets others read it while it is written.
        private static FileStream CreateFile(string filename) =>
            new(filename, FileMode.Create, FileAccess.Write, FileShare.Read);
    }
}
