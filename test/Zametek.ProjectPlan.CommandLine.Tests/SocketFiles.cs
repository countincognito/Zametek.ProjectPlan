using System.Net.Sockets;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // The kinds of socket that zpp serve may find at the path it is to listen on, or make there, made for a test: one
    // that a server which was killed left behind, one that a server is listening on, and one that is bound but does not
    // listen yet.
    internal static class SocketFiles
    {
        // A socket file with no server behind it, as a killed server leaves one. A socket that is closed takes its
        // file with it, so the file is moved out of the way while the socket closes, and moved back. whileBound, if
        // given, is called with the path while the socket is still bound, as a server would have done something to
        // its socket while it was running.
        public static void LeaveStale(
            string path,
            Action<string>? whileBound = null)
        {
            string aside = path + @".aside";

            using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                socket.Bind(new UnixDomainSocketEndPoint(path));
                whileBound?.Invoke(path);
                File.Move(path, aside);
            }

            File.Move(aside, path);
        }

        // A socket that is bound to the path, and does not listen yet: it has its file, and refuses every connection
        // made to it, until it is disposed - which removes its file.
        public static Socket BindTo(string path)
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            try
            {
                socket.Bind(new UnixDomainSocketEndPoint(path));
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        // A socket that something is listening on, which accepts the connections made to it into its backlog, until it
        // is disposed - which removes its file.
        public static Socket ListenOn(string path)
        {
            Socket socket = BindTo(path);

            try
            {
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
