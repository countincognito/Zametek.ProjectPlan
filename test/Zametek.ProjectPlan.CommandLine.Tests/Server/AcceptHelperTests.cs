using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for which of the media types an endpoint offers a request's Accept asks for.
    /// </summary>
    public class AcceptHelperTests
    {
        private static readonly string[] s_Offered = [@"application/json", @"application/zip"];

        private static HttpRequest Request(params string[] accept)
        {
            HttpRequest request = new DefaultHttpContext().Request;

            foreach (string value in accept)
            {
                request.Headers.Append(@"Accept", value);
            }

            return request;
        }

        [Theory]
        [InlineData(@"*/*", @"application/json")]
        [InlineData(@"application/json", @"application/json")]
        [InlineData(@"application/zip", @"application/zip")]
        [InlineData(@"application/json, application/zip", @"application/json")]
        [InlineData(@"application/zip, application/json", @"application/json")]
        [InlineData(@"application/zip, application/json;q=0.5", @"application/zip")]
        [InlineData(@"application/json;q=0.2, application/zip;q=0.8", @"application/zip")]
        [InlineData(@"application/*", @"application/json")]
        [InlineData(@"application/*;q=0.1, application/zip", @"application/zip")]
        [InlineData(@"text/html, */*;q=0.1", @"application/json")]
        [InlineData(@"text/html, application/zip;q=0.1", @"application/zip")]
        [InlineData(@"APPLICATION/ZIP", @"application/zip")]
        [InlineData(@"application/json;q=0, */*;q=0.1", @"application/zip")]
        public void Choose_Given_Accept_Then_TheOfferedTypeItPrefers(string accept, string expected)
        {
            AcceptHelper.Choose(Request(accept), s_Offered).ShouldBe(expected);
        }

        [Fact]
        public void Choose_Given_NoAccept_Then_TheFirstOffered()
        {
            AcceptHelper.Choose(Request(), s_Offered).ShouldBe(@"application/json");
        }

        [Fact]
        public void Choose_Given_SeveralAcceptHeaders_Then_ReadsThemAsOne()
        {
            AcceptHelper.Choose(Request(@"text/html", @"application/zip"), s_Offered).ShouldBe(@"application/zip");
        }

        [Theory]
        [InlineData(@"text/html")]
        [InlineData(@"text/*")]
        [InlineData(@"application/json;q=0")]
        [InlineData(@"application/json;q=0, application/zip;q=0")]
        [InlineData(@"application/json;q=0, application/zip;q=0, */*")]
        [InlineData(@"image/png, application/xml;q=0.9")]
        public void Choose_Given_NothingOfferedIsAcceptable_Then_Null(string accept)
        {
            AcceptHelper.Choose(Request(accept), s_Offered).ShouldBeNull();
        }

        [Fact]
        public void Choose_Given_AMoreSpecificRangeThatRefusesAType_Then_ItWinsOverAGeneralOne()
        {
            // JSON is refused by its own range, whatever */* would give it.
            AcceptHelper.Choose(Request(@"application/json;q=0, */*"), [@"application/json"]).ShouldBeNull();
        }
    }
}
