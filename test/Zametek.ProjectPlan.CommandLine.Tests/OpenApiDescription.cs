using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // docs/openapi.yaml, the description of zpp serve's API, read as the tests hold the server to it: the document as JSON, its
    // references - of a response, a header, an example - followed, and each schema in it built to check JSON against. The
    // schemas are strict, which the description itself is not, as an answer may gain members in a later version: a member that
    // an answer has and its schema does not name is found, so that the description is changed with the API.
    internal sealed partial class OpenApiDescription
    {
        private const string c_SchemaBase = @"https://zpp.test/schemas/";
        private const string c_SchemaReference = @"#/components/schemas/";

        // The methods an operation of a path can be for.
        private static readonly string[] s_Methods = [@"get", @"put", @"post", @"delete", @"options", @"head", @"patch", @"trace"];

        // The schemas that others extend, and so cannot refuse the members those add.
        private static readonly string[] s_Bases = [@"Problem"];

        // The results are listed, as they are otherwise only valid or not, and a document that is not valid would have no errors to
        // show for it. The formats - a date, a URI - are checked as they are, which JsonSchema.Net does of its own accord: a test
        // of this class says so.
        private static readonly EvaluationOptions s_Evaluation = new() { OutputFormat = OutputFormat.List };

        private readonly SchemaRegistry m_Registry = new();
        private readonly BuildOptions m_Options;
        private readonly Dictionary<string, JsonSchema> m_Anonymous = [];

        private OpenApiDescription(JsonObject root)
        {
            Root = root;
            m_Options = new BuildOptions { SchemaRegistry = m_Registry };

            foreach ((string name, JsonNode? schema) in Components(@"schemas"))
            {
                JsonNode strict = Rewrite(schema ?? throw new InvalidOperationException(), isComponent: !s_Bases.Contains(name))
                    ?? throw new InvalidOperationException();
                JsonSchema.Build(JsonSerializer.SerializeToElement(strict), m_Options, new Uri(c_SchemaBase + name));
            }
        }

        // The description, read once from where the project copies it to.
        public static OpenApiDescription Instance { get; } = FromFile(Path.Combine(AppContext.BaseDirectory, @"Docs", @"openapi.yaml"));

        public JsonObject Root { get; }

        // Each operation of each path: its method, in capitals, its path, and what the description says of it.
        public IReadOnlyList<(string Method, string Path, JsonObject Operation)> Operations => [.. Root[@"paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject()
                .Where(x => s_Methods.Contains(x.Key))
                .Select(x => (x.Key.ToUpperInvariant(), path.Key, x.Value!.AsObject())))];

        public JsonObject Components(string kind)
        {
            return Root[@"components"]![kind]!.AsObject();
        }

        // What a reference points to - an object, a reference to another object, or not a reference at all.
        public JsonNode Resolve(JsonNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            while (node is JsonObject reference
                && reference[@"$ref"] is JsonValue pointer)
            {
                JsonNode? target = Root;

                foreach (string part in pointer.GetValue<string>().TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    target = target?[part];
                }

                node = target ?? throw new InvalidOperationException($@"Nothing at {pointer}.");
            }

            return node;
        }

        // The response an operation describes for a status, with its references followed - or none, if it describes none.
        public JsonObject? FindResponse(
            string method,
            string path,
            int status)
        {
            JsonNode? response = Root[@"paths"]![path]?[method.ToLowerInvariant()]?[@"responses"]?[status.ToString(CultureInfo.InvariantCulture)];
            return response is null ? null : Resolve(response).AsObject();
        }

        // What is wrong with a JSON document, as a schema says it should be: nothing, when it is as it says. The schema is a node of
        // the description - a reference to a component, or one of its own - and a member that it does not name is wrong.
        public IReadOnlyList<string> Validate(
            JsonNode schema,
            JsonElement instance)
        {
            ArgumentNullException.ThrowIfNull(schema);

            JsonSchema built = Build(schema);
            EvaluationResults results = built.Evaluate(instance, s_Evaluation);

            if (results.IsValid)
            {
                return [];
            }

            string[] errors = [.. Errors(results).Distinct()];
            return errors.Length > 0 ? errors : [@"does not fit the schema"];
        }

        public static OpenApiDescription FromFile(string path)
        {
            using var reader = new StreamReader(path);
            return new OpenApiDescription((JsonObject)(Parse(reader) ?? throw new InvalidOperationException()));
        }

        public static OpenApiDescription FromText(string yaml)
        {
            using var reader = new StringReader(yaml);
            return new OpenApiDescription((JsonObject)(Parse(reader) ?? throw new InvalidOperationException()));
        }

        // The document as the JSON it stands for.
        public static JsonNode? Parse(TextReader reader)
        {
            var yaml = new YamlStream();
            yaml.Load(reader);

            return ToJson(yaml.Documents[0].RootNode);
        }

        // The document as the JSON it stands for: a plain scalar is the null, the boolean or the number it spells, and any other is
        // the text it is, as YAML 1.2's core schema has it.
        private static JsonNode? ToJson(YamlNode node)
        {
            return node switch
            {
                YamlMappingNode mapping => new JsonObject(mapping.Children.Select(x => KeyValuePair.Create(((YamlScalarNode)x.Key).Value ?? string.Empty, ToJson(x.Value)))),
                YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(ToJson).ToArray()),
                YamlScalarNode scalar => ToJsonScalar(scalar),
                _ => throw new InvalidOperationException(),
            };
        }

        private static JsonNode? ToJsonScalar(YamlScalarNode scalar)
        {
            string value = scalar.Value ?? string.Empty;

            if (scalar.Style != ScalarStyle.Plain)
            {
                return JsonValue.Create(value);
            }

            if (value is "" or "~" or "null" or "Null" or "NULL")
            {
                return null;
            }

            if (value is "true" or "True" or "TRUE")
            {
                return JsonValue.Create(true);
            }

            if (value is "false" or "False" or "FALSE")
            {
                return JsonValue.Create(false);
            }

            if (WholeNumber().IsMatch(value)
                && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole))
            {
                // An int where it is one, as a reader of a status or a limit asks for.
                return whole is >= int.MinValue and <= int.MaxValue
                    ? JsonValue.Create((int)whole)
                    : JsonValue.Create(whole);
            }

            if (RealNumber().IsMatch(value)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
            {
                return JsonValue.Create(real);
            }

            return JsonValue.Create(value);
        }

        // A copy of a schema that refers to the schemas of the components as the registry has them, and - for a component that
        // is an object, and no other schema's base - refuses the members that nothing in it names.
        private static JsonNode? Rewrite(
            JsonNode? node,
            bool isComponent = false)
        {
            JsonNode? copy = node switch
            {
                JsonObject obj => new JsonObject(obj.Select(x => KeyValuePair.Create(
                    x.Key,
                    x.Key == @"$ref" && x.Value is JsonValue reference && reference.TryGetValue(out string? target) && target.StartsWith(c_SchemaReference, StringComparison.Ordinal)
                        ? JsonValue.Create(c_SchemaBase + target[c_SchemaReference.Length..])
                        : Rewrite(x.Value)))),
                JsonArray array => new JsonArray([.. array.Select(x => Rewrite(x))]),
                null => null,
                _ => node.DeepClone(),
            };

            if (isComponent
                && copy is JsonObject schema
                && (schema.ContainsKey(@"properties") || schema.ContainsKey(@"allOf") || IsObject(schema[@"type"])))
            {
                schema[@"unevaluatedProperties"] = false;
            }

            return copy;
        }

        private static bool IsObject(JsonNode? type)
        {
            return type switch
            {
                JsonValue value => value.TryGetValue(out string? name) && name == @"object",
                JsonArray array => array.Any(x => x is JsonValue value && value.TryGetValue(out string? name) && name == @"object"),
                _ => false,
            };
        }

        private JsonSchema Build(JsonNode schema)
        {
            JsonNode copy = Rewrite(schema) ?? throw new InvalidOperationException();
            string key = copy.ToJsonString();

            if (!m_Anonymous.TryGetValue(key, out JsonSchema? built))
            {
                built = JsonSchema.Build(JsonSerializer.SerializeToElement(copy), m_Options, new Uri($@"https://zpp.test/anonymous/{m_Anonymous.Count}"));
                m_Anonymous[key] = built;
            }

            return built;
        }

        private static IEnumerable<string> Errors(EvaluationResults results)
        {
            if (results.Errors is not null)
            {
                foreach ((string keyword, string message) in results.Errors)
                {
                    yield return $@"{results.InstanceLocation} {keyword}: {message}";
                }
            }

            foreach (EvaluationResults detail in results.Details ?? [])
            {
                foreach (string error in Errors(detail))
                {
                    yield return error;
                }
            }
        }

        [GeneratedRegex(@"^[-+]?[0-9]+$")]
        private static partial Regex WholeNumber();

        [GeneratedRegex(@"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$")]
        private static partial Regex RealNumber();
    }
}
