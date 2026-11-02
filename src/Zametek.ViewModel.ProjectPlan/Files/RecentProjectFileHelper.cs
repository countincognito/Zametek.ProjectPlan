namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Pure helper logic for maintaining the recently opened project file list:
    /// normalisation, deduplication and trimming. The list is ordered most recent
    /// first and never contains two entries that refer to the same file.
    /// </summary>
    /// <remarks>
    /// Paths are compared case-insensitively on Windows and macOS (whose default
    /// file systems are case-insensitive) and case-sensitively on Linux, matching
    /// the platform conventions of the .NET runtime itself. This is a per-platform
    /// approximation rather than a per-volume check (a case-sensitive volume can
    /// be mounted on macOS and vice versa), which is the same trade-off VS Code
    /// makes for its own recently opened list.
    /// </remarks>
    public static class RecentProjectFileHelper
    {
        public static StringComparer PathComparer { get; } =
            OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// Converts a filename to its absolute, canonical form (resolving relative
        /// segments and normalising directory separators) so that different spellings
        /// of the same location deduplicate reliably.
        /// </summary>
        public static string NormalizePath(string filename)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            return Path.GetFullPath(filename.Trim());
        }

        /// <summary>
        /// Returns a new list with the given file at the front, any older entries for
        /// the same file removed, and the length capped at the given maximum.
        /// </summary>
        public static List<string> Record(
            IEnumerable<string>? recentPaths,
            string filename,
            int maximum)
        {
            return Record(recentPaths, filename, maximum, PathComparer);
        }

        public static List<string> Record(
            IEnumerable<string>? recentPaths,
            string filename,
            int maximum,
            StringComparer comparer)
        {
            ArgumentNullException.ThrowIfNull(comparer);
            string normalized = NormalizePath(filename);

            // The new entry goes first; Distinct keeps the first occurrence, so an
            // older duplicate (even one differing only by case on a case-insensitive
            // platform) drops out and the newest spelling wins.
            List<string> candidatePaths = [normalized];

            if (recentPaths is not null)
            {
                candidatePaths.AddRange(recentPaths.Where(path => !string.IsNullOrWhiteSpace(path)));
            }

            return [.. candidatePaths.Distinct(comparer).Take(Math.Max(0, maximum))];
        }

        /// <summary>
        /// Returns a new list with every entry for the given file removed.
        /// </summary>
        public static List<string> Remove(
            IEnumerable<string>? recentPaths,
            string filename)
        {
            return Remove(recentPaths, filename, PathComparer);
        }

        public static List<string> Remove(
            IEnumerable<string>? recentPaths,
            string filename,
            StringComparer comparer)
        {
            ArgumentNullException.ThrowIfNull(comparer);
            string normalized = NormalizePath(filename);

            if (recentPaths is null)
            {
                return [];
            }

            return [.. recentPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Where(path => !comparer.Equals(path, normalized))];
        }
    }
}
