# Building from source

## Prerequisites

Every project in the solution targets `net10.0`, so you need the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (or above) installed to build and run it - earlier SDKs (such as .NET 8) will not build the solution. Once installed, it can be built and run with the standard SDK commands:

```
dotnet restore
dotnet build
dotnet run --project src/Zametek.ProjectPlan.Desktop
```

The application is split into a shared project and one project per host, so that the same view models, views and composition serve every platform:

| Project | Role |
| ------- | ---- |
| `Zametek.Shell.ProjectPlan` | The shared application: composition root, dock factory, and the styles and resources every host presents |
| `Zametek.ProjectPlan.Desktop` | The desktop host (`projectplandotnet`), on Windows, Linux and macOS |
| `Zametek.ProjectPlan.Browser` | The web host, an Avalonia WebAssembly application (see below) |
| `Zametek.ProjectPlan.CommandLine` | The headless host, `zpp`, which also runs as a server, `zpp serve` (see [Command line tool (zpp)](COMMAND-LINE.md)) |
| `Zametek.Engine.ProjectPlan` | The headless engine `zpp` runs on: it takes a plan's bytes to its outputs, each job in a DI scope of its own |

Each host supplies the three services that cannot be shared - where settings persist, how dialogs and file pickers are presented, and whether MS Project import is available - as an Autofac module handed to `CompositionRoot.Configure`. Everything else is registered once, in `Zametek.Shell.ProjectPlan`. `zpp` is the exception: it has no views, and runs on the engine, which registers everything a job needs itself.

## Git hooks (Husky.Net)

