namespace Zametek.Data.ProjectPlan.v0_6_2
{
    public static class Converter
    {
        // The estimates are new in v0.6.2, and an upgraded activity has none: each follows the
        // one before it, and so an activity is exactly the activity that v0.6.1 stored.
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            v0_6_1.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            return new ProjectModel
            {
                Id = project.Id,
                Root = project.Root,
                Current = project.Current,
                Nodes = [.. project.Nodes],
                Files = [.. project.Files.Select(mapper.FromV0_6_1ToV0_6_2)],
                Tags = [.. project.Tags],
                // A flat record of scalars, so sharing it shares no collection.
                DisplaySettings = project.DisplaySettings,
            };
        }
    }
}
