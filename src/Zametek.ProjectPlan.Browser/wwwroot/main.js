import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) {
    throw new Error(`Expected to be running in a browser`);
}

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

// The managed entry point does not return until the app shuts down, so the splash text is
// removed first: by the time runMain resolves, Avalonia has long since painted over it.
document.getElementById('loading')?.remove();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
