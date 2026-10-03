using System.Net.Sockets;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // The two kinds of socket that zpp serve may find at the path it is to listen on, made for a test: one that a server
    // which was killed left behind, and one that a server is listening on.
    internal static class SocketFiles
    {
        // A socket file with no server behind it, as a killed server leaves one. A socket that is closed takes its
        // file with it, so the file is moved out of the way while the socket closes, and moved back.
        public static void LeaveStale(string path)
        {
            string aside = path + @".aside";

            using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                socket.Bind(new UnixDomainSocketEndPoint(path));
                File.Move(path, aside);
            }

            File.Move(aside, path);
        }

        // A socket that something is listening on, which accepts the connections made to it into its backlog, until it
        // is disposed - which removes its file.
        public static Socket ListenOn(string path)
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            try
            {
                socket.Bind(new UnixDomainSocketEndPoint(path));
                socket.Listen(10);
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}
