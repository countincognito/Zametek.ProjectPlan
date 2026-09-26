using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class GraphFileFormatMapperTests
    {
        [Fact]
        public void ToGraphFileFormat_Given_EveryFormat_Then_MapsToTheLibraryFormatOfTheSameName()
        {
            foreach (GraphExportFormat format in Enum.GetValues<GraphExportFormat>())
            {
                format.ToGraphFileFormat().ToString().ShouldBe(format.ToString());
            }
        }
    }
}
