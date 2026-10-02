using Shouldly;
using System.Text.Json;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for how zpp serve's API is written in JSON: the server refuses a
    /// job's options with anything it does not know, while zpp reads an answer
    /// with more in it than it knows - from a newer server, say - and leaves out
    /// of the options it sends the outputs it does not ask for.
    /// </summary>
    public class JobJsonHelperTests
    {
        [Fact]
        public void ServerOptions_Given_OptionsWithSomethingItDoesNotKnow_Then_Refuses()
        {
            Should.Throw<JsonException>(() => JsonSerializer.Deserialize<JobOptions>(@"{""output"":true,""colour"":""red""}", JobJsonHelper.ServerOptions));
        }

        [Fact]
        public void ClientOptions_Given_AnAnswerWithMoreInItThanZppKnows_Then_ReadsIt()
        {
            JobResponse response = JsonSerializer.Deserialize<JobResponse>(
                @"{""jobId"":""1"",""exitCode"":0,""stdout"":"""",""stderr"":"""",""outputs"":[],""transcript"":[],""elapsed"":42}",
                JobJsonHelper.ClientOptions).ShouldNotBeNull();

            response.JobId.ShouldBe(@"1");
            response.Transcript.ShouldBeEmpty();
        }

        [Fact]
        public void ClientOptions_Given_OptionsWithoutChartsOrGraphs_Then_LeavesThemOut()
        {
            string json = JsonSerializer.Serialize(JobOptionsHelper.FromOptions(new Options { InputFilename = @"plan.zpp" }), JobJsonHelper.ClientOptions);

            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.EnumerateObject().Select(x => x.Name)
                .ShouldBe([@"output", @"export", @"baseTheme", @"metricsFormat", @"compileTimeout"], ignoreOrder: true);
        }
    }
}
