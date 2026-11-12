using Json.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Zametek.Data.ProjectPlan.Tests
{
    /// <summary>
    /// The JSON Schema of the project file (docs/zpp.schema.json) is made from the classes that zpp writes the file from, and
    /// so it cannot be different from them. A change to the classes changes the schema, and this test fails until the file
    /// is made again: set the variable ZPP_REGENERATE_SCHEMA to 1 and run the test Regenerate.
    /// </summary>
    public class ProjectFileSchemaTests
    {
        private const string c_Regenerate = @"ZPP_REGENERATE_SCHEMA";

        private const string c_FileName = @"zpp.schema.json";

        // The folder docs of the repository, where the file is written when it is made again.
        private static string DocsFolder
        {
            get
            {
                DirectoryInfo? directory = new(AppContext.BaseDirectory);

                while (directory is not null && !directory.GetFiles(@"Zametek.ProjectPlan.slnx").Any())
                {
                    directory = directory.Parent;
                }

                return Path.Combine(directory?.FullName ?? throw new InvalidOperationException(), @"docs");
            }
        }

        [Fact]
        public void Regenerate()
        {
            if (Environment.GetEnvironmentVariable(c_Regenerate) != @"1")
            {
                return;
            }

            File.WriteAllText(Path.Combine(DocsFolder, c_FileName), ProjectFileSchema.BuildText());
        }

        [Fact]
        public void TheFile_Given_TheClassesOfTheLatestVersion_Then_IsTheSchemaThatTheyGive()
        {
            string file = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, @"Docs", c_FileName));

            file.ShouldBe(ProjectFileSchema.BuildText(), $@"Set {c_Regenerate} to 1 and run the test Regenerate.");
        }

        [Fact]
        public void Build_Given_EachDescriptionAndListOfRequiredMembers_Then_EachIsOfAMemberOrAClassOfTheFile()
        {
            // A key that names nothing is a description that the schema does not have, for example after a rename.
            IReadOnlySet<string> used = ProjectFileSchema.Build().Used;

            ProjectFileSchema.Descriptions.Keys.Where(x => !used.Contains(x)).ShouldBeEmpty();
            ProjectFileSchema.Required.Keys.Where(x => !used.Contains(x)).ShouldBeEmpty();
        }

        // What is wrong with a file, as the schema says: nothing, when it fits the schema.
        private static IReadOnlyList<string> Problems(string json)
        {
            var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
            JsonSchema schema = JsonSchema.Build(JsonDocument.Parse(ProjectFileSchema.BuildText()).RootElement, options, new Uri(ProjectFileSchema.SchemaId));
            EvaluationResults results = schema.Evaluate(JsonDocument.Parse(json).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

            return results.IsValid ? [] : [.. Errors(results).Distinct().DefaultIfEmpty(@"does not fit the schema")];
        }

        private static IEnumerable<string> Errors(EvaluationResults results)
        {
            foreach ((string keyword, string message) in results.Errors ?? new Dictionary<string, string>())
            {
                yield return $@"{results.InstanceLocation} {keyword}: {message}";
            }

            foreach (string error in (results.Details ?? []).SelectMany(Errors))
            {
                yield return error;
            }
        }

        // The file as zpp writes it (ProjectFileSave): indented, and with no member that is null.
        private static string Write(v0_6_2.ProjectModel project)
        {
            return JsonConvert.SerializeObject(project, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
            });
        }

        // A plan that a program can write: one scenario at the top of the tree, with one activity.
        private const string c_Minimal = """
            {
              "Version": "v0.6.2",
              "Root": "00000000-0000-0000-0000-000000000001",
              "Current": "00000000-0000-0000-0000-000000000002",
              "Nodes": [
                { "Id": "00000000-0000-0000-0000-000000000002", "ParentId": "00000000-0000-0000-0000-000000000001", "NodeType": 0, "Name": "Plan" }
              ],
              "Files": [
                {
                  "NodeId": "00000000-0000-0000-0000-000000000002",
                  "Scenario": {
                    "ProjectStart": "2026-11-02T00:00:00+00:00",
                    "DependentActivities": [
                      { "Activity": { "Id": 1, "Name": "Design", "MostLikelyDuration": 5 }, "Dependencies": [] }
                    ]
                  }
                }
              ]
            }
            """;

        [Fact]
        public void Validate_Given_AProjectWithEveryMemberFilled_Then_ItFitsTheSchema()
        {
            // Each member has a value, as zpp writes it: the schema and the classes agree on the name and the kind of each.
            Problems(Write(ModelReflection.Fill<v0_6_2.ProjectModel>())).ShouldBeEmpty();
        }

        [Fact]
        public void Validate_Given_EachPlanOfTheLatestVersionThatTheTestsHave_Then_EachFitsTheSchema()
        {
            string[] files =
            [
                Path.Combine(AppContext.BaseDirectory, @"TestFiles", @"test_v0_6_2.zpp"),
                .. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, @"Assets"), @"*.zpp"),
            ];
            string[] latest = [.. files.Where(x => JObject.Parse(File.ReadAllText(x))[@"Version"]?.Value<string>() == Versions.ProjectLatest)];

            latest.Length.ShouldBeGreaterThanOrEqualTo(5);

            foreach (string file in latest)
            {
                Problems(File.ReadAllText(file)).ShouldBeEmpty(Path.GetFileName(file));
            }
        }

        [Fact]
        public void Validate_Given_AMinimalPlan_Then_ItFitsTheSchema()
        {
            Problems(c_Minimal).ShouldBeEmpty();
        }

        [Fact]
        public void Validate_Given_ThePlanOfThePageOfTheRules_Then_ItFitsTheSchema()
        {
            // docs/PROJECT-FILE.md gives a plan that a program can write: its first block of JSON.
            string page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, @"Docs", @"PROJECT-FILE.md"));
            int start = page.IndexOf("```json\n", StringComparison.Ordinal) + "```json\n".Length;

            Problems(page[start..page.IndexOf("```", start, StringComparison.Ordinal)]).ShouldBeEmpty();
        }

        [Fact]
        public void Validate_Given_AMisspeltMember_Then_TheSchemaRefusesIt()
        {
            // zpp ignores a member that it does not know, and so the activity would keep its duration of 0 without a word.
            IReadOnlyList<string> problems = Problems(c_Minimal.Replace(@"""MostLikelyDuration""", @"""MostlikelyDuration""", StringComparison.Ordinal));

            problems.ShouldContain(x => x.Contains(@"/Files/0/Scenario/DependentActivities/0/Activity", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_Given_AnActivityWithNoId_Then_TheSchemaRefusesIt()
        {
            Problems(c_Minimal.Replace(@"""Id"": 1, ", string.Empty, StringComparison.Ordinal)).ShouldNotBeEmpty();
        }

        [Fact]
        public void Validate_Given_AnOlderVersion_Then_TheSchemaRefusesIt()
        {
            // zpp reads the older versions, but the schema describes only the latest.
            Problems(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, @"TestFiles", @"test_v0_6_1.zpp")))
                .ShouldContain(x => x.Contains(@"const", StringComparison.Ordinal));
        }
    }
}
