using ScottPlot;
using Shouldly;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // Every chart format is written to a stream the caller owns, so the exporter must write the format it is asked for
    // and leave the stream open for the caller to read back, send or close.
    public class ScottPlotImageExporterTests
    {
        private static Plot BuildPlot()
        {
            var plot = new Plot();
            plot.Add.Scatter(new[] { 1.0, 2.0, 3.0 }, new[] { 1.0, 4.0, 9.0 });
            return plot;
        }

        private static async Task<byte[]> WriteAsync(ChartImageFormat format)
        {
            using Plot plot = BuildPlot();
            using var stream = new MemoryStream();

            await new ScottPlotImageExporter().WritePlotImageAsync(plot, stream, format, 400, 300);

            stream.CanWrite.ShouldBeTrue();
            return stream.ToArray();
        }

        [Theory]
        [InlineData(ChartImageFormat.Jpeg, new byte[] { 0xFF, 0xD8, 0xFF })]
        [InlineData(ChartImageFormat.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
        [InlineData(ChartImageFormat.Bmp, new byte[] { 0x42, 0x4D })]
        [InlineData(ChartImageFormat.Pdf, new byte[] { 0x25, 0x50, 0x44, 0x46 })]
        public async Task WritePlotImageAsync_Given_Format_Then_WritesThatFormatAndLeavesTheStreamOpen(ChartImageFormat format, byte[] signature)
        {
            byte[] data = await WriteAsync(format);

            data.Take(signature.Length).ShouldBe(signature);
        }

        [Fact]
        public async Task WritePlotImageAsync_Given_Webp_Then_WritesWebpAndLeavesTheStreamOpen()
        {
            byte[] data = await WriteAsync(ChartImageFormat.Webp);

            Encoding.ASCII.GetString(data, 0, 4).ShouldBe(@"RIFF");
            Encoding.ASCII.GetString(data, 8, 4).ShouldBe(@"WEBP");
        }

        [Fact]
        public async Task WritePlotImageAsync_Given_Svg_Then_WritesUtf8SvgWithoutAByteOrderMark()
        {
            // As ScottPlot's own SVG file save writes it.
            byte[] data = await WriteAsync(ChartImageFormat.Svg);

            data[0].ShouldNotBe((byte)0xEF);
            Encoding.UTF8.GetString(data).ShouldContain(@"<svg");
        }
    }
}
