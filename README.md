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

## Building from source

### Prerequisites

Every project in the solution targets `net10.0`, so you need the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (or above) installed to build and run it - earlier SDKs (such as .NET 8) will not build the solution. Once installed, it can be built and run with the standard SDK commands:

```
dotnet restore
dotnet build
dotnet run --project src/Zametek.ProjectPlan
```

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

## Attributions

Application icon using [Project management icons created by Flat Icons - Flaticon](https://www.flaticon.com/free-icons/project-management).

[![Gitter](https://badges.gitter.im/Zametek-ProjectPlan/Lobby.svg)](https://gitter.im/Zametek-ProjectPlan/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
