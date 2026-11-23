using System.Reflection;

namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// The version of the application, as Directory.Build.props gives it (Version). Before, the resource Label_AppVersion
    /// had it, and a release changed it by hand in about seven places, and each assembly had the version 1.0.0.0 (from the
    /// review of src, test and pkg).
    /// </summary>
    public static class AppVersion
    {
        /// <summary>
        /// The version, for example 0.11.0-beta.3: the informational version of this assembly, with no hash of a commit.
        /// </summary>
        public static string Text { get; } = Read(typeof(AppVersion).Assembly);

        /// <summary>
        /// The numeric part of the version, for example 0.11.0. The MSI and the MSIX packages give it with ".0" after it.
        /// </summary>
        public static string Numeric => Text.Split('-', 2)[0];

        /// <summary>
        /// The informational version of an assembly, with no hash of a commit, or an empty text when it has none.
        /// </summary>
        public static string Read(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return version is null ? string.Empty : version.Split('+', 2)[0];
        }
    }
}
