using Shouldly;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // Who may connect to a socket, arranged and checked for a test. Windows keeps this in the socket's access list, which
    // starts as its folder's; Linux and macOS keep it in the socket's mode.
    internal static class SocketAccess
    {
        // Makes it so that every user who is signed in may connect to the socket, as they may to one in C:\tmp.
        public static void OpenToEveryone(string socket)
        {
            if (OperatingSystem.IsWindows())
            {
                var file = new FileInfo(socket);
                FileSecurity security = file.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                    FileSystemRights.Modify,
                    AccessControlType.Allow));
                file.SetAccessControl(security);
            }
            else
            {
                File.SetUnixFileMode(socket, (UnixFileMode)0b111_111_111);
            }
        }

        // Makes it so that this user cannot connect to the socket.
        public static void DenyConnecting(string socket)
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

        // Asserts that the user running the test is the only user who may connect to the socket: on Windows, that its
        // access list has that user's entry, with all access, and no other - nothing inherited from its folder - and
        // elsewhere, that its mode is 600.
        public static void AssertOwnerOnly(string socket)
        {
            if (OperatingSystem.IsWindows())
            {
                FileSecurity security = new FileInfo(socket).GetAccessControl();
                using WindowsIdentity user = WindowsIdentity.GetCurrent();

                security.AreAccessRulesProtected.ShouldBeTrue();
                FileSystemAccessRule rule = security
                    .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>()
                    .ShouldHaveSingleItem();
                rule.IdentityReference.ShouldBe(user.User);
                rule.AccessControlType.ShouldBe(AccessControlType.Allow);
                rule.FileSystemRights.ShouldBe(FileSystemRights.FullControl);
                rule.IsInherited.ShouldBeFalse();
            }
            else
            {
                File.GetUnixFileMode(socket).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
    }
}
