namespace Zametek.Graphs.Avalonia
{
    // Saves a file through a writer that takes a stream, for the Save-As path, which has the file name the user picked
    // where the exporters take a stream. A focused copy of the application's FileStreamHelper, kept local so that the
    // library stays standalone, and internal so that it never clashes with the application's public one in consumers
    // that import both namespaces. The two copies write a file in the same way: a change of one is a change of the other.
    internal static class FileStreamHelper
    {
        // The time that a save tries to put its temporary file in the place of a file that another program holds open.
        private static readonly TimeSpan s_RetryTime = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan s_RetryPause = TimeSpan.FromMilliseconds(100);

        // Builds the whole file in memory first, so that a failure of the build leaves the file that is there as it was.
        // Then it writes the file by way of a temporary file, so that a failure of the write does too.
        public static async Task SaveAsync(string filename, Func<Stream, Task> write)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(write);

            using var buffer = new MemoryStream();
            await write(buffer);

            // Off the thread of the caller, which is the UI thread: the write flushes to the disk, and it waits when
            // another program holds the file.
            await Task.Run(() => Write(filename, buffer.WriteTo, s_RetryTime));
        }

        // Writes a file by way of a temporary file in the same folder, which then takes the place of the file. A write that
        // fails, or a process that stops part way, leaves the file that was there whole.
        //
        // The file that was there keeps what it is: a link stays a link (the write goes to the file behind it), a hidden
        // file stays hidden, and on Windows the file keeps the time of its creation and its permissions. On the other
        // systems it keeps its mode. A file that nobody may write is a failure, with the file whole.
        //
        // A write in place is what the save did before, and it works in two cases where nothing can take the place of the
        // file: another program holds the file open and lets others write it and not delete it, and the folder lets the
        // user make no file. In these cases the write goes in place, so that a save that worked before still works. For a
        // file that a program holds, it tries for the retry time first.
        internal static void Write(
            string filename,
            Action<Stream> write,
            TimeSpan retryTime)
        {
            string target = ResolveLink(filename);

            // A file that nobody may write: the open in place fails before it changes anything, with the words of the system.
            if (IsReadOnly(target))
            {
                WriteInPlace(target, write);
                return;
            }

            string temporary = $@"{target}.tmp";
            FileStream stream;

            try
            {
                stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException and not DirectoryNotFoundException and not PathTooLongException)
            {
                // No file can be made next to the target.
                WriteInPlace(target, write);
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

                if (!PutInPlace(temporary, target, retryTime))
                {
                    WriteInPlace(target, write);
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
        // It gives false when the target still cannot be replaced.
        private static bool PutInPlace(
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
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (Environment.TickCount64 - start >= retryTime.TotalMilliseconds)
                    {
                        return false;
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

        // What the save did before: it empties the file and writes into it. A failure during it can leave a part of a file.
        private static void WriteInPlace(string filename, Action<Stream> write)
        {
            using var stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Read);
            write(stream);
        }

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
    }
}
