using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Zametek.Data.ProjectPlan.Tests
{
    // The JSON Schema of the project file of the latest version (docs/zpp.schema.json), made from the classes that the file
    // is written from. zpp writes the file with Newtonsoft.Json: the names of the members as the classes have them, an enum
    // as its number, and no member that is null. The schema says the same. It is strict about the names, because zpp ignores
    // a member that it does not know, and so a misspelt member changes nothing without a word. It requires only the members
    // that a plan cannot do without, because zpp gives each other member its default. The rules that a schema cannot say are
    // in docs/PROJECT-FILE.md.
    internal static class ProjectFileSchema
    {
        public const string SchemaId = @"https://github.com/countincognito/Zametek.ProjectPlan/blob/master/docs/zpp.schema.json";

        private const string c_Namespace = @"Zametek.Data.ProjectPlan.";

        // What the file is.
        private const string c_Description =
            @"A project file of Zametek.ProjectPlan (.zpp), version " + Versions.ProjectLatest + @". zpp and the application read "
            + @"the older versions too, and they save each project in this version. docs/PROJECT-FILE.md gives the rules that a "
            + @"schema cannot say.";

        // The members that a plan cannot do without, by the class that has them.
        public static IReadOnlyDictionary<string, string[]> Required { get; } = new Dictionary<string, string[]>
        {
            [@"v0_6_2.ProjectModel"] = [@"Version"],
            [@"v0_6_0.ProjectScenarioNodeModel"] = [@"Id", @"ParentId", @"NodeType"],
            [@"v0_6_2.ProjectScenarioFileModel"] = [@"NodeId", @"Scenario"],
            [@"v0_6_2.DependentActivityModel"] = [@"Activity"],
            [@"v0_6_2.ActivityModel"] = [@"Id"],
            [@"v0_6_1.ResourceModel"] = [@"Id"],
            [@"v0_3_2.WorkStreamModel"] = [@"Id"],
        };

        // What a member means, where its name does not say enough, by the class that has it and its name.
        public static IReadOnlyDictionary<string, string> Descriptions { get; } = new Dictionary<string, string>
        {
            [@"v0_6_2.ProjectModel.Version"] = @"The version of the format. zpp reads the file as this version.",
            [@"v0_6_2.ProjectModel.Id"] = @"The id of the project.",
            [@"v0_6_2.ProjectModel.Root"] = @"The id of the root of the tree of scenarios. The root is not a node. A node at the top of the tree has this id as its ParentId.",
            [@"v0_6_2.ProjectModel.Current"] = @"The id of the scenario that zpp and the application load. When no item of Files has this id, they load the scenario of the last node, if it has an item.",
            [@"v0_6_2.ProjectModel.Nodes"] = @"The tree of scenarios: each folder and each scenario. A plan with no nodes gets one new, empty scenario, and zpp ignores Files.",
            [@"v0_6_2.ProjectModel.Files"] = @"The data of each scenario: one item for each node of the type File. A folder has no item.",
            [@"v0_6_2.ProjectModel.Tags"] = @"The labels of the nodes.",
            [@"v0_6_0.ProjectScenarioNodeModel.ParentId"] = @"The id of the folder that the node is in, or Root.",
            [@"v0_6_0.ProjectScenarioNodeModel.NodeType"] = @"A scenario has an item in Files, and a folder has none.",
            [@"v0_6_0.ProjectScenarioNodeModel.IsTracked"] = @"The scenario chart shows the scenario as a point.",
            [@"v0_6_2.ProjectScenarioFileModel.NodeId"] = @"The id of the node of the scenario.",
            [@"v0_6_2.ProjectScenarioModel.ProjectStart"] = @"The start that the plan asks for. Day 0 of the schedule is the first working day on this date or after it (EffectiveProjectStart).",
            [@"v0_6_2.ProjectScenarioModel.EffectiveProjectStart"] = @"Calculated: the first working day on ProjectStart or after it, which is day 0 of the schedule. A time of an activity is a number of working days from it. zpp calculates it again when it reads the file.",
            [@"v0_6_2.ProjectScenarioModel.Metrics"] = @"The metrics of the last compile. zpp calculates them again when it compiles the scenario. The scenario chart shows them for a tracked scenario that zpp does not compile.",
            [@"v0_6_2.ProjectScenarioModel.ResourceMetrics"] = @"The metrics of each resource from the last compile. zpp calculates them again when it compiles the scenario.",
            [@"v0_6_2.ProjectScenarioModel.SimulationSummary"] = @"The result of the last run of the simulation, if the scenario has one.",
            [@"v0_6_2.DependentActivityModel.Dependencies"] = @"The ids of the activities that must finish before this activity can start.",
            [@"v0_6_2.DependentActivityModel.ResourceDependencies"] = @"Calculated: the dependencies that the scheduling of the resources adds. zpp calculates them again when it compiles.",
            [@"v0_6_2.DependentActivityModel.Successors"] = @"Calculated: the ids of the activities that depend on this activity. zpp calculates them again when it compiles.",
            [@"v0_6_2.ActivityModel.Id"] = @"The id of the activity. Each activity of a scenario has an id of its own.",
            [@"v0_6_2.ActivityModel.Duration"] = @"Calculated: the duration that the compiler schedules, in working days. It is (O + 4 × ML + P + 3) / 6 in whole days, from the estimates. zpp calculates it again when it reads the file. Thus, set MostLikelyDuration, or leave it out and set Duration, which is then the most likely duration.",
            [@"v0_6_2.ActivityModel.MostLikelyDuration"] = @"The most likely duration (ML), in working days. Without it, the most likely duration is Duration. A value below 0 is 0.",
            [@"v0_6_2.ActivityModel.OptimisticDuration"] = @"The optimistic duration (O), in working days. Without it, O is ML. A value above ML is ML.",
            [@"v0_6_2.ActivityModel.PessimisticDuration"] = @"The pessimistic duration (P), in working days. Without it, P is ML. A value below ML is ML.",
            [@"v0_6_2.ActivityModel.EarliestStartTime"] = @"Calculated: the earliest start, in working days from ProjectStart. zpp calculates it again when it compiles.",
            [@"v0_6_2.ActivityModel.LatestFinishTime"] = @"Calculated: the latest finish, in working days from ProjectStart. zpp calculates it again when it compiles.",
            [@"v0_6_2.ActivityModel.FreeSlack"] = @"Calculated: the free slack, in working days. zpp calculates it again when it compiles.",
            [@"v0_6_2.ActivityModel.AllocatedToResources"] = @"Calculated: the ids of the resources that do the activity. zpp calculates them again when it compiles.",
            [@"v0_6_2.ActivityModel.TargetResources"] = @"The ids of the resources that can do the activity.",
            [@"v0_6_2.ActivityModel.MinimumFreeSlack"] = @"The free slack that the activity must keep, in working days.",
            [@"v0_6_2.ActivityModel.MinimumEarliestStartTime"] = @"The earliest day on which the activity can start, in working days from ProjectStart. MinimumEarliestStartDateTime takes its place when the file has both.",
            [@"v0_6_2.ActivityModel.MinimumEarliestStartDateTime"] = @"The earliest day on which the activity can start, as a date. It takes the place of MinimumEarliestStartTime.",
            [@"v0_6_2.ActivityModel.MaximumLatestFinishTime"] = @"The latest day by which the activity must finish, in working days from ProjectStart. MaximumLatestFinishDateTime takes its place when the file has both.",
            [@"v0_6_2.ActivityModel.MaximumLatestFinishDateTime"] = @"The latest day by which the activity must finish, as a date. It takes the place of MaximumLatestFinishTime.",
        };

        public static Type Root => typeof(v0_6_2.ProjectModel);

        // The schema, with each member and class that it found a description or a list of required members for.
        public static (JsonObject Schema, IReadOnlySet<string> Used) Build()
        {
            var used = new HashSet<string>();
            var options = new JsonSerializerOptions(JsonSerializerDefaults.General) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

            var exporterOptions = new JsonSchemaExporterOptions
            {
                TreatNullObliviousAsNonNullable = true,
                TransformSchemaNode = (context, node) => Transform(context, node, used),
            };

            JsonObject generated = JsonSchemaExporter.GetJsonSchemaAsNode(options, Root, exporterOptions).AsObject();

            var schema = new JsonObject
            {
                [@"$schema"] = @"https://json-schema.org/draft/2020-12/schema",
                [@"$id"] = SchemaId,
                [@"title"] = $@"zpp project file {Versions.ProjectLatest}",
                [@"description"] = c_Description,
            };

            foreach ((string name, JsonNode? value) in generated.ToList())
            {
                generated.Remove(name);
                schema[name] = value;
            }

            return (schema, used);
        }

        // The text of the file: indented, with a line feed at the end of each line, as the other files of the repository.
        public static string BuildText()
        {
            string text = Build().Schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true, IndentSize = 2 });
            return text.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        }

        private static JsonNode Transform(
            JsonSchemaExporterContext context,
            JsonNode node,
            HashSet<string> used)
        {
            if (node is not JsonObject schema)
            {
                return node;
            }

            Type type = Nullable.GetUnderlyingType(context.TypeInfo.Type) ?? context.TypeInfo.Type;

            // An enum is written as its number: the numbers that it has, and the name of each.
            if (type.IsEnum)
            {
                var values = new JsonArray();

                foreach (object value in Enum.GetValues(type))
                {
                    values.Add(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                }

                if (type != context.TypeInfo.Type)
                {
                    values.Add(null);
                }

                schema[@"enum"] = values;
                schema[@"description"] = string.Join(
                    @", ",
                    Enum.GetValues(type).Cast<object>().Select(x => $@"{Convert.ToInt64(x, System.Globalization.CultureInfo.InvariantCulture)}: {x}"));
            }

            // A class: no member that it does not have, and the members that a plan cannot do without. A class that is the value
            // of a member is in the schema of the member.
            if (context.TypeInfo.Kind == JsonTypeInfoKind.Object
                && schema.ContainsKey(@"properties"))
            {
                schema[@"additionalProperties"] = false;
                string key = Key(type);

                if (Required.TryGetValue(key, out string[]? required))
                {
                    used.Add(key);
                    schema[@"required"] = new JsonArray([.. required.Select(x => (JsonNode?)x)]);
                }
            }

            // A member: what it means. The version has one value, which the class gives it and which zpp reads the file by.
            if (context.PropertyInfo is JsonPropertyInfo property
                && property.DeclaringType is Type declaringType)
            {
                string key = $@"{Key(declaringType)}.{property.Name}";

                if (declaringType == Root
                    && property.Name == nameof(v0_6_2.ProjectModel.Version))
                {
                    schema[@"type"] = @"string";
                    schema[@"const"] = Versions.ProjectLatest;
                }

                if (Descriptions.TryGetValue(key, out string? description))
                {
                    used.Add(key);
                    schema[@"description"] = schema[@"description"] is JsonValue enumNames
                        ? $@"{description} ({enumNames.GetValue<string>()})"
                        : description;
                }
            }

            return schema;
        }

        // The class as the maps name it: its version and its name, as v0_6_2.ProjectModel.
        private static string Key(Type type)
        {
            string name = type.FullName ?? type.Name;
            return name.StartsWith(c_Namespace, StringComparison.Ordinal) ? name[c_Namespace.Length..] : name;
        }
    }
}
