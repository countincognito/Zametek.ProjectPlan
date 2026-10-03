using Shouldly;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what zpp serve does about the file of a socket before it
    /// listens there, with real sockets and real files: a socket that a server
    /// which was killed left behind is removed, so that the server can be
    /// started again; a socket a server listens on, one that this user cannot
    /// tell about, a file with anything in it, and a folder are not; and the
    /// folder the socket is to be in must be there.
    /// </summary>
    public class UnixSocketFileHelperTests
        : IDisposable
    {
        private readonly string m_Folder;

        public UnixSocketFileHelperTests()
        {
            // Short enough for every platform's limit on the path of a socket.
            m_Folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-sock-{Guid.NewGuid():N}"[..17])).FullName;
        }

        public void Dispose()
        {
            Directory.Delete(m_Folder, recursive: true);
            GC.SuppressFinalize(this);
        }

        private string SocketPath()
        {
            return Path.Combine(m_Folder, @"a.sock");
        }

        // Makes it so that this user cannot connect to the socket.
        private static void DenyConnecting(string socket)
        {
            if (OperatingSystem.IsWindows())
            {
                var file = new FileInfo(socket);
                FileSecurity security = file.GetAccessControl();
                using WindowsIdentity user = WindowsIdentity.GetCurrent();
                security.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.ReadData | FileSystemRights.WriteData, AccessControlType.Deny));
                file.SetAccessControl(security);
            }
            else
            {
                File.SetUnixFileMode(socket, UnixFileMode.None);
            }
        }

        [Fact]
        public async Task PrepareAsync_Given_NothingAtThePath_Then_NothingToRemove()
        {
            string socket = SocketPath();

            (await UnixSocketFileHelper.PrepareAsync(socket)).ShouldBeFalse();

            File.Exists(socket).ShouldBeFalse();
        }

        [Fact]
        public async Task PrepareAsync_Given_AFolderThatIsNotThere_Then_UsageException()
        {
            string folder = Path.Combine(m_Folder, @"nowhere");
            string socket = Path.Combine(folder, @"a.sock");

            UsageException exception = await Should.ThrowAsync<UsageException>(() => UnixSocketFileHelper.PrepareAsync(socket));

            exception.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketFolderNotThere, folder, socket));

            // It is not made for the socket.
            Directory.Exists(folder).ShouldBeFalse();
        }

        [Fact]
        public async Task PrepareAsync_Given_ASocketLeftBehind_Then_RemovesIt()
        {
            string socket = SocketPath();
            SocketFiles.LeaveStale(socket);
            File.Exists(socket).ShouldBeTrue();

            (await UnixSocketFileHelper.PrepareAsync(socket)).ShouldBeTrue();

            File.Exists(socket).ShouldBeFalse();
        }

        [Fact]
        public async Task PrepareAsync_Given_ASocketAServerListensOn_Then_IOExceptionAndTheSocketIsLeftAlone()
        {
            string socket = SocketPath();
            using Socket server = SocketFiles.ListenOn(socket);

            IOException exception = await Should.ThrowAsync<IOException>(() => UnixSocketFileHelper.PrepareAsync(socket));

            exception.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketInUse, socket));

            // It is still there, and still takes connections.
            File.Exists(socket).ShouldBeTrue();
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await client.ConnectAsync(new UnixDomainSocketEndPoint(socket));
        }

        [Fact]
        public async Task PrepareAsync_Given_AServerThatDoesNotAnswerInTime_Then_TakenForListeningAndLeftAlone()
        {
            // With no time to answer in, the system's answer that nothing is listening is not heard: it is the same as a
            // server with all its connections taken, which does not answer.
            string socket = SocketPath();
            SocketFiles.LeaveStale(socket);

            IOException exception = await Should.ThrowAsync<IOException>(() => UnixSocketFileHelper.PrepareAsync(socket, TimeSpan.Zero, CancellationToken.None));

            exception.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketInUse, socket));
            File.Exists(socket).ShouldBeTrue();
        }

        [Fact]
        public async Task PrepareAsync_Given_ASocketThisUserCannotConnectTo_Then_IOExceptionAndTheSocketIsLeftAlone()
        {
            // The system's answer is not that nothing is listening, so it is not a socket to remove. Where the user is
            // root the system lets it connect to any.
            if (!OperatingSystem.IsWindows()
                && Environment.IsPrivilegedProcess)
            {
                return;
            }

            string socket = SocketPath();
            using Socket server = SocketFiles.ListenOn(socket);
            DenyConnecting(socket);

            IOException exception = await Should.ThrowAsync<IOException>(() => UnixSocketFileHelper.PrepareAsync(socket));

            // Followed by the system's own words for it.
            exception.Message.ShouldStartWith(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketCannotTell, socket, string.Empty));
            exception.InnerException.ShouldBeOfType<SocketException>();
            File.Exists(socket).ShouldBeTrue();
        }

        [Fact]
        public async Task PrepareAsync_Given_AFolder_Then_UsageExceptionAndTheFolderIsLeftAlone()
        {
            string socket = SocketPath();
            Directory.CreateDirectory(socket);

            UsageException exception = await Should.ThrowAsync<UsageException>(() => UnixSocketFileHelper.PrepareAsync(socket));

            exception.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketIsAFolder, socket));
            Directory.Exists(socket).ShouldBeTrue();
        }

        [Fact]
        public async Task PrepareAsync_Given_AFileWithContent_Then_UsageExceptionAndTheFileIsLeftAlone()
        {
            string socket = SocketPath();
            File.WriteAllText(socket, @"precious");

            UsageException exception = await Should.ThrowAsync<UsageException>(() => UnixSocketFileHelper.PrepareAsync(socket));

            exception.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketIsAFile, socket));
            File.ReadAllText(socket).ShouldBe(@"precious");
        }

        [Fact]
        public async Task PrepareAsync_Given_AnEmptyFile_Then_RemovedWhereTheSystemCannotTellItFromASocket()
        {
            string socket = SocketPath();
            File.WriteAllBytes(socket, []);

            if (OperatingSystem.IsWindows())
            {
                // A socket is a reparse point there, which an ordinary file is not.
                UsageException exception = await Should.ThrowAsync<UsageException>(() => UnixSocketFileHelper.PrepareAsync(socket));

                exception.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketIsAFile, socket));
                File.Exists(socket).ShouldBeTrue();
            }
            else
            {
                (await UnixSocketFileHelper.PrepareAsync(socket)).ShouldBeTrue();
                File.Exists(socket).ShouldBeFalse();
            }
        }

        [Fact]
        public async Task PrepareAsync_Given_ACancelledToken_Then_OperationCanceledException()
        {
            // The server being stopped is not a server answering: nothing is taken for listening, and nothing is removed.
            string socket = SocketPath();
            SocketFiles.LeaveStale(socket);
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => UnixSocketFileHelper.PrepareAsync(socket, cancelled.Token));

            File.Exists(socket).ShouldBeTrue();
        }
    }
}
