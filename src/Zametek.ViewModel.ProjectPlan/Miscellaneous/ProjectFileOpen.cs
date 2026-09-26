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
        private readonly IDateTimeCalculator m_DateTimeCalculator;

        public ProjectFileOpen(IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_DateTimeCalculator = dateTimeCalculator;
        }

        public async Task<ProjectModel> OpenProjectFileAsync(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            // The caller owns the stream, so the reader leaves it open.
            using var reader = new StreamReader(stream, leaveOpen: true);
            string content = await reader.ReadToEndAsync();
            JObject json = JObject.Parse(content);
            string version =
                json?.GetValue(nameof(ProjectModel.Version), StringComparison.OrdinalIgnoreCase)?.ToString()
                ?? string.Empty;
            string jsonString = json?.ToString() ?? string.Empty;

            // A stream has no name to report, so the message names the version that was not recognised instead.
            Func<string, ProjectModel> func =
                jString => throw new InvalidDataException(
                    string.Format(Resource.ProjectPlan.Messages.Message_UnknownProjectFileVersion, version));

            DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

            version.ValueSwitchOn()
                .Case(Versions.v0_1_0_original, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_1_0.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_1_0.ProjectModel());
                })
                .Case(Versions.v0_1_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_1_0.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_1_0.ProjectModel());
                })
                .Case(Versions.v0_2_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_2_0.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_2_0.ProjectModel());
                })
                .Case(Versions.v0_2_1, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_2_1.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_2_1.ProjectModel());
                })
                .Case(Versions.v0_3_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_3_0.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_3_0.ProjectModel());
                })
                .Case(Versions.v0_3_1, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_3_1.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_3_1.ProjectModel());
                })
                .Case(Versions.v0_3_2, x =>
                 {
                     func = jString => Converter.Upgrade(
                        localNow,
                         JsonConvert.DeserializeObject<Data.ProjectPlan.v0_3_2.ProjectModel>(jString)
                         ?? new Data.ProjectPlan.v0_3_2.ProjectModel());
                 })
                .Case(Versions.v0_4_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_0.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_4_0.ProjectModel());
                })
                .Case(Versions.v0_4_1, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_1.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_4_1.ProjectModel());
                })
                .Case(Versions.v0_4_2, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_2.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_4_2.ProjectModel());
                })
                .Case(Versions.v0_4_3, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_3.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_4_3.ProjectModel());
                })
                .Case(Versions.v0_4_4, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_4.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_4_4.ProjectModel());
                })
                .Case(Versions.v0_5_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        localNow,
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_5_0.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_5_0.ProjectModel());
                })
                .Case(Versions.v0_6_0, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_6_0.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_6_0.ProjectModel());
                })
                .Case(Versions.v0_6_1, x =>
                {
                    func = jString => Converter.Upgrade(
                        JsonConvert.DeserializeObject<Data.ProjectPlan.v0_6_1.ProjectModel>(jString)
                        ?? new Data.ProjectPlan.v0_6_1.ProjectModel());
                });

            return await Task.Run(() => func(jsonString));
        }
    }
}
