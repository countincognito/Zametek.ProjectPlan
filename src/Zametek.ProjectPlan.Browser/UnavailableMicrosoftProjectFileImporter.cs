using System;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.Browser
{
    /// <summary>
    /// Stands in for MS Project import, which cannot run in the browser.
    /// </summary>
    /// <remarks>
    /// The importer is MPXJ.Net - the Java MPXJ library cross-compiled by IKVM. IKVM needs a native
    /// OpenJDK runtime image on disk, resolved through Assembly.Location, and it publishes those
    /// images per RID: Windows, Linux and macOS only. There is no browser-wasm image, and there is
    /// no disk to put one on. The reference still compiles - it is a managed assembly - but its
    /// module initializer throws the moment anything touches it, so this type is registered instead
    /// and reports the limitation plainly rather than surfacing a stack trace from inside IKVM.
    /// <para>
    /// The browser head should keep MS Project import out of the UI entirely; this exists so that any
    /// path which does reach the importer fails with an explanation instead of a puzzle.
    /// </para>
    /// </remarks>
    public class UnavailableMicrosoftProjectFileImporter
        : IMicrosoftProjectFileImporter
    {
        public ProjectScenarioImportModel ImportMicrosoftProjectFile(string filename) =>
            throw new PlatformNotSupportedException(
                @"Importing Microsoft Project files is not available in the browser. The importer depends on a Java runtime image that has no WebAssembly build. Please use the desktop application for this.");
    }
}
