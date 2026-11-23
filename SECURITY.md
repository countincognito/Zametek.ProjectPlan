# Security

## Report a vulnerability

Do not report a security problem in a public issue. Use the private vulnerability reporting of GitHub:

1. Open the tab **Security** of this repository.
2. Select **Report a vulnerability**.
3. Write the report, and send it.

Only the maintainer can read the report. Give the version of the application or of `zpp`, the steps that show the problem, and what an attacker can do with it.

## Versions that get fixes

Only the newest release gets fixes for security problems. A fix goes into a new release.

## Scope

The parts that are most important for security are these:

- `zpp serve`, the HTTP server of the command line tool. Its API has a [security review](docs/API.md#security-review), which each release repeats.
- The read of the files that the application and `zpp` get: project files, workbooks and the files of other project tools.
- The packages and the installers of a release.
