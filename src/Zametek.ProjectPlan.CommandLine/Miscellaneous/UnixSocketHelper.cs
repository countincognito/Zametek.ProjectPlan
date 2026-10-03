using System.Net.Sockets;

namespace Zametek.ProjectPlan.CommandLine
{
    // Reads the address of a Unix domain socket - unix: and the path of the socket - which zpp serve is given to listen
    // on and zpp is given to run on: both read it the same way, whichever system they are on.
    internal static class UnixSocketHelper
    {
        // What the address of a socket starts with, before its path.
        public const string Scheme = @"unix:";

        // Docker writes the address of a socket as unix:// and its path, which for a path that starts with / is unix:///.
        private const string c_Authority = @"//";

        private const char c_Slash = '/';
        private const char c_DriveSeparator = ':';

        // Whether the address is the address of a socket, rather than of anything on the network.
        public static bool IsSocketAddress(string address)
        {
            ArgumentNullException.ThrowIfNull(address);

            return address.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);
        }

        // The path of the socket the address gives, in full - a relative path is taken from the current directory, as zpp
        // takes every other path it is given, and as Kestrel listens only on a socket whose path is absolute - or null
        // when the address gives no path. A path that is too long for a socket is a usage error.
        public static string? GetPath(string address)
        {
            string? path = ReadPath(address, OperatingSystem.IsWindows());

            if (path is null)
            {
                return null;
            }

            string fullPath = Path.GetFullPath(path);

            try
            {
                // The system's own limit on the length of a socket's path is checked when the end point is made.
                _ = new UnixDomainSocketEndPoint(fullPath);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_UnixSocketPathTooLong, fullPath));
            }

            return fullPath;
        }

        // The path as the address writes it, which is not made absolute yet: what comes after unix:, or after unix://,
        // and null when that is nothing. On Windows a path with a drive may be written as a file URI writes one, as
        // unix:///C:/zpp.sock, which is C:/zpp.sock; elsewhere the slash before C: is part of the path.
        internal static string? ReadPath(
            string address,
            bool isWindows)
        {
            if (!IsSocketAddress(address))
            {
                throw new ArgumentException($@"'{address}' is not the address of a socket", nameof(address));
            }

            string path = address[Scheme.Length..];

            if (path.StartsWith(c_Authority, StringComparison.Ordinal))
            {
                path = path[c_Authority.Length..];
            }

            if (isWindows
                && path.Length >= 3
                && path[0] == c_Slash
                && char.IsAsciiLetter(path[1])
                && path[2] == c_DriveSeparator)
            {
                path = path[1..];
            }

            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }
}
