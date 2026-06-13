namespace Zametek.Data.ProjectPlan.v0_6_1
{
    public static class Converter
    {
        public static ProjectModel Upgrade(
            VersionMapper mapper,
            v0_6_0.ProjectModel project)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(project);

            return new ProjectModel
            {
                Id = project.Id,
                Root = project.Root,
                Current = project.Current,
                Nodes = project.Nodes,
                Files = [.. project.Files.Select(mapper.FromV0_6_0ToV0_6_1)],
                Tags = project.Tags,
                DisplaySettings = project.DisplaySettings,
            };
        }
    }
}
