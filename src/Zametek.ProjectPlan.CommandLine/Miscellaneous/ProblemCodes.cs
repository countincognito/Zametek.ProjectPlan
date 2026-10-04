namespace Zametek.ProjectPlan.CommandLine
{
    // The codes of what is wrong with a request, in a problem's errors: each names the rule a request breaks, for programs,
    // and is never translated. They are documented in docs/API.md. A compilation error has the code the compiler gave it.
    internal static class ProblemCodes
    {
        // Something that has to be there is not.
        public const string Required = @"required";

        // A member the endpoint does not know.
        public const string UnknownProperty = @"unknownProperty";

        // A value of another type than the member takes.
        public const string WrongType = @"wrongType";

        // A value the member does not take, or a member that is not allowed here.
        public const string NotAllowed = @"notAllowed";

        // A value that is not written as the member's format is.
        public const string InvalidFormat = @"invalidFormat";

        // A number outside the limits.
        public const string OutOfRange = @"outOfRange";

        // An option that is only valid with a project sent as a project.
        public const string NotAllowedWithImport = @"notAllowedWithImport";

        // A part that is larger than the limit.
        public const string TooLarge = @"tooLarge";

        // A file of a format the server does not read.
        public const string UnsupportedFormat = @"unsupportedFormat";

        // A file that is not one the server can read.
        public const string Unreadable = @"unreadable";

        // A scenario that is not there.
        public const string NotFound = @"notFound";

        // A scenario whose name or id prefix is several scenarios'.
        public const string Ambiguous = @"ambiguous";

        // A scenario that holds no data.
        public const string HasNoData = @"hasNoData";

        // An output that could not be produced.
        public const string Failed = @"failed";
    }
}
