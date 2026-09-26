using Newtonsoft.Json;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Data.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectFileSave
        : IProjectFileSave
    {
        public async Task SaveProjectFileAsync(ProjectModel project, Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            // The caller owns the stream, so the writer leaves it open.
            using var writer = new StreamWriter(stream, leaveOpen: true);

            // The indented JSON ends its lines with the writer's line end, which is the platform's unless set.
            writer.NewLine = NewLineHelper.NewLine;

            var jsonSerializer = JsonSerializer.Create(
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                });
            Data.ProjectPlan.v0_6_1.ProjectModel output = Converter.Format(project);
            await Task.Run(() => jsonSerializer.Serialize(writer, output, output.GetType()));
        }
    }
}
