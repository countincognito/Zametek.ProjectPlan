using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading.Tasks;
using Zametek.Utility;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public static class OpenSave
    {
        public static async Task<Common.Project.v0_2_0.ProjectPlanDto> OpenProjectPlanDtoAsync(string filename)
        {
            if (File.Exists(filename))
            {
                using (StreamReader reader = File.OpenText(filename))
                {
                    string content = await reader.ReadToEndAsync();
                    JObject json = JObject.Parse(content);
                    string version = json.GetValue(nameof(Common.Project.IHaveVersion.Version)).ToString();
                    string jsonString = json.ToString();
                    Common.Project.v0_2_0.ProjectPlanDto projectPlanDto = null;

                    version.ValueSwitchOn()
                        .Case(Common.Project.Versions.v0_1_0_original, x =>
                        {
                            projectPlanDto = Common.Project.v0_2_0.DtoConverter.Upgrade(JsonConvert.DeserializeObject<Common.Project.v0_1_0.ProjectPlanDto>(jsonString));

                        })
                        .Case(Common.Project.Versions.v0_1_0, x =>
                        {
                            projectPlanDto = Common.Project.v0_2_0.DtoConverter.Upgrade(JsonConvert.DeserializeObject<Common.Project.v0_1_0.ProjectPlanDto>(jsonString));
                        })
                        .Case(Common.Project.Versions.v0_2_0, x =>
                        {
                            projectPlanDto = JsonConvert.DeserializeObject<Common.Project.v0_2_0.ProjectPlanDto>(jsonString);
                        })
                        .Default(x => throw new InvalidOperationException($@"Cannot process version ""{x}""."));

                    return projectPlanDto;
                }
            }
            return null;
        }

        public static void SaveProjectPlanDto(Common.Project.v0_2_0.ProjectPlanDto state, string fileName)
        {
            using (StreamWriter writer = File.CreateText(fileName))
            {
                var jsonSerializer = JsonSerializer.Create(
                    new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Ignore,
                    });
                jsonSerializer.Serialize(writer, state, typeof(Common.Project.v0_2_0.ProjectPlanDto));
            }
        }
    }
}
