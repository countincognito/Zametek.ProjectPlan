using System.Text.Json;

namespace Zametek.ProjectPlan.CommandLine
{
    // How zpp serve's API names what it names - properties and enum values alike: in camelCase, with the one exception that
    // graphml, which is the name of a file format and so a word, is not written graphML.
    internal sealed class ApiNamingPolicy
        : JsonNamingPolicy
    {
        public static ApiNamingPolicy Instance { get; } = new();

        public override string ConvertName(string name)
        {
            ArgumentNullException.ThrowIfNull(name);

            return string.Equals(name, nameof(GraphExport.GraphML), StringComparison.Ordinal)
                ? @"graphml"
                : CamelCase.ConvertName(name);
        }
    }
}
