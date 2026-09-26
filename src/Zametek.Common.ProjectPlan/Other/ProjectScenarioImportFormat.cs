namespace Zametek.Common.ProjectPlan
{
    // The formats a project scenario can be imported from. MS Project's .mpp and .xml files are one format here, because
    // MPXJ tells them apart by their content.
    [Serializable]
    public enum ProjectScenarioImportFormat
    {
        MicrosoftProject,
        Xlsx
    }
}
