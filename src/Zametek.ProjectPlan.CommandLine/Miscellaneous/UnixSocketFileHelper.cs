using System.Net.Sockets;

namespace Zametek.ProjectPlan.CommandLine
{
    // Gets the file a Unix domain socket is ready for zpp serve to listen on. A server that is stopped removes its
    // socket, but one that is killed leaves it behind, and the system will not bind a socket to a path that is taken:
    // so nothing could listen there until somebody removed the file - and a server that whatever started it starts
    // again after it is killed would fail to start, and be started again, for ever. A socket that nothing listens on
    // is therefore removed. Nothing else is: not a socket a server listens on, nor one that this user cannot tell
    // about, nor a file with anything in it, nor a folder.
    internal static class UnixSocketFileHelper
    {
        // How long a server is given to answer before it is taken for busy rather than gone: one with all its
        // connections taken does not answer.
        private static readonly TimeSpan s_AnswerLimit = TimeSpan.FromSeconds(2);

        // Makes the path of a socket ready to listen on, and returns whether it removed a socket that nothing listened
        // on. The folder it is to be in must be there. A path that is free is left as it is. A server that listens on it
        // is a failure to start, as a port that is taken is; a folder, or a file that is not what a server leaves
        // behind, is a usage error: the path cannot be used, and zpp serve does not remove what it did not make.
        public static async Task<bool> PrepareAsync(
            string socket,
            CancellationToken cancellationToken = default)
        {
            return await PrepareAsync(socket, s_AnswerLimit, cancellationToken);
        }

        internal static async Task<bool> PrepareAsync(
            string socket,
            TimeSpan answerLimit,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(socket);

            if (Directory.Exists(socket))
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketIsAFolder, socket));
            }

            string folder = Path.GetDirectoryName(socket)
                ?? throw new ArgumentException($@"'{socket}' is not the path of a file", nameof(socket));

            if (!Directory.Exists(folder))
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketFolderNotThere, folder, socket));
            }

            if (!File.Exists(socket))
            {
                return false;
            }

            if (await IsServedAsync(socket, answerLimit, cancellationToken))
            {
                throw new IOException(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketInUse, socket));
            }

            if (!IsLeftBehind(new FileInfo(socket)))
            {
                throw new UsageException(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketIsAFile, socket));
            }

            File.Delete(socket);
            return true;
        }

        // Whether a server is listening on the socket: yes if one answers, or does not answer in time, and no if the
        // system says that nothing is listening there - which it does of a socket left behind, and of a file that is not
        // a socket at all. Anything else the system says, such as that this user may not connect to it, is no answer:
        // it is raised, rather than taken for a socket to remove.
        private static async Task<bool> IsServedAsync(
            string socket,
            TimeSpan answerLimit,
            CancellationToken cancellationToken)
        {
            using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // A limit of nothing has run out already, whatever the system would have said.
            if (answerLimit > TimeSpan.Zero)
            {
                limit.CancelAfter(answerLimit);
            }
            else
            {
                await limit.CancelAsync();
            }

            try
            {
                await probe.ConnectAsync(new UnixDomainSocketEndPoint(socket), limit.Token);
                return true;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                return false;
            }
            catch (SocketException ex)
            {
                throw new IOException(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketCannotTell, socket, ex.Message), ex);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return true;
            }
        }

        // Whether the file is what a server that was killed leaves behind: empty, and, where the system says what kind
        // of file it is, a socket - which on Windows is a reparse point that is no link. Elsewhere there is no telling an
        // empty file from a socket without a native call, so an empty file is taken for one.
        private static bool IsLeftBehind(FileInfo file)
        {
            return file.Length == 0
                && (!OperatingSystem.IsWindows()
                    || (file.Attributes.HasFlag(FileAttributes.ReparsePoint) && file.LinkTarget is null));
        }
    }
}
