using Newtonsoft.Json;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Data.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectFileSave
        : IProjectFileSave
    {
        public async Task SaveProjectFileAsync(ProjectModel project, string filename)
        {
            using StreamWriter writer = File.CreateText(filename);
            var jsonSerializer = JsonSerializer.Create(
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                });
            Data.ProjectPlan.v0_6_0.ProjectModel output = Converter.Format(project);
            await Task.Run(() => jsonSerializer.Serialize(writer, output, output.GetType()));
        }
    }
}
