namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// The codes of <see cref="ImportIssue"/>: each names a kind of problem, for programs, and is never translated. They are
    /// documented in docs/API.md.
    /// </summary>
    public static class ImportIssueCodes
    {
        /// <summary>The file cannot be read as a workbook.</summary>
        public const string UnreadableWorkbook = @"unreadableWorkbook";

        /// <summary>The workbook has no sheet that the import knows.</summary>
        public const string NoKnownSheet = @"noKnownSheet";

        /// <summary>A sheet has no row of titles.</summary>
        public const string MissingHeader = @"missingHeader";

        /// <summary>A column that the sheet needs is not there.</summary>
        public const string MissingColumn = @"missingColumn";

        /// <summary>A title that the import reads is the title of more than one column.</summary>
        public const string DuplicateColumn = @"duplicateColumn";

        /// <summary>A cell that has to hold a value is blank.</summary>
        public const string MissingValue = @"missingValue";

        /// <summary>The Id is the Id of an earlier row of the sheet.</summary>
        public const string DuplicateId = @"duplicateId";

        /// <summary>A row of a tracker sheet is for an activity that the workbook does not have.</summary>
        public const string UnknownActivity = @"unknownActivity";

        /// <summary>A cell holds an error of Excel (for example #N/A).</summary>
        public const string ExcelError = @"excelError";

        /// <summary>A cell does not hold a whole number.</summary>
        public const string NotAWholeNumber = @"notAWholeNumber";

        /// <summary>A cell does not hold a number.</summary>
        public const string NotANumber = @"notANumber";

        /// <summary>A cell does not hold true or false.</summary>
        public const string NotTrueOrFalse = @"notTrueOrFalse";

        /// <summary>A cell does not hold a date.</summary>
        public const string NotADate = @"notADate";

        /// <summary>A cell holds a word that is not one of the choices of its column.</summary>
        public const string NotAChoice = @"notAChoice";

        /// <summary>A cell does not hold a colour in the form #RRGGBB or #RRGGBBAA.</summary>
        public const string NotAColor = @"notAColor";
    }
}
