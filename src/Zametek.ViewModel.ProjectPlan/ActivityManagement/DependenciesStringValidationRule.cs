using System.Text.RegularExpressions;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DependenciesStringValidationRule
    {
        #region Fields

        private readonly static Regex s_Whitespace = new(@"\s+", RegexOptions.Compiled);
        private static Regex? s_StrippedMatch;

        #endregion

        #region Ctors

        static DependenciesStringValidationRule()
        {
            RefreshStrippedMatch();
        }

        #endregion

        #region Properties

        private static char s_Separator = ',';
        public static char Separator
        {
            get => s_Separator;
            set
            {
                s_Separator = value;
                RefreshStrippedMatch();
            }
        }

        #endregion

        #region Private Methods

        private static void RefreshStrippedMatch()
        {
            s_StrippedMatch = new Regex(@"^[0-9]*(" + Separator + @"[0-9]+)*$", RegexOptions.Compiled);
        }

        #endregion

        #region Public Methods

        public static string StripWhitespace(string input)
        {
            return s_Whitespace.Replace(input, string.Empty);
        }

        public static IList<int> Parse(string input)//!!)
        {
            return [.. input
                .Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .Distinct()
                .OrderBy(x => x)];
        }

        public static (IEnumerable<int>? output, string? errorMessage) Validate(string? value, int id)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ([], null);
            }
            string stripped = StripWhitespace(value);
            if (!s_StrippedMatch?.IsMatch(stripped) ?? false)
            {
                return (null, Resource.ProjectPlan.Labels.Label_InvalidFormat);
            }
            if (id != 0)
            {
                IList<int> output = Parse(stripped);
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
