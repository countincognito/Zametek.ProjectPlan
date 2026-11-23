using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// The engine takes each exception of a read for a file that it cannot read, also one that is a defect and not a bad file,
    /// for example a NullReferenceException in a mapper. The server answered 422 project-not-readable, and wrote nothing to
    /// its log (from the review of src, test and pkg). Such an exception now goes to the log as a warning.
    /// </summary>
    public class ProjectEndpointsNotReadableLogTests
    {
        private const string c_Path = @"/v1/projects/compile";

        public static IEnumerable<object[]> ExceptionsOfAReader =>
        [
            [new Newtonsoft.Json.JsonReaderException(@"Unexpected character")],
            [new Newtonsoft.Json.JsonSerializationException(@"Error converting value")],
            [new InvalidDataException(@"The version is not one that the reader knows")],
            [new System.Xml.XmlException(@"Data at the root level is invalid")],
            [new EndOfStreamException()],
        ];

        [Fact]
        public void LogIfNotFromAReader_Given_ADefectInTheRead_Then_AWarningWithTheException()
        {
            var logger = new RecordingLogger<ProjectEndpoints>();
            var defect = new NullReferenceException(@"Object reference not set to an instance of an object.");

            ProjectEndpoints.LogIfNotFromAReader(logger, Request(), new ProjectNotReadableException(defect.Message, defect));

            logger.Entries.ShouldHaveSingleItem().ShouldBe((LogLevel.Warning, $@"{c_Path}: the project was not readable, because of an exception that is not of a reader"));
        }

        [Theory]
        [MemberData(nameof(ExceptionsOfAReader))]
        public void LogIfNotFromAReader_Given_AFileThatAReaderRefuses_Then_NoLine(Exception inner)
        {
            var logger = new RecordingLogger<ProjectEndpoints>();

            ProjectEndpoints.LogIfNotFromAReader(logger, Request(), new ProjectNotReadableException(inner.Message, inner));

            logger.Entries.ShouldBeEmpty();
        }

        private static DefaultHttpContext Request()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = c_Path;
            return context;
        }
    }
}
