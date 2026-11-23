using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A program that writes a project file can give a list or a part as null, for example "Dependencies": null. The models of
    /// a file give each list and each part a default, and the reader with the default settings of Newtonsoft.Json replaced the
    /// default with the null. The steps of the upgrade then read the member with no check, for example `[.. project.Nodes]` in
    /// the converter of version 0.6.2 (from the review of src, test and pkg). A null now keeps the default: the file opens as
    /// a file that leaves the member out.
    /// </summary>
    public class ProjectFileOpenNullTests
    {
        [Fact]
        public async Task OpenProject_Given_ANullForEachListAndPartOfTheNewestVersion_Then_TheSameAsAFileWithoutTheMember()
        {
            var calculator = new DateTimeCalculator(TimeProvider.System);
            var opener = new ProjectFileOpen(calculator);
            var saver = new ProjectFileSave();

            // A plan with each kind of list and part, saved in the newest version.
            ProjectModel sample = await CoreViewModelFixture.LoadProjectAsync(@"sample_v0_6_1.zpp");
            JObject plan = ParseKeepingTexts(await SaveAsync(saver, sample));
            plan[nameof(ProjectModel.Version)]!.ToString().ShouldBe(Data.ProjectPlan.Versions.ProjectLatest);

            List<string> paths = [.. MembersThatAreListsOrParts(plan)];
            paths.Count.ShouldBeGreaterThan(30);
            var failures = new List<string>();

            foreach (string path in paths)
            {
                JObject withNull = (JObject)plan.DeepClone();
                ((JProperty)withNull.SelectToken(path)!.Parent!).Value = JValue.CreateNull();

                JObject without = (JObject)plan.DeepClone();
                without.SelectToken(path)!.Parent!.Remove();

                string expected = await SaveAsync(saver, await OpenAsync(opener, without));

                try
                {
                    string actual = await SaveAsync(saver, await OpenAsync(opener, withNull));

                    if (actual != expected)
                    {
                        failures.Add($@"{path}: the file differs from the file without the member");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($@"{path}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            failures.ShouldBeEmpty();
        }

        // The path of each member whose value is a list or a part, at each depth. Only the first item of a list is searched,
        // because the items of a list have the same members.
        private static IEnumerable<string> MembersThatAreListsOrParts(JToken token)
        {
            switch (token)
            {
                case JObject part:
                    foreach (JProperty property in part.Properties())
                    {
                        if (property.Value is JObject or JArray)
                        {
                            yield return property.Value.Path;
                        }

                        foreach (string path in MembersThatAreListsOrParts(property.Value))
                        {
                            yield return path;
                        }
                    }

                    break;
                case JArray list when list.Count > 0:
                    foreach (string path in MembersThatAreListsOrParts(list[0]))
                    {
                        yield return path;
                    }

                    break;
            }
        }

        // Each text stays the text of the file, as ProjectFileOpen reads it: JObject.Parse reads a date as a moment.
        private static JObject ParseKeepingTexts(string content)
        {
            using var reader = new JsonTextReader(new StringReader(content)) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(reader);
        }

        private static async Task<ProjectModel> OpenAsync(ProjectFileOpen opener, JObject plan)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(plan.ToString()));
            return await opener.OpenProjectFileAsync(stream);
        }

        private static async Task<string> SaveAsync(ProjectFileSave saver, ProjectModel project)
        {
            using var stream = new MemoryStream();
            await saver.SaveProjectFileAsync(project, stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