This repository uses [Husky.Net](https://alirezanet.github.io/Husky.Net/) to run a pre-commit hook. The tool is pinned in `.config/dotnet-tools.json` and is installed automatically: `Directory.Build.targets` runs `dotnet tool restore` and `dotnet husky install` before every restore, so a normal `dotnet restore` (or `dotnet build`) sets the hooks up for you. To install them manually, run:

```
dotnet tool restore
dotnet husky install
```

On every commit, the hook (`.husky/pre-commit`) runs the following checks against `Zametek.ProjectPlan.slnf`:

1. `dotnet format style --verify-no-changes` - code style.
2. `dotnet format analyzers --verify-no-changes` - analyzer rules.
3. `dotnet build --no-restore --configuration Debug` - compilation.
4. `dotnet test --no-build --configuration Debug` - the test suites.

If the style or analyzer check fails, run `dotnet format style` or `dotnet format analyzers` to fix the issues automatically, then re-stage and commit.

To skip hook installation (for example in CI), set the `HUSKY` environment variable to `0`. To bypass the hook for a single commit, use `git commit --no-verify`.

## WebAssembly toolchain (browser head)

The browser head (`Zametek.ProjectPlan.Browser`) targets `net10.0-browser` and therefore needs the `wasm-tools` workload on top of the .NET 10 SDK. Like the Husky hooks, it is provisioned automatically: `Directory.Build.targets` runs `dotnet workload restore` before restoring any project whose target framework is a browser one, so a normal `dotnet build` of the browser head sets the toolchain up for you. Desktop and CLI builds skip that step entirely, so they are unaffected. To install it manually, run:

```
dotnet workload install wasm-tools
```

Installing a workload writes into the SDK directory, so on a machine-wide SDK install it needs an elevated shell. The automatic step is best-effort and never fails the build; if it could not install the workload, the SDK stops the build itself with `NETSDK1147`, naming the missing workload and the command that installs it. To skip the automatic step (for example in CI, or when workloads are provisioned separately), set the `WASM_WORKLOAD` environment variable to `0`.

## Running the web app

With the toolchain installed, serve it locally with `make run-browser`, or:

```
dotnet run --project src/Zametek.ProjectPlan.Browser
```

`dotnet publish` writes a self-contained static site to `src/Zametek.ProjectPlan.Browser/bin/<configuration>/net10.0-browser/AppBundle`, which any static web host can serve. Two hosting notes:

- Serve it over HTTPS or from `localhost`. Several browser APIs the app relies on, including the file pickers, require a secure context.
- Send a `Service-Worker-Allowed: /` header for `_framework/sw.js`, or serve that script from the site root. Avalonia registers its service worker at the root scope, and without it registration fails - which costs Firefox and Safari the save-file fallback they use in place of the File System Access API. Chrome and Edge have the native API and are unaffected.

The web app presents the whole application - the dock layout, the data grids, the interactive graphs and the charts all render - but it is still being brought up, and does not yet match the desktop application. Known gaps:

- **MS Project import is not available and cannot be.** The importer is MPXJ, the Java library cross-compiled by IKVM, which needs a native OpenJDK runtime image on disk; those images are published for Windows, Linux and macOS only, and a browser has no disk to put one on.
- **Opening and saving files is not wired up.** A browser hands back an opaque file handle rather than a path, so this needs the file layer to work in streams rather than file names.
- **Settings do not survive a page reload.** They are held for the lifetime of the page, pending a store backed by the browser.

## Building the MSI installer (Windows)

The Windows MSI installer is produced by the `Zametek.ProjectPlan.MsiPackager` project (under `pkg/`) using version 5 of the [WiX Toolset](https://wixtoolset.org/) (the project pins `WixToolset.Sdk` 5.0.2). The WiX SDK is a NuGet package, so it is restored automatically when the project is built - no separate command-line install is required.

To build the installer from Visual Studio, first install the [WiX Toolset Visual Studio 2022 Extension](https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudio2022Extension), which adds Visual Studio support for WiX projects. Then:

1. Set the solution **Configuration** to `Release`.
2. Set the **Platform** to the target architecture (`x64`, `x86`, or `ARM64`).
3. Build the `Zametek.ProjectPlan.MsiPackager` project.

The resulting installer (for example `projectplandotnet.0.9.3.installer.x64.msi`) is written to the project's output folder. Note that the MSI is not produced by CI - the release workflow ships only the portable zip / tar.gz archives - so the installer must be built locally.

## Building the MSIX packages (Windows)

MSIX packages are produced by the two Windows Application Packaging projects under `pkg/` - `Zametek.ProjectPlan.Desktop.WapPackager` for the desktop app and `Zametek.ProjectPlan.CommandLine.WapPackager` for the CLI - driven by the `build-msix.ps1` script in the repository root. The script exists because these projects cannot be built by the .NET CLI: `.wapproj` needs the Windows App Packaging SDK and therefore full MSBuild, which the script locates through `vswhere`, so Visual Studio 2022 or newer must be installed. It also sets the `RuntimeIdentifiers` environment variable for the duration of the build, because the packaging project publishes its entry point once per architecture in the bundle and NuGet restore has to have populated every one of those targets in a single pass or the build fails with `NETSDK1047`.

Run it from the repository root:

```
.\build-msix.ps1
```

That builds both packages in `Release` for `x86|x64|arm64`, unsigned, and prints the path of each resulting `.msixbundle`. The parameters:

| Parameter | Default | Effect |
| --------- | ------- | ------ |
| `-Target` | `Both` | Which package to build: `Desktop`, `CommandLine`, or `Both` |
| `-Configuration` | `Release` | `Debug` or `Release` |
| `-BundlePlatforms` | `x86\|x64\|arm64` | The architectures to include in the bundle |
| `-Platform` | derived | The build platform, which must be one of the bundle platforms or `APPX3104` fires; picks `x64` if it is in the bundle, else `x86`, else whichever is listed first |
| `-BuildMode` | `SideloadOnly` | `SideloadOnly` for local installation, `StoreUpload` for submission |
| `-Sign` | off | Sign the package with the certificate configured in the packaging project |
| `-Clean` | off | Delete `bin` for the selected packaging projects and for every project in `src` first. `obj` is left in place deliberately - see the comment in the script |

Each bundle is written to the packaging project's own `AppPackages` folder. As with the MSI, CI does not produce these - they must be built locally.

## Running on Linux or WSL

When running on Ubuntu or WSL, you will likely need to install the following packages for the compiled binary to run:

```
sudo apt-get update
sudo apt-get install libfreetype6
sudo apt-get install libfontconfig1
sudo apt-get install fontconfig
sudo apt-get install libice6
sudo apt-get install libsm6
sudo apt-get install libgtk-3-dev
```

## Using the makefile

The repository root contains a `makefile` that wraps the most common build, publish, and verification commands. It requires GNU Make, so on Windows run it from a shell that provides `make` (for example Git Bash with make installed, or WSL). Running `make` on its own prints the available targets along with the accepted `ARCH` and `OS` values:

```
make
```

The targets:

| Target | Effect |
| ------ | ------ |
| `build` | Compile the desktop app, the CLI and the web app (`build-desktop` / `build-cli` / `build-browser` for one at a time) |
| `publish` | Produce self-contained, single-file distribution builds of the desktop app and CLI (`publish-desktop` / `publish-cli` individually) |
| `publish-browser` | Produce the web app's static site bundle |
| `run-browser` | Serve the web app locally on `http://localhost:5210` |
| `clean` | Clean the solution |
| `hooks` | Install the pre-commit hooks manually (see the Git hooks section above) |
| `workloads` | Install the WebAssembly build toolchain manually (see the WebAssembly toolchain section above) |
| `format` | Apply code style fixes to the solution filter |
| `format-check` | Verify code style without modifying files |
| `lint` | Release build of the solution filter, as a compilation check |
| `lint-api` | Lint the API description, `docs/openapi.yaml`, with Spectral and the rules of `.spectral.yaml`, as CI does (needs Node.js) |
| `test` | Run all test suites in Release |

Unlike the plain SDK commands above, the `build` and `publish` targets compile for an explicit OS and architecture. They are parameterised by variables that can be overridden on the command line: `ARCH` (`x64`, `x86`, `arm64`; default `x64`), `OS` (`win`, `linux`, `osx`; default `win`) and `CONFIGURATION` (default `Release`). For example:

```
make publish-cli OS=linux ARCH=arm64
```

Published output lands in `src/<project>/bin/<configuration>/net10.0/<os>-<arch>/publish/` - for example, the default `make publish-cli` writes a self-contained `zpp.exe` to `src/Zametek.ProjectPlan.CommandLine/bin/Release/net10.0/win-x64/publish/`.
