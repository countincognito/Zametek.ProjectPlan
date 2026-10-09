# Build from source

## Prerequisites

Each project in `Zametek.ProjectPlan.slnx` uses the `net10.0` target framework. Thus, you must install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or a newer version of the SDK to build the projects and to start the desktop application. Previous versions of the SDK, for example .NET 8, cannot build the projects.

To restore, build and start the desktop application, enter these commands:

```
dotnet restore
dotnet build
dotnet run --project src/Zametek.ProjectPlan.Desktop
```

The application has one project that all hosts use, and one project for each host. Thus, all platforms use the same view models, UI components and composition. The table that follows gives a list of the projects:

| Project | Description |
| ------- | ----------- |
| `Zametek.Shell.ProjectPlan` | The source code of the application that all hosts use. It includes the composition root, the dock factory, the styles and the resources. |
| `Zametek.ProjectPlan.Desktop` | The desktop host (`projectplandotnet`). It operates on Windows, Linux and macOS. |
| `Zametek.ProjectPlan.Browser` | The web host. It is an Avalonia WebAssembly application (refer to [The web app](#the-web-app)). |
| `Zametek.ProjectPlan.CommandLine` | The host that has no user interface: `zpp`. It can also operate as a server with the command `zpp serve` (refer to [Command line tool (zpp)](COMMAND-LINE.md)). |
| `Zametek.Engine.ProjectPlan` | The engine that `zpp` uses. The engine converts the bytes of a plan into outputs. Each job has a dependency injection scope that no other job uses. |

Each host supplies three services. These services are different for each host, and they control:

- The location where the application keeps the settings
- The method that the application uses to show dialogs and file pickers
- If MS Project import is available.

The host gives these services to `CompositionRoot.Configure` as an Autofac module. `Zametek.Shell.ProjectPlan` registers all other services one time.

This is not applicable to `zpp`. It has no UI components, and it uses the engine instead. The engine registers all the services that a job uses.

## Git hooks (Husky.Net)

This repository uses [Husky.Net](https://alirezanet.github.io/Husky.Net/) to operate a pre-commit hook. The file `.config/dotnet-tools.json` sets the version of the tool. The installation is automatic: before each restore, `Directory.Build.targets` starts `dotnet tool restore` and `dotnet husky install`. Thus, a standard `dotnet restore` or `dotnet build` installs the hooks. To install the hooks manually, enter these commands:

```
dotnet tool restore
dotnet husky install
```

At each commit, the hook (`.husky/pre-commit`) does these checks on `Zametek.ProjectPlan.slnf`:

1. `dotnet format style --verify-no-changes` checks the style of the source code.
2. `dotnet format analyzers --verify-no-changes` checks the analyzer rules.
3. `dotnet build --no-restore --configuration Debug` builds the source code.
4. `dotnet test --no-build --configuration Debug` starts the test suites.

If the style check or the analyzer check produces errors, enter `dotnet format style` or `dotnet format analyzers`. These commands correct the errors for you. Then stage the changes again and commit.

To prevent the installation of the hooks (for example in CI), set the `HUSKY` environment variable to `0`. To ignore the hook for one commit, use `git commit --no-verify`.

## WebAssembly toolchain (web host)

The web host (`Zametek.ProjectPlan.Browser`) uses the `net10.0-browser` target framework. Thus, to build the web host, you must install the `wasm-tools` workload and the .NET 10 SDK.

The installation of the workload is automatic, as for the Husky hooks. Before it restores a project that uses a browser target framework, `Directory.Build.targets` starts `dotnet workload restore`. Thus, a standard `dotnet build` of the web host installs the toolchain. The builds of the desktop application and the command line tool do not do this step. Thus, the step has no effect on them. To install the workload manually, enter this command:

```
dotnet workload install wasm-tools
```

When you install a workload, the installation writes into the SDK directory. Thus, if the SDK installation is for all users of the computer, you must use a shell that has administrator rights.

The automatic step tries to install the workload, but it cannot stop the build. If the step cannot install the workload, the SDK stops the build with the error `NETSDK1147`. This error gives the name of the missing workload and the command that installs it. To prevent the automatic step (for example in CI, or when you install the workloads with a different method), set the `WASM_WORKLOAD` environment variable to `0`.

## The web app

To start the web app on your computer, enter `make run-browser` or this command:

```
dotnet run --project src/Zametek.ProjectPlan.Browser
```

The command `dotnet publish` writes a self-contained static site to `src/Zametek.ProjectPlan.Browser/bin/<configuration>/net10.0-browser/AppBundle`. Each static web host can supply this site to browsers. These two notes are for the web host that supplies the site:

- Use HTTPS or `localhost` to supply the site. The web app uses some browser APIs, for example the file pickers. These APIs operate only in a secure context.
- Send the header `Service-Worker-Allowed: /` for `_framework/sw.js`, or put that script in the root of the site. Avalonia registers its service worker at the root scope. Without the header, the registration cannot complete. Then Firefox and Safari cannot use their fallback to save files. They use this fallback and not the File System Access API. Chrome and Edge have the native API, and thus this problem has no effect on them.

The web app shows the full application. The dock layout, the data grids, the interactive graphs and the charts all show. But the web app is not complete, and it does not have all the functions of the desktop application. The web app has these known differences:

- **MS Project import is not available, and it cannot be available.** The importer is MPXJ, a Java library that IKVM compiles for .NET. It must have a native OpenJDK runtime image on a disk. Images are available for Windows, Linux and macOS only, and a browser has no disk for an image.
- **The web app cannot open and save files at this time.** A browser gives a file handle and not a path. You cannot read the path from the handle. Thus, the file layer must use streams and not file names.
- **The web app does not keep settings after a page reload.** It keeps the settings only while the page is open, until a store in the browser is available.

## MSI installer (Windows)

The project `Zametek.ProjectPlan.MsiPackager` (in the folder `pkg/`) makes the Windows MSI installer. It uses version 5 of the [WiX Toolset](https://wixtoolset.org/), and the project sets the version of `WixToolset.Sdk` to 5.0.2. The WiX SDK is a NuGet package. Thus, the build of the project restores it. It is not necessary to install the SDK with a different command.

To build the installer in Visual Studio, first install the [WiX Toolset Visual Studio 2022 Extension](https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudio2022Extension). This extension makes it possible to use WiX projects in Visual Studio. Then do these steps:

1. Publish the Windows x64 binaries of the desktop application and the command line tool with `make publish-desktop publish-cli ARCH=x64 OS=win` (refer to [The makefile](#the-makefile)). The installer takes its files from these two publish folders. The build does not check that the publish is new.
2. Set the **Configuration** to `Release`.
3. Set the **Platform** to `x64`.
4. Build the `Zametek.ProjectPlan.MsiPackager` project.

The installer contains the x64 binaries of the publish, whatever the platform is. Thus, use only the platform `x64`. The platforms `x86` and `ARM64` make an installer that has the name of that platform, but it contains the x64 binaries.

The build writes the installer (for example `projectplandotnet.0.10.1.installer.x64.msi`) to the output folder of the project. The CI does not make the MSI. The release workflow supplies only the portable zip and tar.gz archives. Thus, you must build the installer on your computer.

## MSIX packages (Windows)

Two packaging projects in the folder `pkg/` make the MSIX packages: `Zametek.ProjectPlan.Desktop.WapPackager` for the desktop application and `Zametek.ProjectPlan.CommandLine.WapPackager` for the command line tool. These projects are of the type Windows Application Packaging Project. The script `build-msix.ps1` in the root of the repository controls the build.

The script is necessary because the .NET CLI cannot build these projects. A `.wapproj` project must have the Windows App Packaging SDK and the full MSBuild. The script finds MSBuild with `vswhere`. Thus, you must install Visual Studio 2022 or a newer version.

The script also sets the `RuntimeIdentifiers` environment variable during the build. The packaging project publishes its entry point one time for each architecture in the bundle. Thus, one NuGet restore must prepare each of these targets. If it does not, the build stops with the error `NETSDK1047`.

Start the script from the root of the repository:

```
.\build-msix.ps1
```

This command builds the two packages in `Release` for `x86|x64|arm64`, without a signature. It shows the path of each `.msixbundle` that it makes. The table that follows gives a list of the parameters:

| Parameter | Default | Effect |
| --------- | ------- | ------ |
| `-Target` | `Both` | The package to build: `Desktop`, `CommandLine` or `Both` |
| `-Configuration` | `Release` | `Debug` or `Release` |
| `-BundlePlatforms` | `x86\|x64\|arm64` | The architectures in the bundle |
| `-Platform` | Set by the script | The build platform. It must be one of the bundle platforms, or `APPX3104` occurs. The script uses `x64` if the bundle has `x64`. If not, it uses `x86` if the bundle has `x86`. If not, it uses the first platform in the list. |
| `-BuildMode` | `SideloadOnly` | `SideloadOnly` for a local installation, `StoreUpload` for the upload to the Microsoft Store |
| `-Sign` | off | This parameter signs the package with the certificate that you set in the packaging project. |
| `-Clean` | off | This parameter first deletes the `bin` folder of the selected packaging projects and of each project in `src`. The script does not delete `obj` on purpose. Refer to the comment in the script. |

The script writes each bundle to the `AppPackages` folder of its packaging project. As for the MSI, the CI does not make these bundles. You must build them on your computer.

## Linux and WSL

To start the compiled binary on Ubuntu or WSL, it may be necessary first to install these packages:

```
sudo apt-get update
sudo apt-get install libfreetype6
sudo apt-get install libfontconfig1
sudo apt-get install fontconfig
sudo apt-get install libice6
sudo apt-get install libsm6
sudo apt-get install libgtk-3-dev
```

## The makefile

The root of the repository has a `makefile`. Its targets build the projects, publish them and do checks. GNU Make is necessary. Thus, on Windows, use a shell that has `make`, for example Git Bash with make installed, or WSL. The command `make` shows the available targets and the accepted values for `ARCH` and `OS`:

```
make
```

The table that follows gives a list of the targets:

| Target | Effect |
| ------ | ------ |
| `build` | This target builds the desktop application, the command line tool and the web app. To build only one of them, use `build-desktop`, `build-cli` or `build-browser`. |
| `publish` | This target makes self-contained, single-file builds of the desktop application and the command line tool. You supply these builds to users. To do only one of them, use `publish-desktop` or `publish-cli`. |
| `publish-browser` | This target makes the static site bundle of the web app. |
| `run-browser` | This target starts the web app on your computer at `http://localhost:5210`. |
| `clean` | This target cleans all the projects. |
| `hooks` | This target installs the pre-commit hooks manually (refer to [Git hooks](#git-hooks-huskynet)). |
| `workloads` | This target installs the WebAssembly toolchain manually (refer to [WebAssembly toolchain](#webassembly-toolchain-web-host)). |
| `format` | This target corrects the style of the source code in the projects of the filter file `Zametek.ProjectPlan.slnf`. |
| `format-check` | This target does a check of the style of the source code, and it does not change files. |
| `lint` | This target makes a `Release` build of the projects in the filter file, to make sure that the source code compiles. |
| `lint-api` | This target does a check of the API description `docs/openapi.yaml` with Spectral and the rules in `.spectral.yaml`, as the CI does. Node.js is necessary. |
| `test` | This target starts all the test suites in `Release`. |

The `build` and `publish` targets are different from the standard SDK commands. They build for an OS and an architecture that you give. The variables `ARCH`, `OS` and `CONFIGURATION` set these values. You can change the variables on the command line. These are the values:

- `ARCH`: `x64`, `x86` or `arm64`. The default is `x64`.
- `OS`: `win`, `linux` or `osx`. The default is `win`.
- `CONFIGURATION`: the default is `Release`.

For example:

```
make publish-cli OS=linux ARCH=arm64
```

The published output goes to `src/<project>/bin/<configuration>/net10.0/<os>-<arch>/publish/`. For example, the default `make publish-cli` writes a self-contained `zpp.exe` to `src/Zametek.ProjectPlan.CommandLine/bin/Release/net10.0/win-x64/publish/`.
