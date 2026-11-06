namespace Zametek.ProjectPlan.CommandLine
{
    // How zpp serve writes its log on stderr.
    public enum LogFormat
    {
        // A line for people to read: the time, the level, the id of the request it belongs to if it belongs to one, and what
        // it says.
        Text,

        // An object a line, for a program to read: see LogJsonFormatter.
        Json,
    }
}
