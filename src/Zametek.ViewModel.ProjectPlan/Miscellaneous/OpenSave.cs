using AutoMapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading.Tasks;
using Zametek.Common.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class OpenSave
    {
        public static async Task<ProjectPlanModel> OpenProjectPlanAsync(string filename)
        {
            if (File.Exists(filename))
            {
                using StreamReader reader = File.OpenText(filename);
                string content = await reader.ReadToEndAsync().ConfigureAwait(true);
                JObject json = JObject.Parse(content);
                string version = json.GetValue(nameof(ProjectPlanModel.Version), StringComparison.OrdinalIgnoreCase)?.ToString();
                string jsonString = json.ToString();
                ProjectPlanModel projectPlan = null;

                version.ValueSwitchOn()
                    .Case(Versions.v0_1_0_original, x =>
                    {
                        projectPlan = Data.ProjectPlan.Converter.Upgrade(JsonConvert.DeserializeObject<Data.ProjectPlan.v0_1_0.ProjectPlanModel>(jsonString));
                    })
                    .Case(Versions.v0_1_0, x =>
                    {
                        projectPlan = Data.ProjectPlan.Converter.Upgrade(JsonConvert.DeserializeObject<Data.ProjectPlan.v0_1_0.ProjectPlanModel>(jsonString));
                    })
                    .Case(Versions.v0_2_0, x =>
                    {
                        projectPlan = Data.ProjectPlan.Converter.Upgrade(JsonConvert.DeserializeObject<Data.ProjectPlan.v0_2_0.ProjectPlanModel>(jsonString));
                    })
                    .Case(Versions.v0_2_1, x =>
                    {
                        projectPlan = Data.ProjectPlan.Converter.Upgrade(JsonConvert.DeserializeObject<Data.ProjectPlan.v0_2_1.ProjectPlanModel>(jsonString));
                    })
                    .Default(x => throw new InvalidOperationException($@"Cannot process version ""{x}""."));

                return projectPlan;
            }
            return null;
        }

        public static void SaveProjectPlan(
            ProjectPlanModel state,
            string fileName)
        {
            using StreamWriter writer = File.CreateText(fileName);
            var jsonSerializer = JsonSerializer.Create(
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                });
            Data.ProjectPlan.v0_2_1.ProjectPlanModel output = Data.ProjectPlan.Converter.Format(state);
            jsonSerializer.Serialize(writer, output, output.GetType());
        }
    }
}
