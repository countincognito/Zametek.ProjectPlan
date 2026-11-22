using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // The file end of the stream-based file layer. Opening, saving, importing and exporting all read and write streams,
    // and the callers that start from a file name - the desktop's dialogs and zpp's options - open the file here. Shared
    // by the view models and the command line; the graph library keeps its own copy of the save, so that it stays
    // standalone.
    public static class FileStreamHelper
    {
        // What a write adds to the name of a file for its temporary file.
        internal const string TemporaryExtension = @".tmp";

        // The time that a write tries to put its temporary file in the place of a file that another program holds open.
        private static readonly TimeSpan s_RetryTime = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan s_RetryPause = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Raised when a write could not put a temporary file in the place of its target, and wrote the target in place
        /// (see <see cref="Write"/>). It gives the file and the reason. A host writes it to its log.
        /// </summary>
        public static event Action<string, Exception>? WroteInPlace;

        // Opens a file to import. An MS Project plan is opened sharing both reading and writing, as MPXJ opened it when it
        // was handed the file name, so that a plan MS Project still has open can be imported.
        public static FileStream OpenImportFile(string filename, ProjectScenarioImportFormat format)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);

            return format == ProjectScenarioImportFormat.MicrosoftProject
                ? new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                : File.OpenRead(filename);
        }

        // The saves build the whole file in memory first, so that a failure of the build leaves the file that is there as
        // it was. Then they write it by way of a temporary file (see Write), so that a failure of the write does too.

        public static void Save(string filename, Action<Stream> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(write);

            using var buffer = new MemoryStream();
            write(buffer);

            Write(filename, buffer.WriteTo, s_RetryTime, ReportInPlace);
        }

        public static async Task SaveAsync(string filename, Func<Stream, Task> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(write);

            using var buffer = new MemoryStream();
            await write(buffer);

            // Off the thread of the caller, which is the UI thread for a save of the desktop: the write flushes to the disk,
            // and it waits when another program holds the file.
            await Task.Run(() => Write(filename, buffer.WriteTo, s_RetryTime, ReportInPlace));
        }

        // Writes a file with no copy of it in memory first. This is for the small files of the settings, which the desktop
        // writes at each change.
        public static void Replace(string filename, Action<Stream> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(write);

            Write(filename, write, s_RetryTime, ReportInPlace);
        }

        /// <summary>
        /// Writes a file by way of a temporary file in the same folder, which then takes the place of the file. A write that
        /// fails, or a process that stops part way, leaves the file that was there whole.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The file that was there keeps what it is: a link stays a link (the write goes to the file behind it), a hidden
        /// file stays hidden, and on Windows the file keeps the time of its creation and its permissions. On the other
        /// systems it keeps its mode. A file that nobody may write is a failure, with the file whole.
        /// </para>
        /// <para>
        /// A write in place is what the saves did before, and it works in two cases where nothing can take the place of the
        /// file: another program holds the file open and lets others write it and not delete it, and the folder lets the
        /// user make no file. In these cases the write goes in place, so that a save that worked before still works, and
        /// <paramref name="report"/> hears of it. For a file that a program holds, it tries for
        /// <paramref name="retryTime"/> first: a scanner or a sync client holds a file for a moment.
        /// </para>
        /// </remarks>
        /// <param name="write">Writes the content to a stream. It can be called two times: for the temporary file, and for
        /// the write in place.</param>
        internal static void Write(
            string filename,
            Action<Stream> write,
            TimeSpan retryTime,
            Action<string, Exception>? report)
        {
            string target = ResolveLink(filename);

            // A file that nobody may write: the open in place fails before it changes anything, with the words of the system.
            if (IsReadOnly(target))
            {
                WriteInPlace(target, write);
                return;
            }

            string temporary = $@"{target}{TemporaryExtension}";
            FileStream stream;

            try
            {
                stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException and not DirectoryNotFoundException and not PathTooLongException)
            {
                // No file can be made next to the target. The report comes after the write: a write in place that fails
                // is a failure of the save, and not a write in place.
                WriteInPlace(target, write);
                report?.Invoke(target, ex);
                return;
            }

            try
            {
                using (stream)
                {
                    write(stream);

                    // On the disk before the move, so that a stop of the computer does not leave an empty file.
                    stream.Flush(flushToDisk: true);
                }

                KeepMode(target, temporary);

                if (PutInPlace(temporary, target, retryTime) is Exception failure)
                {
                    WriteInPlace(target, write);
                    report?.Invoke(target, failure);
                }
            }
            finally
            {
                DeleteIfPossible(temporary);
            }
        }

        // The file that a write changes: the file itself or, for a link, the file at the end of the links.
        private static string ResolveLink(string filename)
        {
            try
            {
                var info = new FileInfo(filename);

                return info.LinkTarget is null
                    ? filename
                    : info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? filename;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return filename;
            }
        }

        private static bool IsReadOnly(string filename)
        {
            try
            {
                return File.Exists(filename)
                    && File.GetAttributes(filename).HasFlag(FileAttributes.ReadOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        // A new file has the mode that the system gives to each new file. The file that takes the place of another keeps the
        // mode of that one. Windows keeps the permissions itself (File.Replace).
        private static void KeepMode(string target, string temporary)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                if (File.Exists(target))
                {
                    File.SetUnixFileMode(temporary, File.GetUnixFileMode(target));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The file gets the mode of a new file.
            }
        }

        // Puts the temporary file in the place of the target, and tries again for a time when a program holds the target.
        // It gives null, or the last failure when the target still cannot be replaced.
        private static Exception? PutInPlace(
            string temporary,
            string target,
            TimeSpan retryTime)
        {
            long start = Environment.TickCount64;

            while (true)
            {
                try
                {
                    TakePlace(temporary, target);
                    return null;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (Environment.TickCount64 - start >= retryTime.TotalMilliseconds)
                    {
                        return ex;
                    }

                    Thread.Sleep(s_RetryPause);
                }
            }
        }

        private static void TakePlace(string temporary, string target)
        {
            if (!File.Exists(target))
            {
                File.Move(temporary, target);
                return;
            }

            if (!OperatingSystem.IsWindows())
            {
                // One step of the file system: a reader sees the old file or the new one.
                File.Move(temporary, target, overwrite: true);
                return;
            }

            FileAttributes attributes = File.GetAttributes(target);

            try
            {
                // Keeps the attributes, the permissions and the time of creation of the target, and works on a file that a
                // program holds open when that program lets others delete it.
                File.Replace(temporary, target, destinationBackupFileName: null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                // A file system that cannot do that (not NTFS). The move keeps the content only, so the attributes go back.
                File.Move(temporary, target, overwrite: true);
                File.SetAttributes(target, attributes);
            }
        }

        // What the saves did before: it empties the file and writes into it. A failure during it can leave a part of a file.
        private static void WriteInPlace(string filename, Action<Stream> write)
        {
            using FileStream stream = CreateFile(filename);
            write(stream);
        }

        private static void ReportInPlace(string filename, Exception reason) => WroteInPlace?.Invoke(filename, reason);

        // Removes the temporary file, when a write left it. The failure of the write is what the caller hears of.
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
