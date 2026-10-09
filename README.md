# projectplan.net

<a href="https://apps.microsoft.com/detail/9mw5mdp78528?referrer=appbadge&cid=github&mode=direct">
	<img src="https://get.microsoft.com/images/en-us%20light.svg" width="200"/>
</a>

Projectplan.net is an open-source, cross-platform desktop application for designing and creating project plans. It automates many of the tasks necessary for good project design, as explained in [Righting Software](https://rightingsoftware.org/). It can be also used as a free and simple desktop alternative to Microsoft Project for project planning and tracking.

This product is freely available for download from: [https://www.getprojectplan.net](https://www.getprojectplan.net)

## Donations

You can donate to the project [here](https://www.patreon.com/zametek).

## Documentation

For user documentation, refer to the [project wiki](https://github.com/countincognito/Zametek.ProjectPlan/wiki).

The other documentation is in the [`docs`](docs) folder:

- [Build from source](docs/BUILDING.md): This document has the prerequisites, the Git hooks, the web app, the Windows installers, Linux and WSL, and the makefile.
- [Command line tool (zpp)](docs/COMMAND-LINE.md): This document explains how to make the outputs of a plan without the desktop application. It also gives the exit codes and explains how to operate `zpp` as a server.
- [Client-server quick start](docs/SERVER.md): This document is a step-by-step guide. It shows `zpp serve` on Windows, with HTTPS or on a Unix domain socket. It also shows `zpp` when it sends its jobs to the server. You can download sample plans.
- [The zpp serve API](docs/API.md): This document is the reference for the HTTP API of the server. It explains how to compile a plan and how to list the scenarios of a plan. It also gives a list of each error that the server sends in a response, and it has the security review and the changelog. The file [openapi.yaml](docs/openapi.yaml) is the description of the API for computer programs. The server supplies this file at `/v1/openapi`.
- [RESTful API Guide](docs/RESTFUL-API-GUIDE.md): This document gives the rules for style that the `zpp serve` API follows. The rules are for URIs, methods, representations, errors, security, versioning and documentation. The API reference refers to each rule with its identifier.
- [Architecture](docs/ARCHITECTURE.md): This document explains the internal parts of the application. It shows how a change moves along the compilation pipeline. It also explains how the application stops and replays bulk updates (when plans are loaded, imported and reset). It also explains the rules for thread management to prevent deadlocks.
- [TODO](docs/TODO.md): This document is the list of the technical work that the maintainers will do. The file is in the repository, and thus it changes with the code.
- [Glossary](docs/GLOSSARY.md): This document explains the terms of the project and the rules for the language of the documentation.

## Attributions

All documentation in this repository is written loosely following [ASD-STE100 Simplified Technical English Issue 9](https://www.asd-ste100.org/assets/files/ASD-STE100_ISSUE9.pdf).

The application icon uses [Project management icons created by Flat Icons - Flaticon](https://www.flaticon.com/free-icons/project-management).

[![Gitter](https://badges.gitter.im/Zametek-ProjectPlan/Lobby.svg)](https://gitter.im/Zametek-ProjectPlan/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
