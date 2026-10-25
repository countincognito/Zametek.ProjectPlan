using Shouldly;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what zpp serve does about the file of a socket before it
    /// listens there, and as it makes the socket, with real sockets and real
    /// files: a socket that a server which was killed left behind is removed,
    /// so that the server can be started again; a socket a server listens on,
    /// one that this user cannot tell about, a file with anything in it, and a
    /// folder are not; the folder the socket is to be in must be there; and the
    /// socket is left to the user running the server as soon as it is bound,
    /// before it listens.
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
            SocketAccess.DenyConnecting(socket);

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

        [Fact]
        public async Task PrepareAsync_Given_ASocketLeftBehindThatWasLeftToItsOwner_Then_RemovesIt()
        {
            // As the socket of a zpp serve that was killed is: it was its owner's alone while the server ran, and the
            // next server, run by the same user, still removes it.
            string socket = SocketPath();
            SocketFiles.LeaveStale(socket, UnixSocketFileHelper.RestrictToOwner);
            SocketAccess.AssertOwnerOnly(socket);

            (await UnixSocketFileHelper.PrepareAsync(socket)).ShouldBeTrue();

            File.Exists(socket).ShouldBeFalse();
        }

        [Fact]
        public void RestrictToOwner_Given_ASocketOpenToEveryone_Then_TheOwnerAlone()
        {
            string socket = SocketPath();
            using Socket server = SocketFiles.BindTo(socket);
            SocketAccess.OpenToEveryone(socket);

            UnixSocketFileHelper.RestrictToOwner(socket);

            SocketAccess.AssertOwnerOnly(socket);
        }

        [Fact]
        public async Task RestrictToOwner_Given_ASocket_Then_TheOwnerCanStillConnectToIt()
        {
            string socket = SocketPath();
            using Socket server = SocketFiles.BindTo(socket);

            UnixSocketFileHelper.RestrictToOwner(socket);
            server.Listen(10);

            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await client.ConnectAsync(new UnixDomainSocketEndPoint(socket));
            client.Connected.ShouldBeTrue();
        }

        [Fact]
        public void RestrictToOwner_Given_NoSocket_Then_IOExceptionSayingWhy()
        {
            string socket = SocketPath();

            IOException exception = Should.Throw<IOException>(() => UnixSocketFileHelper.RestrictToOwner(socket));

            // It gives the system's own words for it.
            Exception reason = exception.InnerException.ShouldNotBeNull();
            reason.ShouldBeAssignableTo<IOException>();
            exception.Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeUnixSocketCannotRestrict, socket, reason.Message));
        }

        [Fact]
        public async Task CreateBoundListenSocket_Given_AUnixEndPoint_Then_TheOwnersAloneBeforeItListens()
        {
            string socket = SocketPath();
            var endPoint = new UnixDomainSocketEndPoint(socket);

            using Socket bound = UnixSocketFileHelper.CreateBoundListenSocket(endPoint);

            // It is bound, and does not listen until Kestrel makes it: nothing can connect to it yet. It is the owner's
            // alone already, so nobody else can connect to it once it does.
            File.Exists(socket).ShouldBeTrue();
            SocketAccess.AssertOwnerOnly(socket);
            using (var early = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                SocketException refused = await Should.ThrowAsync<SocketException>(async () => await early.ConnectAsync(endPoint));

                refused.SocketErrorCode.ShouldBe(SocketError.ConnectionRefused);
            }

            bound.Listen(10);
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await client.ConnectAsync(endPoint);
            client.Connected.ShouldBeTrue();
        }

        [Fact]
        public void CreateBoundListenSocket_Given_AnAddressOnTheNetwork_Then_BoundAndNotRestricted()
        {
            using Socket bound = UnixSocketFileHelper.CreateBoundListenSocket(
                new IPEndPoint(IPAddress.Loopback, 0),
                _ => throw new InvalidOperationException(@"Only a Unix domain socket is left to its owner."));

            bound.IsBound.ShouldBeTrue();
            bound.LocalEndPoint.ShouldBeOfType<IPEndPoint>().Port.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void CreateBoundListenSocket_Given_ASocketThatCannotBeLeftToItsOwner_Then_ThrowsAndTheSocketIsClosed()
        {
            string socket = SocketPath();
            var failure = new IOException(@"Cannot be done.");

            IOException exception = Should.Throw<IOException>(
                () => UnixSocketFileHelper.CreateBoundListenSocket(new UnixDomainSocketEndPoint(socket), _ => throw failure));

            // Nothing is left bound to the path to take connections from everybody: the socket took its file with it.
            exception.ShouldBeSameAs(failure);
            File.Exists(socket).ShouldBeFalse();
        }
    }
}
