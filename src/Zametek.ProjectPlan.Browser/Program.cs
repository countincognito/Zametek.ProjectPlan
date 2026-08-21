using Avalonia;
using Avalonia.Browser;
using ReactiveUI.Avalonia;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Zametek.ProjectPlan.Core;

namespace Zametek.ProjectPlan.Browser
{
    internal static class Program
    {
        // "out" is the id of the div in wwwroot/index.html that Avalonia mounts itself into.
        [SupportedOSPlatform("browser")]
        private static Task Main(string[] args)
        {
            // Wire the Autofac-backed Splat locator before BuildAvaloniaApp runs, so that
            // .UseReactiveUI(...) registers ReactiveUI plugins into our container. Same ordering
            // requirement as the desktop head, for the same reason.
            CompositionRoot.Configure(new BrowserPlatformModule());

            return BuildAvaloniaApp()
                .StartBrowserAppAsync("out", new BrowserPlatformOptions
                {
                    // Highest priority first. Software2D is a long way slower than either WebGL mode, but it is
                    // the only one that survives a context the browser refuses to hand out - a hardware
                    // blocklist, an exhausted WebGL context pool, or a canvas that is not compositing - and a
                    // slow chart beats a blank page.
                    RenderingMode =
                    [
                        BrowserRenderingMode.WebGL2,
                        BrowserRenderingMode.WebGL1,
                        BrowserRenderingMode.Software2D,
                    ],

                    // Avalonia's service worker stands in for the save-file picker on browsers without the
                    // File System Access API (Firefox and Safari, as of writing), where saving otherwise has
                    // no way to hand the file back to the user. Requires a secure context: HTTPS or localhost.
                    //
                    // HOSTING REQUIREMENT: the worker is registered at the domain root scope, but the package
                    // serves it from /_framework/sw.js, and a worker may only claim a scope at or above its own
                    // path unless the response carries a "Service-Worker-Allowed: /" header. The dev host
                    // (WasmAppHost) does not send it, so registration fails there with "An unknown error
                    // occurred when fetching the script" - harmless, because every browser that ships the
                    // native File System Access API never reaches the fallback. A real deployment should send
                    // that header, or serve sw.js from the site root, so Firefox and Safari can save files.
                    RegisterAvaloniaServiceWorker = true,
                });
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI(builder => builder.WithAvalonia());
    }
}
