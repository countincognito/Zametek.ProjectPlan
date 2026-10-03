using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for how zpp serve and zpp read the address of a Unix domain socket
    /// - unix: and the path of the socket, or unix:// and the path, as Docker
    /// writes one - on Windows as elsewhere: the path they take from it, in
    /// full, and that a path too long for a socket is a usage error.
    /// </summary>
    public class UnixSocketHelperTests
    {
        // A relative path that goes up to the root of wherever the tests run - however deep that is, as more .. than
        // there are directories stay at the root - so that its path in full is short enough for a socket's.
        internal const string UpToTheRoot = @"../../../../../../../../../../../../../../../../../../../../";

        private static string SocketPath()
        {
            return Path.Combine(Path.GetTempPath(), @"zpp.sock");
        }

        [Theory]
        [InlineData(@"unix:/tmp/zpp.sock", true)]
        [InlineData(@"unix:C:\zpp\zpp.sock", true)]
        [InlineData(@"UNIX:zpp.sock", true)]
        [InlineData(@"Unix:", true)]
        [InlineData(@"unix", false)]
        [InlineData(@"http://unix:/tmp/zpp.sock", false)]
        [InlineData(@"http://localhost:9770", false)]
        [InlineData(@"/tmp/zpp.sock", false)]
        [InlineData(@"", false)]
        public void IsSocketAddress_Given_AnAddress_Then_WhetherItStartsWithUnix(
            string address,
            bool isSocket)
        {
            UnixSocketHelper.IsSocketAddress(address).ShouldBe(isSocket);
        }

        [Theory]
        [InlineData(@"unix:/tmp/zpp.sock", false, @"/tmp/zpp.sock")]
        [InlineData(@"unix:///tmp/zpp.sock", false, @"/tmp/zpp.sock")]
        [InlineData(@"UNIX:zpp.sock", false, @"zpp.sock")]
        [InlineData(@"unix://zpp.sock", false, @"zpp.sock")]
        [InlineData(@"unix:C:\Users\me\zpp.sock", true, @"C:\Users\me\zpp.sock")]
        [InlineData(@"unix:C:/Users/me/zpp.sock", true, @"C:/Users/me/zpp.sock")]
        [InlineData(@"unix://C:\Users\me\zpp.sock", true, @"C:\Users\me\zpp.sock")]
        [InlineData(@"unix:\Users\me\zpp.sock", true, @"\Users\me\zpp.sock")]
        // On Windows a path with a drive may be written as a file URI writes one, with a slash before the drive.
        [InlineData(@"unix:///C:/Users/me/zpp.sock", true, @"C:/Users/me/zpp.sock")]
        [InlineData(@"unix:/C:/Users/me/zpp.sock", true, @"C:/Users/me/zpp.sock")]
        // Elsewhere, the slash is the start of the path, and C: a name like any other.
        [InlineData(@"unix:///C:/Users/me/zpp.sock", false, @"/C:/Users/me/zpp.sock")]
        // And on Windows, a slash before anything but a drive is the start of the path.
        [InlineData(@"unix:///c/zpp.sock", true, @"/c/zpp.sock")]
        [InlineData(@"unix:///1:/zpp.sock", true, @"/1:/zpp.sock")]
        public void ReadPath_Given_ASocket_Then_ThePathItWrites(
            string address,
            bool isWindows,
            string path)
        {
            UnixSocketHelper.ReadPath(address, isWindows).ShouldBe(path);
        }

        [Theory]
        [InlineData(@"unix:")]
        [InlineData(@"unix://")]
        [InlineData(@"unix:   ")]
        [InlineData(@"UNIX://  ")]
        public void ReadPath_Given_NoPath_Then_Null(string address)
        {
            UnixSocketHelper.ReadPath(address, isWindows: false).ShouldBeNull();
            UnixSocketHelper.ReadPath(address, isWindows: true).ShouldBeNull();
            UnixSocketHelper.GetPath(address).ShouldBeNull();
        }

        [Theory]
        [InlineData(@"http://localhost:9770")]
        [InlineData(@"/tmp/zpp.sock")]
        [InlineData(@"")]
        public void ReadPath_Given_AnythingButASocket_Then_ArgumentException(string address)
        {
            Should.Throw<ArgumentException>(() => UnixSocketHelper.ReadPath(address, isWindows: false));
        }

        [Fact]
        public void GetPath_Given_AnAbsolutePath_Then_ThePathInFull()
        {
            string socket = SocketPath();

            UnixSocketHelper.GetPath($@"unix:{socket}").ShouldBe(socket);
        }

        [Fact]
        public void GetPath_Given_ADockerStyleAddress_Then_ThePathInFull()
        {
            string socket = SocketPath();

            UnixSocketHelper.GetPath($@"unix://{socket}").ShouldBe(socket);
        }

        [Fact]
        public void GetPath_Given_ARelativePath_Then_ItIsMadeAbsoluteFromTheCurrentDirectory()
        {
            // Kestrel listens only on a socket whose path is absolute. The current directory is where the tests are run,
            // which may be too deep for a socket in it, so the path goes up to its root.
            string root = Path.GetPathRoot(Environment.CurrentDirectory)!;

            UnixSocketHelper.GetPath($@"unix:{UpToTheRoot}zpp.sock").ShouldBe(Path.Combine(root, @"zpp.sock"));
        }

        [Fact]
        public void GetPath_Given_APathTooLongForASocket_Then_UsageException()
        {
            string socket = Path.Combine(Path.GetTempPath(), new string('x', 300) + @".sock");

            Should.Throw<UsageException>(() => UnixSocketHelper.GetPath($@"unix:{socket}"))
                .Message.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_UnixSocketPathTooLong, socket));
        }
    }
}
