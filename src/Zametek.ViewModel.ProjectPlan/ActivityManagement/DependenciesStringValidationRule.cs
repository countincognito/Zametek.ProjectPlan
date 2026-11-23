using System.Globalization;
using System.Text.RegularExpressions;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DependenciesStringValidationRule
    {
        #region Fields

        // The separator of the ids in a text. It was a static property that anything could change, and nothing did.
        public const char Separator = ',';

        private readonly static Regex s_Whitespace = new(@"\s+", RegexOptions.Compiled);
        private readonly static Regex s_StrippedMatch = new($@"^[0-9]*({Separator}[0-9]+)*$", RegexOptions.Compiled);

        #endregion

        #region Public Methods

        public static string StripWhitespace(string input)
        {
            return s_Whitespace.Replace(input, string.Empty);
        }

        // The ids of a text that the pattern accepts, or null when an id is too large for an int. The pattern accepts a
        // number of any length, and int.Parse threw OverflowException for a number of more than 10 digits, in the setter of
        // DependenciesString.
        public static IList<int>? Parse(string input)
        {
            var ids = new List<int>();

            foreach (string part in input.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
                {
                    return null;
                }

                ids.Add(id);
            }

            return [.. ids.Distinct().Order()];
        }

        public static (IEnumerable<int>? output, string? errorMessage) Validate(string? value, int id)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ([], null);
            }
            string stripped = StripWhitespace(value);
            if (!s_StrippedMatch.IsMatch(stripped))
            {
                return (null, Resource.ProjectPlan.Labels.Label_InvalidFormat);
            }
            if (id != 0)
            {
                IList<int>? output = Parse(stripped);
                if (output is null)
                {
                    return (null, Resource.ProjectPlan.Labels.Label_InvalidFormat);
                }
                if (output.Contains(id))
                {
                    return (null, Resource.ProjectPlan.Labels.Label_SelfDependency);
                }
                return (output, null);
            }
            return ([], null);
        }

        #endregion
    }
}
