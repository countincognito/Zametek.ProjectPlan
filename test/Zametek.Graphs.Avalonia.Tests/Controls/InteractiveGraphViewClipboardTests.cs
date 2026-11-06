using Avalonia.Headless;
using Shouldly;
using SkiaSharp;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Controls
{
    // Copy Image puts the image on the clipboard as a bitmap and as PNG bytes. An empty image is a failure, not an image:
    // before, the clipboard got empty PNG bytes, and the copy reported no error.
    [Collection("Headless rendering")]
    public class InteractiveGraphViewClipboardTests
    {
        private readonly HeadlessUnitTestSession m_Session;

        public InteractiveGraphViewClipboardTests(HeadlessSessionFixture fixture)
        {
            m_Session = fixture.Session;
        }

        [Fact]
        public async Task An_empty_image_is_not_put_on_the_clipboard()
        {
            await m_Session.Dispatch(
                () => InteractiveGraphView.BuildImageDataTransfer([]).ShouldBeNull(),
                CancellationToken.None);
        }

        [Fact]
        public async Task An_image_is_put_on_the_clipboard()
        {
            using var recorder = new SKPictureRecorder();
            SKCanvas canvas = recorder.BeginRecording(new SKRect(0f, 0f, 40f, 30f));
            using (var paint = new SKPaint { Color = SKColors.SteelBlue })
            {
                canvas.DrawRect(5f, 5f, 30f, 20f, paint);
            }

            using SKPicture picture = recorder.EndRecording();
            byte[] png = ImageExporter.RenderToPng(picture);

            await m_Session.Dispatch(
                () => InteractiveGraphView.BuildImageDataTransfer(png).ShouldNotBeNull(),
                CancellationToken.None);
        }
    }
}
