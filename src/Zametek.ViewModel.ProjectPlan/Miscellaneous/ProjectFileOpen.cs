using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectFileOpen
        : IProjectFileOpen
    {
        public async Task<ProjectPlanModel> OpenProjectPlanFileAsync(string filename)
        {
            using StreamReader reader = File.OpenText(filename);
            string content = await reader.ReadToEndAsync();
            JObject json = JObject.Parse(content);
            string version = json?.GetValue(nameof(ProjectPlanModel.Version), StringComparison.OrdinalIgnoreCase)?.ToString() ?? string.Empty;
            string jsonString = json?.ToString() ?? string.Empty;

            Func<string, ProjectPlanModel> func =
                jString => throw new ArgumentOutOfRangeException(
                    nameof(filename),
                    @$"{Resource.ProjectPlan.Messages.Message_UnableToOpenFile} {filename}");

            version.ValueSwitchOn()
                .Case(Versions.v0_1_0_original, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_1_0.ProjectPlanModel>(jString)
                        ?? new Data.ProjectPlan.v0_1_0.ProjectPlanModel());
                })
                .Case(Versions.v0_1_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_1_0.ProjectPlanModel>(jString)
                        ?? new Data.ProjectPlan.v0_1_0.ProjectPlanModel());
                })
                .Case(Versions.v0_2_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_2_0.ProjectPlanModel>(jString)
                        ?? new Data.ProjectPlan.v0_2_0.ProjectPlanModel());
                })
                .Case(Versions.v0_2_1, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_2_1.ProjectPlanModel>(jString)
                        ?? new Data.ProjectPlan.v0_2_1.ProjectPlanModel());
                })
                .Case(Versions.v0_3_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_3_0.ProjectPlanModel>(jString)
                        ?? new Data.ProjectPlan.v0_3_0.ProjectPlanModel());
                })
                .Case(Versions.v0_3_1, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_3_1.ProjectPlanModel>(jString)
                        ?? new Data.ProjectPlan.v0_3_1.ProjectPlanModel());
                })
                .Case(Versions.v0_3_2, x =>
                 {
                     func = jString => Converter.Upgrade(
                         JsonConvert.DeserializeObject<Data.ProjectPlan.v0_3_2.ProjectPlanModel>(jString)
                         ?? new Data.ProjectPlan.v0_3_2.ProjectPlanModel());
                 })
                .Case(Versions.v0_4_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_0.ProjectPlanModel>(jString)
                        ?? new Data.ProjectPlan.v0_4_0.ProjectPlanModel());
                });

            return await Task.Run(() => func(jsonString));
        }
    }
}
