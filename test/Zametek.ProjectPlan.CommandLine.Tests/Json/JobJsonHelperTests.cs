using Shouldly;
using System.Text.Json;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for how zpp serve's API is written in JSON: camelCase, enums by name and never by number, durations as
    /// ISO 8601, and names matched as they are written. The server writes every member, with no value as null, and reads
    /// back strictly, so that a member the contract does not name shows; while zpp reads an answer with more in it than it
    /// knows - from a newer server, say - and leaves out of the options it sends what the server defaults to.
    /// </summary>
    public class JobJsonHelperTests
    {
        [Fact]
        public void ServerOptions_Given_AnAnswerWithSomethingTheContractDoesNotName_Then_Refuses()
        {
            Should.Throw<JsonException>(() => JsonSerializer.Deserialize<CompileResponse>(
                @"{""metrics"":{},""outputs"":[],""colour"":""red""}",
                JobJsonHelper.ServerOptions));
        }

        [Fact]
        public void ServerOptions_Given_NamesInAnotherCase_Then_Refuses()
        {
            Should.Throw<JsonException>(() => JsonSerializer.Deserialize<CompileResponse>(
                @"{""Metrics"":{},""outputs"":[]}",
                JobJsonHelper.ServerOptions));
        }

        [Fact]
        public void ServerOptions_Given_AMemberThatHasNoValue_Then_WritesItAsNull()
        {
            string json = JsonSerializer.Serialize(new MetricsResponse(), JobJsonHelper.ServerOptions);

            json.ShouldContain(@"""activityRisk"":null");
            json.ShouldContain(@"""projectFinishDate"":null");
        }

        [Fact]
        public void ServerOptions_Given_EnumsAndDurations_Then_WritesThemAsTheContractDoes()
        {
            JsonSerializer.Serialize(GraphExport.GraphML, JobJsonHelper.ServerOptions).ShouldBe(@"""graphml""");
            JsonSerializer.Serialize(PlotExport.Webp, JobJsonHelper.ServerOptions).ShouldBe(@"""webp""");
            JsonSerializer.Serialize(MetricsExport.Markdown, JobJsonHelper.ServerOptions).ShouldBe(@"""markdown""");
            JsonSerializer.Serialize(TimeSpan.FromSeconds(5), JobJsonHelper.ServerOptions).ShouldBe(@"""PT5S""");
        }

        [Theory]
        [InlineData(@"""nonsense""")]
        [InlineData(@"""""")]
        [InlineData(@"2")]
        public void ServerOptions_Given_AnEnumThatIsNotOneOfTheNamesTheContractGives_Then_Refuses(string json)
        {
            Should.Throw<JsonException>(() => JsonSerializer.Deserialize<GraphExport>(json, JobJsonHelper.ServerOptions));
        }

        [Fact]
        public void ServerOptions_Given_TextWithMarkupInIt_Then_WritesItAsItIs()
        {
            // A response is JSON for its caller, never HTML: what zpp prints, quotes and all, is not escaped further.
            JsonSerializer.Serialize(@"<a href=""x"">&'</a>", JobJsonHelper.ServerOptions).ShouldBe(@"""<a href=\""x\"">&'</a>""");
        }

        [Fact]
        public void ClientOptions_Given_AnAnswerWithMoreInItThanZppKnows_Then_ReadsIt()
        {
            CompileResponse response = JsonSerializer.Deserialize<CompileResponse>(
                @"{""metrics"":{""networkDuration"":5,""somethingNew"":1},""outputs"":[],""console"":{""exitCode"":0,""standardOutput"":"""",""standardError"":"""",""transcript"":[],""elapsed"":42},""more"":true}",
                JobJsonHelper.ClientOptions).ShouldNotBeNull();

            response.Metrics.NetworkDuration.ShouldBe(5);
            response.Console.ShouldNotBeNull().Transcript.ShouldBeEmpty();
        }

        [Fact]
        public void ClientOptions_Given_AProblemWithMoreInItThanZppKnows_Then_ReadsIt()
        {
            ProblemResponse problem = JsonSerializer.Deserialize<ProblemResponse>(
                @"{""type"":""t"",""title"":""x"",""status"":422,""traceId"":""abc"",""instance"":""/somewhere"",""errors"":[{""pointer"":""#/project"",""code"":""required"",""detail"":""is required"",""hint"":""y""}]}",
                JobJsonHelper.ClientOptions).ShouldNotBeNull();

            problem.Status.ShouldBe(422);
            problem.Errors.ShouldNotBeNull().ShouldHaveSingleItem().Pointer.ShouldBe(@"#/project");
        }

        [Fact]
        public void ClientOptions_Given_OptionsWithoutChartsOrGraphs_Then_LeavesThemOutAndWhatTheServerDefaultsTo()
        {
            string json = JsonSerializer.Serialize(
                CompileOptionsHelper.FromOptions(new Options { InputFilename = @"plan.zpp", MetricsFormat = MetricsExport.Json }),
                JobJsonHelper.ClientOptions);

            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.EnumerateObject().Select(x => x.Name).ShouldBe([@"metricsFormat", @"compileTimeout"]);
        }
    }
}
