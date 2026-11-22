using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The recovery copies as files in one folder. A session has three files, each with the id of the session as its name:
    /// the copy (a project file), a small file that says what the copy is of, and a lock file that the session holds open
    /// for as long as it lives. The system closes the lock file when the process ends, however it ends. Thus, a copy whose
    /// lock file nobody holds is a copy that a run left.
    /// </summary>
    public sealed class RecoveryStore
        : IRecoveryStore
    {
        private const string c_InfoExtension = @".json";
        private const string c_LockExtension = @".lock";

        private readonly string m_Folder;
        private readonly string m_CopyExtension;
        private readonly string m_SessionId;
        private readonly Lock m_Lock = new();
        private FileStream? m_SessionLock;
        private RecoveryCopyModel? m_Copy;

        public RecoveryStore(string folder)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            m_Folder = folder;
            m_CopyExtension = $@".{Resource.ProjectPlan.Filters.Filter_ProjectFileExtension}";
            m_SessionId = Guid.NewGuid().ToString(@"N");
        }

        #region IRecoveryStore Members

        public void BeginSession()
        {
            lock (m_Lock)
            {
                if (m_SessionLock is not null)
                {
                    return;
                }

                Directory.CreateDirectory(m_Folder);
                m_SessionLock = new FileStream(
                    PathOf(m_SessionId, c_LockExtension),
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose);
            }
        }

        public RecoveryCopyModel? Copy
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Copy;
                }
            }
        }

        public async Task<RecoveryCopyModel> WriteCopyAsync(
            RecoveryCopyModel copy,
            Func<Stream, Task> write)
        {
            ArgumentNullException.ThrowIfNull(copy);
            ArgumentNullException.ThrowIfNull(write);

            // The whole file first, so that a project that cannot be written leaves the copy before it.
            using var buffer = new MemoryStream();
            await write(buffer);

            lock (m_Lock)
            {
                Directory.CreateDirectory(m_Folder);
                RecoveryCopyModel written = copy with { Filename = PathOf(m_SessionId, m_CopyExtension) };

                // The copy first, and then what it is of. A run that ends between the two leaves a copy that says less,
                // and not a file that says something of a copy that is not there.
                FileStreamHelper.Replace(written.Filename, buffer.WriteTo);
                FileStreamHelper.Replace(PathOf(m_SessionId, c_InfoExtension), stream => WriteInfo(stream, written));
                m_Copy = written;
                return written;
            }
        }

        public void DeleteCopy()
        {
            lock (m_Lock)
            {
                if (m_Copy is null)
                {
                    return;
                }

                // The file that says what the copy is of goes last: a copy without it is still a copy.
                File.Delete(PathOf(m_SessionId, m_CopyExtension));
                File.Delete(PathOf(m_SessionId, c_InfoExtension));
                m_Copy = null;
            }
        }

        public IReadOnlyList<RecoveryCopyModel> FindLeftCopies()
        {
            if (!Directory.Exists(m_Folder))
            {
                return [];
            }

            RemoveLeftTemporaryFiles();
            List<RecoveryCopyModel> copies = [];

            foreach (string filename in Directory.EnumerateFiles(m_Folder, $@"*{m_CopyExtension}"))
            {
                string id = Path.GetFileNameWithoutExtension(filename);

                if (string.Equals(id, m_SessionId, StringComparison.OrdinalIgnoreCase)
                    || IsAlive(id))
                {
                    continue;
                }

                copies.Add(ReadInfo(id, filename));
            }

            return [.. copies.OrderByDescending(x => x.WrittenAt).ThenBy(x => x.Filename, StringComparer.Ordinal)];
        }

        public void Remove(RecoveryCopyModel copy)
        {
            ArgumentNullException.ThrowIfNull(copy);
            string id = Path.GetFileNameWithoutExtension(copy.Filename);

            // Only a file of this folder: the name of the copy is a name that this store gave.
            File.Delete(PathOf(id, m_CopyExtension));
            File.Delete(PathOf(id, c_InfoExtension));
        }

        #endregion

        #region Private Members

        private string PathOf(string id, string extension) => Path.Combine(m_Folder, $@"{id}{extension}");

        // A run that ended during the write of a copy left the temporary file of the write (see FileStreamHelper.Write).
        // That file is a part of a copy, and nobody can use it. The temporary file of a run that is alive is a write that
        // is on its way, and it stays. A file that does not go now goes at a later start.
        private void RemoveLeftTemporaryFiles()
        {
            foreach (string filename in Directory.EnumerateFiles(m_Folder, $@"*{FileStreamHelper.TemporaryExtension}"))
            {
                string name = Path.GetFileName(filename);
                string id = name[..name.IndexOf('.', StringComparison.Ordinal)];

                if (string.Equals(id, m_SessionId, StringComparison.OrdinalIgnoreCase)
                    || IsAlive(id))
                {
                    continue;
                }

                try
                {
                    File.Delete(filename);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The look for copies goes on: a file that stays does no harm.
                }
            }
        }

        // Whether the session with this id holds its lock file. A lock file that nobody holds is what a run left that
        // ended with no chance to remove it, and it goes.
        private bool IsAlive(string id)
        {
            string lockFilename = PathOf(id, c_LockExtension);

            if (!File.Exists(lockFilename))
            {
                return false;
            }

            try
            {
                using var stream = new FileStream(lockFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);
                return false;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static void WriteInfo(Stream stream, RecoveryCopyModel copy)
        {
            var info = new JObject
            {
                [nameof(RecoveryCopyModel.ProjectFile)] = copy.ProjectFile,
                [nameof(RecoveryCopyModel.Title)] = copy.Title,
                [nameof(RecoveryCopyModel.WrittenAt)] = copy.WrittenAt.ToString(@"o", CultureInfo.InvariantCulture),
                [nameof(RecoveryCopyModel.Reason)] = copy.Reason,
            };

            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(info.ToString(Formatting.Indented));
        }

        // What the copy is of. A copy with no file that says so, or with one that cannot be read, is a copy of a project
        // with no title, written when its file was written.
        private RecoveryCopyModel ReadInfo(string id, string filename)
        {
            var copy = new RecoveryCopyModel
            {
                Filename = filename,
                WrittenAt = new DateTimeOffset(File.GetLastWriteTimeUtc(filename), TimeSpan.Zero),
            };

            try
            {
                string infoFilename = PathOf(id, c_InfoExtension);

                if (!File.Exists(infoFilename))
                {
                    return copy;
                }

                using var reader = new JsonTextReader(new StringReader(File.ReadAllText(infoFilename))) { DateParseHandling = DateParseHandling.None };
                JObject info = JObject.Load(reader);

                return copy with
                {
                    ProjectFile = (string?)info[nameof(RecoveryCopyModel.ProjectFile)] ?? string.Empty,
                    Title = (string?)info[nameof(RecoveryCopyModel.Title)] ?? string.Empty,
                    Reason = (string?)info[nameof(RecoveryCopyModel.Reason)] ?? string.Empty,
                    WrittenAt = DateTimeOffset.TryParse((string?)info[nameof(RecoveryCopyModel.WrittenAt)], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset writtenAt)
                        ? writtenAt
                        : copy.WrittenAt,
                };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidCastException or ArgumentException)
            {
                return copy;
            }
        }

        #endregion

        #region IDisposable Members

        // The end of the session: the lock file goes. The copy stays, for the caller to remove when the run ends well.
        public void Dispose()
        {
            lock (m_Lock)
            {
                m_SessionLock?.Dispose();
                m_SessionLock = null;
            }
        }

        #endregion
    }
}
