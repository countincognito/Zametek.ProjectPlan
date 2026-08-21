# projectplan.net

<a href="https://apps.microsoft.com/detail/9mw5mdp78528?referrer=appbadge&cid=github&mode=direct">
	<img src="https://get.microsoft.com/images/en-us%20light.svg" width="200"/>
</a>

Projectplan.net is an Open Source, cross-platform desktop application for designing and creating project plans. It is built to automate many of the tasks necessary for good project design, as detailed in [Righting Software](https://rightingsoftware.org/). However, it can be also used as a free and simple desktop alternative to Microsoft Project for project planning and tracking.

This product is freely available for download from: [https://www.getprojectplan.net](https://www.getprojectplan.net)

## Donations

You can donate to the project [here](https://www.patreon.com/zametek).

You should only spend money on projectplan.net if you can afford to and if you want to support ongoing development.

## Documentation

For user documentation, see the [project wiki](https://github.com/countincognito/Zametek.ProjectPlan/wiki).

For anyone interested in the internals - or contributing to them - [ARCHITECTURE.md](ARCHITECTURE.md) describes the reactive update architecture: how edits propagate through the compile pipeline, how bulk updates (project loads, imports, resets) are suppressed and replayed, and the threading rules that keep it all deadlock-free.

## Building from source

### Prerequisites

Every project in the solution targets `net10.0`, so you need the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (or above) installed to build and run it - earlier SDKs (such as .NET 8) will not build the solution. Once installed, it can be built and run with the standard SDK commands:

```
dotnet restore
dotnet build
dotnet run --project src/Zametek.ProjectPlan.Desktop
```

The application is split into a shared project and one project per host, so that the same view models, views and composition serve every platform:

| Project | Role |
| ------- | ---- |
| `Zametek.ProjectPlan.Core` | The shared application: composition root, dock factory, and the styles and resources every host presents |
| `Zametek.ProjectPlan.Desktop` | The desktop host (`projectplandotnet`), on Windows, Linux and macOS |
| `Zametek.ProjectPlan.Browser` | The web host, an Avalonia WebAssembly application (see below) |
| `Zametek.ProjectPlan.CommandLine` | The headless host, `zpp` (see below) |

Each host supplies the three services that cannot be shared - where settings persist, how dialogs and file pickers are presented, and whether MS Project import is available - as an Autofac module handed to `CompositionRoot.Configure`. Everything else is registered once, in `Core`.

### Git hooks (Husky.Net)

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

### WebAssembly toolchain (browser head)

The browser head (`Zametek.ProjectPlan.Browser`) targets `net10.0-browser` and therefore needs the `wasm-tools` workload on top of the .NET 10 SDK. Like the Husky hooks, it is provisioned automatically: `Directory.Build.targets` runs `dotnet workload restore` before restoring any project whose target framework is a browser one, so a normal `dotnet build` of the browser head sets the toolchain up for you. Desktop and CLI builds skip that step entirely, so they are unaffected. To install it manually, run:

```
dotnet workload install wasm-tools
```

Installing a workload writes into the SDK directory, so on a machine-wide SDK install it needs an elevated shell. The automatic step is best-effort and never fails the build; if it could not install the workload, the SDK stops the build itself with `NETSDK1147`, naming the missing workload and the command that installs it. To skip the automatic step (for example in CI, or when workloads are provisioned separately), set the `WASM_WORKLOAD` environment variable to `0`.

### Running the web app

With the toolchain installed, serve it locally with `make run-browser`, or:

```
dotnet run --project src/Zametek.ProjectPlan.Browser
```

`dotnet publish` writes a self-contained static site to `src/Zametek.ProjectPlan.Browser/bin/<configuration>/net10.0-browser/AppBundle`, which any static web host can serve. Two hosting notes:

- Serve it over HTTPS or from `localhost`. Several browser APIs the app relies on, including the file pickers, require a secure context.
- Send a `Service-Worker-Allowed: /` header for `_framework/sw.js`, or serve that script from the site root. Avalonia registers its service worker at the root scope, and without it registration fails - which costs Firefox and Safari the save-file fallback they use in place of the File System Access API. Chrome and Edge have the native API and are unaffected.

The web app is still being brought up, and does not yet match the desktop application. Known gaps:

- **MS Project import is not available and cannot be.** The importer is MPXJ, the Java library cross-compiled by IKVM, which needs a native OpenJDK runtime image on disk; those images are published for Windows, Linux and macOS only, and a browser has no disk to put one on.
- **Opening and saving files is not wired up.** A browser hands back an opaque file handle rather than a path, so this needs the file layer to work in streams rather than file names.
- **Settings do not survive a page reload.** They are held for the lifetime of the page, pending a store backed by the browser.

### Building the MSI installer (Windows)

The Windows MSI installer is produced by the `Zametek.ProjectPlan.MsiPackager` project (under `pkg/`) using version 5 of the [WiX Toolset](https://wixtoolset.org/) (the project pins `WixToolset.Sdk` 5.0.2). The WiX SDK is a NuGet package, so it is restored automatically when the project is built - no separate command-line install is required.

To build the installer from Visual Studio, first install the [WiX Toolset Visual Studio 2022 Extension](https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudio2022Extension), which adds Visual Studio support for WiX projects. Then:

1. Set the solution **Configuration** to `Release`.
2. Set the **Platform** to the target architecture (`x64`, `x86`, or `ARM64`).
3. Build the `Zametek.ProjectPlan.MsiPackager` project.

The resulting installer (for example `projectplandotnet.0.9.3.installer.x64.msi`) is written to the project's output folder. Note that the MSI is not produced by CI - the release workflow ships only the portable zip / tar.gz archives - so the installer must be built locally.

### Running on Linux or WSL

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

### Using the makefile

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
| `test` | Run all test suites in Release |

Unlike the plain SDK commands above, the `build` and `publish` targets compile for an explicit OS and architecture. They are parameterised by variables that can be overridden on the command line: `ARCH` (`x64`, `x86`, `arm64`; default `x64`), `OS` (`win`, `linux`, `osx`; default `win`) and `CONFIGURATION` (default `Release`). For example:

```
make publish-cli OS=linux ARCH=arm64
```

Published output lands in `src/<project>/bin/<configuration>/net10.0/<os>-<arch>/publish/` - for example, the default `make publish-cli` writes a self-contained `zpp.exe` to `src/Zametek.ProjectPlan.CommandLine/bin/Release/net10.0/win-x64/publish/`.

## Command line tool (zpp)

The solution also ships a headless command line tool, `zpp` (the `Zametek.ProjectPlan.CommandLine` project), which opens or imports a project, compiles it, and produces any combination of outputs without launching the desktop app - useful for scripting, CI pipelines, and batch processing. Build it with the standard SDK commands above, or produce a self-contained single-file build with `make publish-cli`.

### Usage

Compile a project and print its metrics:

```
zpp -i plan.zpp
```

Produce chart and graph images (sizes are `<width>:<height>` in pixels):

```
zpp -i plan.zpp --gantt-directory out --gantt-size 1200:800 --gantt-format png --arrow-directory out --arrow-format svg --scenario-chart-directory out --scenario-chart-size 1200:800
```

Work with scenarios:

```
zpp -i plan.zpp --list-scenarios
zpp -i plan.zpp --scenario "Iteration 2" -o plan-iter2.zpp
```

`--scenario` accepts a name or an id - either the full id shown by `--list-scenarios` or, git-style, any unique prefix of it that is at least four hex characters long (an exact name match wins over an id prefix, the same way a git ref beats an abbreviated commit hash).

Emit machine-readable metrics (stdout carries only the JSON document, so it pipes cleanly):

```
zpp -i plan.zpp --metrics-format json
```

Import from Microsoft Project or Excel, and convert to a project file:

```
zpp -m plan.mpp -o plan.zpp
```

Run `zpp --help` for the full option list. Chart and graph exports honour the display settings saved in the project file (the theme excepted - pass `--base-theme Dark` for dark output). Diagnostic logging goes to stderr, never stdout: warnings and errors always show, and `--verbose` adds informational lifecycle output.

Every compilation runs under a watchdog: `--compile-timeout` gives it a budget in milliseconds (5000 by default), and a compilation that runs past it is cancelled and exits with code 4. Large plans can legitimately need longer, so raise it - or pass `--compile-timeout 0` to switch the limit off entirely - for a batch run that must not be interrupted. The desktop application applies the same budget, read from `CompilationTimeoutMilliseconds` in its settings file.

### Exit codes

The exit codes are a contract for scripts and CI gates, pinned by the `Zametek.ProjectPlan.CommandLine.Tests` suite:

| Code | Meaning |
| ---- | ------- |
| 0 | Success |
| 1 | Runtime failure (bad paths, unreadable files, unexpected errors) |
| 2 | Bad usage (invalid options or combinations) |
| 3 | The project compiled with errors |
| 4 | A compilation ran past `--compile-timeout` and was cancelled |

## Attributions

Application icon using [Project management icons created by Flat Icons - Flaticon](https://www.flaticon.com/free-icons/project-management).

[![Gitter](https://badges.gitter.im/Zametek-ProjectPlan/Lobby.svg)](https://gitter.im/Zametek-ProjectPlan/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
