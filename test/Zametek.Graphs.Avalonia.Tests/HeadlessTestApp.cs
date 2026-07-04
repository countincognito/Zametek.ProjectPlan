using Avalonia;
using Avalonia.Headless;
using ReactiveUI.Avalonia;

namespace Zametek.Graphs.Avalonia.Tests
{
    // Minimal headless Avalonia app for the rendering tests. UseHeadlessDrawing = false selects the real
    // Skia renderer, so RenderTargetBitmap produces actual pixels (the default headless drawing is a no-op).
    // Driven manually via HeadlessUnitTestSession (see the render tests) rather than the Avalonia.Headless
    // .XUnit integration, which targets xunit v3 and would clash with this project's xunit v2.
    public sealed class HeadlessTestApp : Application
    {
    }

    public static class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<HeadlessTestApp>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .UseReactiveUI(builder => builder.WithAvalonia());
    }
}
