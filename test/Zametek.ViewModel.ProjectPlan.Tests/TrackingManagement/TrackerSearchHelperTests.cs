using Shouldly;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class TrackerSearchHelperTests
    {
        [Fact]
        public void GetSearchSymbol_Given_NoLastTrackerIndex_Then_Nowhere()
        {
            TrackerSearchHelper.GetSearchSymbol(null, 5)
                .ShouldBe(Resource.ProjectPlan.Symbols.Symbol_Nowhere);
        }

        [Fact]
        public void GetSearchSymbol_Given_LastAheadOfCurrent_Then_Forwards()
        {
            TrackerSearchHelper.GetSearchSymbol(7, 5)
                .ShouldBe(Resource.ProjectPlan.Symbols.Symbol_Forwards);
        }

        [Fact]
        public void GetSearchSymbol_Given_LastBehindCurrent_Then_Backwards()
        {
            TrackerSearchHelper.GetSearchSymbol(3, 5)
                .ShouldBe(Resource.ProjectPlan.Symbols.Symbol_Backwards);
        }

        [Fact]
        public void GetSearchSymbol_Given_LastAtCurrent_Then_InPlace()
        {
            TrackerSearchHelper.GetSearchSymbol(5, 5)
                .ShouldBe(Resource.ProjectPlan.Symbols.Symbol_InPlace);
        }

        [Fact]
        public void GetSearchSymbol_Given_ZeroIndexes_Then_InPlace()
        {
            TrackerSearchHelper.GetSearchSymbol(0, 0)
                .ShouldBe(Resource.ProjectPlan.Symbols.Symbol_InPlace);
        }
    }
}
