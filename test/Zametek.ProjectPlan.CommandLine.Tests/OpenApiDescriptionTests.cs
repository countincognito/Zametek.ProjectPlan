using Shouldly;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for what OpenApiContractTests hold the server to the description with: how the description is read - the JSON its YAML
    /// stands for - and how the schemas in it check a document: strictly, with each reference followed.
    /// </summary>
    public class OpenApiDescriptionTests
    {
        private const string c_Description = """
            openapi: 3.1.0
            paths:
              /v1/things:
                get:
                  responses:
                    '200':
                      $ref: '#/components/responses/Things'
            components:
              responses:
                Things:
                  description: The things.
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
              schemas:
                Thing:
                  type: object
                  required: [name]
                  properties:
                    name:
                      type: string
                    size:
                      type: [integer, 'null']
                    made:
                      type: string
                      format: date
                Problem:
                  type: object
                  properties:
                    type:
                      type: string
                ExtendedProblem:
                  allOf:
                    - $ref: '#/components/schemas/Problem'
                    - type: object
                      properties:
                        extra:
                          type: integer
            """;

        private static IReadOnlyList<string> Validate(
            string component,
            string json)
        {
            OpenApiDescription description = OpenApiDescription.FromText(c_Description);
            using JsonDocument document = JsonDocument.Parse(json);

            return description.Validate(JsonNode.Parse($$"""{"$ref":"#/components/schemas/{{component}}"}""")!, document.RootElement);
        }

        [Theory]
        [InlineData("a: 1", """{"a":1}""")]
        [InlineData("a: -1", """{"a":-1}""")]
        [InlineData("a: 3000000000", """{"a":3000000000}""")]
        [InlineData("a: 1.5", """{"a":1.5}""")]
        [InlineData("a: 1e3", """{"a":1000}""")]
        [InlineData("a: true", """{"a":true}""")]
        [InlineData("a: False", """{"a":false}""")]
        [InlineData("a: ~", """{"a":null}""")]
        [InlineData("a: null", """{"a":null}""")]
        [InlineData("a:", """{"a":null}""")]
        [InlineData("a: ''", """{"a":""}""")]
        [InlineData("a: '1'", """{"a":"1"}""")]
        [InlineData("a: \"1\"", """{"a":"1"}""")]
        [InlineData("a: 'true'", """{"a":"true"}""")]
        [InlineData("a: 'null'", """{"a":"null"}""")]
        [InlineData("a: x", """{"a":"x"}""")]
        [InlineData("a: 0.10.1", """{"a":"0.10.1"}""")]
        [InlineData("a: 2024-01-06", """{"a":"2024-01-06"}""")]
        [InlineData("a: 002ff9ee", """{"a":"002ff9ee"}""")]
        [InlineData("a: [1, x, 'y']", """{"a":[1,"x","y"]}""")]
        [InlineData("a: { b: 1, c: [] }", """{"a":{"b":1,"c":[]}}""")]
        [InlineData("a: |\n  one\n  two", """{"a":"one\ntwo"}""")]
        public void Parse_Given_Yaml_Then_TheJsonItStandsFor(string yaml, string expected)
        {
            using var reader = new StringReader(yaml);

            OpenApiDescription.Parse(reader)!.ToJsonString().ShouldBe(expected);
        }

        [Fact]
        public void Parse_Given_AnInteger_Then_AnIntWhereItFitsAndALongWhereItDoesNot()
        {
            using var reader = new StringReader("small: 422\nlarge: 3000000000");

            JsonObject parsed = Parse(reader);

            parsed["small"]!.GetValue<int>().ShouldBe(422);
            parsed["large"]!.GetValue<long>().ShouldBe(3_000_000_000);
        }

        private static JsonObject Parse(TextReader reader)
        {
            return (JsonObject)OpenApiDescription.Parse(reader)!;
        }

        [Fact]
        public void Operations_Given_APathWithAnOperation_Then_ItsMethodInCapitalsAndItsPath()
        {
            OpenApiDescription description = OpenApiDescription.FromText(c_Description);

            (string method, string path, JsonObject _) = description.Operations.ShouldHaveSingleItem();

            method.ShouldBe("GET");
            path.ShouldBe("/v1/things");
        }

        [Fact]
        public void FindResponse_Given_AReferenceToAResponse_Then_TheResponseItPointsTo()
        {
            OpenApiDescription description = OpenApiDescription.FromText(c_Description);

            description.FindResponse("GET", "/v1/things", 200)!["description"]!.GetValue<string>().ShouldBe("The things.");
            description.FindResponse("GET", "/v1/things", 404).ShouldBeNull();
            description.FindResponse("POST", "/v1/things", 200).ShouldBeNull();
            description.FindResponse("GET", "/v1/other", 200).ShouldBeNull();
        }

        [Fact]
        public void Validate_Given_ADocumentThatFitsTheSchema_Then_NoErrors()
        {
            Validate("Thing", """{"name":"x","size":null,"made":"2024-01-06"}""").ShouldBeEmpty();
        }

        [Theory]
        [InlineData("""{"size":1}""")]
        [InlineData("""{"name":1}""")]
        [InlineData("""{"name":"x","size":"1"}""")]
        [InlineData("""{"name":"x","made":"yesterday"}""")]
        public void Validate_Given_ADocumentThatDoesNotFitTheSchema_Then_ItsErrors(string json)
        {
            Validate("Thing", json).ShouldNotBeEmpty();
        }

        [Fact]
        public void Validate_Given_AMemberTheSchemaDoesNotName_Then_AnError()
        {
            IReadOnlyList<string> errors = Validate("Thing", """{"name":"x","other":1}""");

            string.Join(" | ", errors).ShouldContain("other");
        }

        [Fact]
        public void Validate_Given_AMemberOfAnExtendingSchema_Then_NoErrorsAndOneThatNoSchemaNamesIsOne()
        {
            // The schema that is extended cannot refuse the members that extend it, but the schema that extends it can refuse all else.
            Validate("ExtendedProblem", """{"type":"t","extra":1}""").ShouldBeEmpty();
            string.Join(" | ", Validate("ExtendedProblem", """{"type":"t","bogus":1}""")).ShouldContain("bogus");
        }

        [Fact]
        public void Validate_Given_ASchemaThatIsAReference_Then_TheSchemaItPointsTo()
        {
            OpenApiDescription description = OpenApiDescription.FromText(c_Description);
            using JsonDocument document = JsonDocument.Parse("""{"name":1}""");
            JsonNode schema = description.Resolve(description.FindResponse("GET", "/v1/things", 200)!["content"]!["application/json"]!)["schema"]!;

            description.Validate(schema, document.RootElement).ShouldNotBeEmpty();
        }

        [Fact]
        public void Resolve_Given_AReferenceToNothing_Then_Throws()
        {
            OpenApiDescription description = OpenApiDescription.FromText(c_Description);

            Should.Throw<InvalidOperationException>(() => description.Resolve(JsonNode.Parse("""{"$ref":"#/components/responses/Nothing"}""")!));
        }
    }
}
