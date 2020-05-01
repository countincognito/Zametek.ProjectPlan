using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DependenciesStringValidationRule
        : ValidationRule
    {
        #region Fields

        private static char s_Separator = ',';
        private static readonly Regex s_Whitespace = new Regex(@"\s+", RegexOptions.Compiled);
        private static Regex s_StrippedMatch;

        #endregion

        #region Ctors

        static DependenciesStringValidationRule()
        {
            RefreshStrippedMatch();
        }

        #endregion

        #region Properties

        public ManagedActivityContext Context
        {
            get;
            set;
        }

        public static char Separator
        {
            get
            {
                return s_Separator;
            }
            set
            {
                s_Separator = value;
                RefreshStrippedMatch();
            }
        }

        #endregion

        #region Public Methods

        public static string StripWhitespace(string input)
        {
            return s_Whitespace.Replace(input, string.Empty);
        }

        public static IList<int> Parse(string input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            return input
                .Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        #endregion

        #region Private Methods

        private static void RefreshStrippedMatch()
        {
            s_StrippedMatch = new Regex(@"^[0-9]*(" + s_Separator + @"[0-9]+)*$", RegexOptions.Compiled);
        }

        #endregion

        #region Overrides

        public override ValidationResult Validate(object value, CultureInfo culture)
        {
            string input = value as string;
            if (string.IsNullOrWhiteSpace(input))
            {
                return ValidationResult.ValidResult;
            }
            string stripped = StripWhitespace(input);
            if (!s_StrippedMatch.IsMatch(stripped))
            {
                return new ValidationResult(false, Resource.ProjectPlan.Resources.Label_InvalidFormat);
            }
            ManagedActivityContext context = Context;
            if (context != null && context.Id != 0)
            {
                IList<int> output = Parse(stripped);
                if (output.Contains(context.Id))
                {
                    return new ValidationResult(false, Resource.ProjectPlan.Resources.Label_SelfDependency);
                }
            }
            return ValidationResult.ValidResult;
        }

        #endregion
    }
}
