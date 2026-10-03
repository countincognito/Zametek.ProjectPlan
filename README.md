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

The rest of the documentation is in [docs](docs):

- [Building from source](docs/BUILDING.md) - the prerequisites, the git hooks, the web app, the Windows installers, Linux and WSL, and the makefile.
- [Command line tool (zpp)](docs/COMMAND-LINE.md) - producing a project's outputs without launching the desktop app, the exit codes, and running zpp as a server.
- [Client-server quick start](docs/SERVER.md) - `zpp serve` over https on Windows, or on a Unix domain socket, and zpp sending its runs to it, step by step, with sample plans to download.
- [Architecture](docs/ARCHITECTURE.md) - for anyone interested in the internals, or contributing to them: how edits propagate through the compile pipeline, how bulk updates (project loads, imports, resets) are suppressed and replayed, and the threading rules that keep it all deadlock-free.
- [TODO](docs/TODO.md) - the engineering work the maintainers intend, which versions with the code.

## Attributions

Application icon using [Project management icons created by Flat Icons - Flaticon](https://www.flaticon.com/free-icons/project-management).

[![Gitter](https://badges.gitter.im/Zametek-ProjectPlan/Lobby.svg)](https://gitter.im/Zametek-ProjectPlan/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
