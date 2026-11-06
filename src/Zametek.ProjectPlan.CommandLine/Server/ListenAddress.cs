namespace Zametek.ProjectPlan.CommandLine
{
    // An address zpp serve listens on, as --listen gives it: http or https, a host - localhost, an IP address, or * for
    // every address the machine has - and a port.
    public record ListenAddress(
        string Url,
        bool IsHttps,
        string Host,
        int Port);
}
