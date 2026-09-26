namespace Zametek.Graphs.Avalonia
{
    // Saves a file through a writer that takes a stream, for the Save-As path, which has the file name the user picked
    // where the exporters take a stream. A focused copy of the application's FileStreamHelper, kept local so that the
    // library stays standalone, and internal so that it never clashes with the application's public one in consumers
    // that import both namespaces.
    internal static class FileStreamHelper
    {
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
