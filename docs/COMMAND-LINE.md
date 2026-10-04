# Command line tool (zpp)

The solution also ships a headless command line tool, `zpp` (the `Zametek.ProjectPlan.CommandLine` project), which opens or imports a project, compiles it, and produces any combination of outputs without launching the desktop app - useful for scripting, CI pipelines, and batch processing. It can also run as a server, which stays warm from one job to the next, and send its runs to one (see [Running zpp as a server](#running-zpp-as-a-server)). Build it with the standard SDK commands in [Building from source](BUILDING.md), or produce a self-contained single-file build with `make publish-cli`.

## Usage

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

Run `zpp --help` for the full option list, and `zpp serve --help` for the server's. Chart and graph exports honour the display settings saved in the project file (the theme excepted - pass `--base-theme Dark` for dark output). Diagnostic logging goes to stderr, never stdout: warnings and errors always show, and `--verbose` adds informational lifecycle output.

zpp and `zpp serve` read their options strictly: an option that takes a value but is not given one, a value given to a switch such as `--verbose`, more values than a size takes, an option given more than once - but `--listen`, which listens on each address it is given - and a word that is neither an option nor an option's value are each refused with exit code 2, rather than ignored. A value comes after its option, or after `=`, whether the option is written long or short: `--output plan.zpp`, `--output=plan.zpp`, `-o plan.zpp` and `-o=plan.zpp` are the same. A value that starts with `-`, other than a negative number, goes after `=` - `--scenario=-draft` or `-s=-draft` - since on its own it would be read as an option.

Everything zpp writes as text - stdout (except the `--help` text), saved project files, and GraphML and Dot exports - ends its lines with `\n` on every platform, so line endings never differ between operating systems. The desktop application saves project files and exports GraphML and Dot the same way.

Every compilation runs under a watchdog: `--compile-timeout` gives it a budget in milliseconds (5000 by default), and a compilation that runs past it is cancelled and exits with code 4. Large plans can legitimately need longer, so raise it - or pass `--compile-timeout 0` to switch the limit off entirely - for a batch run that must not be interrupted. The desktop application applies the same budget, read from `CompilationTimeoutMilliseconds` in its settings file.

A run takes its times from the clock: saving a project marks the scenario it loaded as modified now, an import creates its project now and - unless the file says otherwise - takes today as the plan's today, and an Excel export records when it was written. `--now` gives the run a time of its own instead, with its offset from UTC (`--now 2026-09-27T12:00:00+01:00`, or `Z` for UTC itself), so the same command writes the same files whenever it is run. The one exception is an import, which gives the project it creates new ids every time. Either way the times are shown in the machine's time zone, so two machines write the same times only if they share one.

## Exit codes

The exit codes are a contract for scripts and CI gates, pinned by the `Zametek.ProjectPlan.CommandLine.Tests` suite:

| Code | Meaning |
| ---- | ------- |
| 0 | Success |
| 1 | Runtime failure (bad paths, unreadable files, an output that could not be written, unexpected errors) |
| 2 | Bad usage (invalid options or combinations) |
| 3 | The project compiled with errors |
| 4 | A compilation ran past `--compile-timeout` and was cancelled |
| 5 | The run was sent to a server, which did not run it (see [Running zpp on it](#running-zpp-on-it)) |

A chart or graph that cannot be written - because the file is open in another program, say - does not stop the run: the error goes to stderr, the remaining outputs are still produced and the metrics still printed, and the run then exits with code 1.

## Running zpp as a server

`zpp serve` runs zpp as a web server. It starts once and then takes one job after another, so its jobs do not pay zpp's start-up, which is most of a one-shot run: a plan that takes a new `zpp` process three-quarters of a second takes a warm server about 25 milliseconds. Each job runs exactly as zpp would run it, and the server answers with the project's metrics and the files it would have written, byte for byte - and, when it is asked to, the code zpp would have exited with and what it would have printed.

```
zpp serve
```

It listens on `http://localhost:9770`, which only this machine can reach. As it starts, it warms up on a sample plan it carries, which takes a few seconds, and `/health/ready` answers 200 once it has. Jobs sent before then still run, only more slowly. zpp sends its runs to it with `--server`, and anything that speaks HTTP can send it projects (see [The API](#the-api)).

For a start step by step - `zpp serve` over https on Windows, or on a Unix domain socket, with zpp sending its runs to it from a second window, and sample plans to download - see the [client-server quick start](SERVER.md).

### Running zpp on it

`--server` sends a run to a server rather than running it in this process - the same command, with the same options, which prints the same text, writes the same files and exits with the same code:

```
zpp -i plan.zpp --gantt-directory out --gantt-size 1200:800 --server http://localhost:9770
```

zpp sends the plan and its options, less the paths, and the server runs the job and answers with what zpp would have printed and written, which zpp prints and writes here - each file where its options say, named as zpp names it. zpp itself still starts up, but its engine does not: on Windows, a run that took 0.7 seconds here took a quarter of a second on a warm server, and one with five exports took a third of a second rather than 1.4. Set `ZPP_SERVER` instead of giving `--server`, and every run goes to that server; `--local` runs one here regardless. An MS Project import has to run here: a server imports Excel workbooks only, so zpp refuses to send one.

- The server is its `http` or `https` address - with a path, if a reverse proxy puts it under one - or `unix:` and the path of its socket: `--server unix:/tmp/zpp.sock`, or on Windows `--server unix:C:\tmp\zpp.sock` (see [On a Unix domain socket](#on-a-unix-domain-socket)). https trusts the certificates this machine trusts.
- A server that needs its API key gets it from `ZPP_API_KEY`, or from the file `--api-key-file` names - never from the command line.
- A server that is busy is tried again when it says to, and after longer each time, with a little at random, for up to two minutes. A server that stopped the job at its time limit is not: the job would only run out of time again.
- zpp's own checks come first: a usage error, a plan or a directory that is not there, or an export to a file zpp does not write fails as it would without a server, and nothing is sent.
- A server that does not run the job - it cannot be reached, wants its key, refuses the request as not valid or beyond its limits, stays busy, or stops the job at its time limit - ends the run with exit code 5, and zpp says why on stderr, with the id of the request, which the server's log names it by. A server that did run it, and failed - the project does not compile, a file cannot be read, a scenario cannot be selected - ends the run as the same run would have ended here, with the same text on stderr, whatever the answer's status.
- The compile timeout goes with the run - 5000 milliseconds, unless `--compile-timeout` says otherwise - and must be within the server's limit (see [Limits](#limits)): `0`, which switches the watchdog off here, is refused, as is a timeout above the limit.
- The text and the files are the server's, so numbers and dates come out in its culture and time zone, and the run takes its times from the server's clock unless `--now` gives it one (see [Culture, time zone and logs](#culture-time-zone-and-logs)). zpp's log stays in the server's log; `-v` says where the run went.

### The API

A project is sent as a `multipart/form-data` POST to `/v1/projects/compile`, as a file named `project`, as `--input` takes it:

```
curl -F project=@plan.zpp http://localhost:9770/v1/projects/compile
```

The answer is JSON: the project's metrics, and the outputs it was asked for. The request asks for them in its options, which are JSON in a part named `options` - zpp's own options, with its names, values and defaults, less the paths: where zpp writes an output to the file or directory it is given, the request asks for the output, and the answer carries it, in base64:

```
curl -F project=@plan.zpp \
     -F 'options={"outputs":{"ganttChart":{"format":"png","width":1600,"height":900},"arrowGraph":{"format":"svg"}}}' \
     http://localhost:9770/v1/projects/compile
```

To get the files themselves, ask for a zip, which holds each file under the name zpp gives it, and `result.json`, which is the answer less the files' contents:

```
curl -F project=@plan.zpp \
     -F 'options={"outputs":{"ganttChart":{"format":"png","width":1600,"height":900},"arrowGraph":{"format":"svg"}}}' \
     -H 'Accept: application/zip' -o plan.zip \
     http://localhost:9770/v1/projects/compile
```

In Windows PowerShell, where `curl` names another command, call `curl.exe`, and put the options in a file - `-F "options=@options.json;type=application/json"` - which saves quoting JSON for the shell. A workbook to import goes in a part named `import`, as `--import` takes it - from Excel only. `/v1/projects/scenarios` lists a project's scenarios, as `--list-scenarios` does, and gives them as JSON:

```
curl -F project=@plan.zpp http://localhost:9770/v1/projects/scenarios
```

What the server cannot answer is not an answer but a problem - `application/problem+json`, with the status that says why: `422` for a project that does not compile, whose errors it lists, or a request that is not valid, whose every problem it lists; `500` when the server failed; `503`, with `Retry-After`, when it is busy. `curl --fail-with-body` makes that a failed command, which makes it a gate for a build. A request asks for what zpp would have printed and exited with by adding `?include=console`, which comes with a problem as well as with an answer, and every response carries `Request-Id`, which is what the server's log names the request by.

| Endpoint | Does |
| -------- | ---- |
| `POST /v1/projects/compile` | Compiles a project, and answers with its metrics and the outputs the request asks for |
| `POST /v1/projects/scenarios` | Lists a project's scenarios |
| `GET /v1/info` | Gives the server's version, the culture and time zone its jobs write in, and its limits |
| `GET /health/live` | Answers 200 once the server is listening |
| `GET /health/ready` | Answers 200 once it has warmed up, and 503 until then |

[The zpp serve API](API.md) is the reference - every option, answer and problem, with the security review and the changelog - and [openapi.yaml](openapi.yaml) describes it for programs (OpenAPI 3.1).

### Limits

| Limit | Default | Option | Setting |
| ----- | ------- | ------ | ------- |
| Jobs at once | The number of processors | `--max-jobs` | `MaxJobs` |
| Jobs waiting for one to finish | Twice the jobs at once | `--max-queue` | `MaxQueue` |
| Largest request, in MB | 50 | `--max-upload` | `MaxUploadMegabytes` |
| Largest chart, in pixels | 5000 x 5000 | `--max-chart-size <width>:<height>` | `MaxChartWidth`, `MaxChartHeight` |
| A job's time, in seconds | 120 | `--job-timeout` | `JobTimeoutSeconds` |
| A job's compile timeout, in milliseconds | 60000 | `--max-compile-timeout` | `MaxCompileTimeoutMilliseconds` |

Each limit has its default unless it is configured: by its setting in `zpp-serve.json`, beside zpp; by an environment variable named for its setting after `ZPP_` (`ZPP_MaxJobs=4`); or by its option - each overriding the one before. The file is read once, when the server starts:

```
{
  "MaxJobs": 4,
  "JobTimeoutSeconds": 300
}
```

`GET /v1/info` gives the limits a server is running with, as ISO 8601 durations where they are times (`"jobTimeout": "PT2M"`). A job's time is checked between its steps, so a step already under way - a compile, or a chart being drawn - finishes first. Unlike zpp, a job cannot switch its compile timeout off.

### Beyond this machine

```
zpp serve --listen https://0.0.0.0:9771 --certificate server.pfx --api-key-file /etc/zpp/api-key
```

- `--listen` takes `http` or `https`, then `localhost`, an IP address or `*` for every address the machine has, then a port - or `unix:` and the path of a Unix domain socket (see [On a Unix domain socket](#on-a-unix-domain-socket)). Give it more than once to listen on several.
- An address other machines can reach needs an API key - from the file `--api-key-file` names, or from `ZPP_API_KEY`, never from the command line, which other users of the machine can see - and without one the server refuses to start. A key is at least 32 characters, which the server checks as it starts; a random 256-bit key written in base64 is 44, and `openssl rand -base64 32` makes one. Requests to `/v1` must then carry the key, as `Authorization: Bearer <key>`: `curl` reads that header from a file, `-H @key-header.txt`, so that the key is not on a command line, where the other users of the machine can read it. The health endpoints need no key, so that a load balancer can ask them.
- An `https` address needs `--certificate`: a `.pfx` or `.p12` file, with its password - if it has one - in `ZPP_CERTIFICATE_PASSWORD`, or a `.pem` or `.crt` file, with `--certificate-key` if its key is in a file of its own. It takes TLS 1.2 and 1.3, and nothing older.
- Plain `http` that other machines can reach would send the API key, and every project, in the clear, so the server refuses to start with it - unless `--behind-tls-proxy` says that a proxy in front of the server ends TLS, and that only the proxy can reach the address: `zpp serve --listen http://10.0.0.5:9770 --api-key-file /etc/zpp/api-key --behind-tls-proxy`. The server then says so in its log, as a warning, each time it starts. `--behind-tls-proxy` is refused without such an address, and so is plain http beside https that other machines can reach: a client could use the one that is not protected.

### On a Unix domain socket

```
zpp serve --listen unix:/tmp/zpp.sock
```

A Unix domain socket is a file that programs on this machine connect to, as they would to a port. Its address is `unix:` and its path, for `--listen` and for `zpp --server`, which sends its run over it. A server that is given sockets alone listens on them instead of `localhost:9770`; give `--listen` a port as well to listen on both.

- On Linux and macOS the address is `unix:/tmp/zpp.sock` or, as Docker writes it, `unix:///tmp/zpp.sock`. On Windows the path is a Windows path, `unix:C:\tmp\zpp.sock`, which may also be written with `/`, or as a file URI, `unix:///C:/tmp/zpp.sock`. A relative path is taken from the folder the command runs in. The path, in full, can be about a hundred characters at most, which is as much as the system allows a socket: a longer one is refused (exit code 2).
- Only the user running the server can connect to the socket: zpp serve leaves it to that user alone, whatever its folder allows, and does so before the server takes a connection. On Windows that is an access list with that user alone in it and nothing inherited - a socket otherwise has the access of its folder, which in a folder like `C:\tmp` is every signed-in user's - and on Linux and macOS it is mode 600. A server that cannot do so, because the folder's rules do not let it change the socket's access, does not start (exit code 1), rather than listen to everybody. So a server that listens on sockets alone needs no API key and no certificate; anyone else needs an `http` or `https` address, and a key.
- Anything that speaks HTTP over a socket can use it: `curl --unix-socket /tmp/zpp.sock -F project=@plan.zpp http://localhost/v1/projects/compile`, or `curl.exe` in Windows PowerShell, where the host name in the URL is not used. The log gives the socket as the web server writes it, `Now listening on: http://unix:/tmp/zpp.sock`, which is not an address for `--listen` or `--server`: give them `unix:` and the path.
- The folder the socket is to be in must be there, or the server does not start (exit code 2). A server that is stopped removes its socket, but one that is killed leaves it behind; the next removes it as it starts, if nothing is listening on it, and says so in its log. A socket that another server listens on is not removed (zpp serve exits with code 1, as it does for a port that is taken), nor is one that zpp serve is not allowed to connect to, to see whether a server is listening on it (exit code 1, with the reason), nor is anything else at that path - a folder, or a file that is not a socket left behind (exit code 2). On Linux and macOS, where zpp serve cannot tell an empty file from a socket, an empty file is taken for one.

### Culture, time zone and logs

Jobs write numbers and dates in the machine's culture, and times in its time zone, as zpp does. `--culture` sets the culture for all of them (`--culture en-GB`); on Linux and macOS, `TZ` sets the time zone (`TZ=Europe/London zpp serve`). A job cannot choose either, and `/v1/info` says which they are - the time zone by its IANA name, whatever the system calls it.

The log goes to stderr: starting, warming up, a line for each request with its status, its exit code and how long it took - or why it was refused, with the method and the path, and never the key - and the warnings and errors of the jobs and of the web server. `-v` adds their informational output. Each line that belongs to a request begins with its id, which is the `Request-Id` of its response and the `traceId` of its problem, so that a request can be found in the log from what it was answered with - or from the trace it came in, as a request that carries a `traceparent` keeps its trace id. `--log-format json` writes each line as a JSON object, for a program to read (see [The log](API.md#the-log)).

### Stopping it

Ctrl+C, or SIGTERM from whatever started it, stops the server: it takes no more requests, gives the jobs it is running up to their time limit to finish, and exits with code 0. It exits with code 2 when its options or settings cannot be used, and with code 1 when it cannot start - when its port is taken, or another server listens on its socket, say. Docker gives a container only 10 seconds to stop before it kills it, so give `docker stop` at least the job time limit (`docker stop -t 120`, or `stop_grace_period` in Compose).
