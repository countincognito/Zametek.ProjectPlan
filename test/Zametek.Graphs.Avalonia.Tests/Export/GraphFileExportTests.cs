using Shouldly;
using SkiaSharp;
using System.Text;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Export
{
    // A graph is written to a stream in the GraphFileFormat asked for, and the stream is left open for the caller. The
    // Save-As path, which has only the file name the user picked, reads the format from its extension.
    public class GraphFileExportTests
    {
        [Theory]
        [InlineData(@"graph.jpeg", GraphFileFormat.Jpeg)]
        [InlineData(@"graph.png", GraphFileFormat.Png)]
        [InlineData(@"graph.pdf", GraphFileFormat.Pdf)]
        [InlineData(@"graph.svg", GraphFileFormat.Svg)]
        [InlineData(@"graph.graphml", GraphFileFormat.GraphML)]
        [InlineData(@"graph.dot", GraphFileFormat.GraphViz)]
        public void A_file_extension_names_its_format(string filename, GraphFileFormat expected)
        {
            GraphFileExtensions.GetFileFormat(filename).ShouldBe(expected);
        }

        [Fact]
        public void An_unknown_file_extension_is_refused()
        {
            ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(
                () => GraphFileExtensions.GetFileFormat(@"graph.gif"));

            exception.Message.ShouldStartWith($@"{Graphs_Messages.Message_UnableToSaveFile} graph.gif");
        }

        [Theory]
        [InlineData(GraphFileFormat.Jpeg, new byte[] { 0xFF, 0xD8, 0xFF })]
        [InlineData(GraphFileFormat.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
        [InlineData(GraphFileFormat.Pdf, new byte[] { 0x25, 0x50, 0x44, 0x46 })]
        public async Task A_picture_is_written_in_the_format_asked_for_and_the_stream_left_open(GraphFileFormat format, byte[] signature)
        {
            byte[] data = await WriteAsync(format);

            data.Take(signature.Length).ShouldBe(signature);
        }

        [Fact]
        public async Task A_picture_is_written_as_SVG_and_the_stream_left_open()
        {
            byte[] data = await WriteAsync(GraphFileFormat.Svg);

            Encoding.UTF8.GetString(data).ShouldContain(@"<svg");
        }

        [Theory]
        [InlineData(GraphFileFormat.GraphML)]
        [InlineData(GraphFileFormat.GraphViz)]
        public async Task The_data_formats_are_not_drawn_from_a_picture(GraphFileFormat format)
        {
            using SKPicture picture = BuildPicture();
            using var stream = new MemoryStream();

            await Should.ThrowAsync<ArgumentOutOfRangeException>(() => ImageExporter.WriteImageAsync(picture, stream, format));
        }

        private static async Task<byte[]> WriteAsync(GraphFileFormat format)
        {
            using SKPicture picture = BuildPicture();
            using var stream = new MemoryStream();

            await ImageExporter.WriteImageAsync(picture, stream, format);

            stream.CanWrite.ShouldBeTrue();
            return stream.ToArray();
        }

        // A filled rectangle is enough of a picture to encode.
        private static SKPicture BuildPicture()
        {
            using var recorder = new SKPictureRecorder();
            SKCanvas canvas = recorder.BeginRecording(new SKRect(0, 0, 40, 30));
            using var paint = new SKPaint { Color = SKColors.SteelBlue };
            canvas.DrawRect(5, 5, 30, 20, paint);
            return recorder.EndRecording();
        }
    }
}
