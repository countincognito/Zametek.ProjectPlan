namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// A recovery copy of a project: a project file that the application keeps apart from the files of the user, for a run of
    /// the application that ends before the user saves. It says which project the copy is of, and when and why it was written.
    /// </summary>
    [Serializable]
    public record RecoveryCopyModel
    {
        /// <summary>The file of the copy. It is a project file, and each program that reads a project file reads it.</summary>
        public string Filename { get; init; } = string.Empty;

        /// <summary>The file that the project was opened from or saved to, or an empty text for a project that has no file.</summary>
        public string ProjectFile { get; init; } = string.Empty;

        /// <summary>The title of the project, or an empty text for a project that has no title.</summary>
        public string Title { get; init; } = string.Empty;

        public DateTimeOffset WrittenAt { get; init; }

        /// <summary>Why the copy was written. It is one of <see cref="RecoveryCopyReasons"/>.</summary>
        public string Reason { get; init; } = string.Empty;
    }

    /// <summary>
    /// The reasons for a recovery copy, as its file says them. They are never translated.
    /// </summary>
    public static class RecoveryCopyReasons
    {
        /// <summary>The plan had changes when the time between two copies ended.</summary>
        public const string Interval = @"interval";

        /// <summary>The application met an exception that nothing handled, and it ended.</summary>
        public const string Error = @"error";

        /// <summary>The user opened a copy that an earlier run left. This run keeps it as its own.</summary>
        public const string Opened = @"opened";
    }
}
