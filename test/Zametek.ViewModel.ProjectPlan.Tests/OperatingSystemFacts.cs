using System;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // A test for one system. On a different system it returned at its start, and its result was "passed". CI runs on
    // Linux only, and so a test for Windows was green there and never ran (from the review of src, test and pkg). These
    // facts give such a test the result "skipped" on the other system.

    /// <summary>
    /// A fact that runs on Windows only, and is skipped on each other system.
    /// </summary>
    internal sealed class WindowsFactAttribute
        : FactAttribute
    {
        public WindowsFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = @"This test runs on Windows only.";
            }
        }
    }

    /// <summary>
    /// A fact that runs on each system but Windows (Linux and macOS), and is skipped on Windows.
    /// </summary>
    internal sealed class UnixFactAttribute
        : FactAttribute
    {
        public UnixFactAttribute()
        {
            if (OperatingSystem.IsWindows())
            {
                Skip = @"This test does not run on Windows.";
            }
        }
    }
}
