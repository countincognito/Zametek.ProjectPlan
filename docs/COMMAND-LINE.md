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

zpp and `zpp serve` read their options strictly: an option that takes a value but is not given one, a value given to a switch such as `--verbose`, more values than a size takes, and a word that is neither an option nor an option's value are each refused with exit code 2, rather than ignored. A value that starts with `-`, other than a negative number, goes after `=` - `--scenario=-draft` - since on its own it would be read as an option.

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

`zpp serve` runs zpp as a web server. It starts once and then takes one job after another, so its jobs do not pay zpp's start-up, which is most of a one-shot run: a plan that takes a new `zpp` process three-quarters of a second takes a warm server about 25 milliseconds. Each job runs exactly as zpp would run it, and the server answers with the code zpp would have exited with, what it would have printed, and the files it would have written, byte for byte.

```
zpp serve
```

It listens on `http://localhost:9770`, which only this machine can reach. As it starts, it warms up on a sample plan it carries, which takes a few seconds, and `/health/ready` answers 200 once it has. Jobs sent before then still run, only more slowly. zpp sends its runs to it with `--server`, and anything that speaks HTTP can send it jobs.

For a start step by step - `zpp serve` over https on Windows, with zpp sending its runs to it from a second window, and sample plans to download - see the [client-server quick start](SERVER.md).

### Running zpp on it

`--server` sends a run to a server rather than running it in this process - the same command, with the same options, which prints the same text, writes the same files and exits with the same code:

```
zpp -i plan.zpp --gantt-directory out --gantt-size 1200:800 --server http://localhost:9770
```

zpp sends the plan and its options, less the paths, and the server runs the job and answers with what zpp would have printed and written, which zpp prints and writes here - each file where its options say, named as zpp names it. zpp itself still starts up, but its engine does not: on Windows, a run that took 0.7 seconds here took a quarter of a second on a warm server, and one with five exports took a third of a second rather than 1.4. Set `ZPP_SERVER` instead of giving `--server`, and every run goes to that server; `--local` runs one here regardless. An MS Project import has to run here: a server imports Excel workbooks only, so zpp refuses to send one.

- The server is its `http` or `https` address - with a path, if a reverse proxy puts it under one - or `unix:` and the path of its socket (`--server unix:/tmp/zpp.sock`, or `unix:///tmp/zpp.sock` as Docker writes it). https trusts the certificates this machine trusts.
- A server that needs its API key gets it from `ZPP_API_KEY`, or from the file `--api-key-file` names - never from the command line.
- A server that is busy is tried again when it says to, for up to two minutes.
- zpp's own checks come first: a usage error, a plan or a directory that is not there, or an export to a file zpp does not write fails as it would without a server, and nothing is sent.
- A server that does not run the job - it cannot be reached, wants its key, refuses the job as beyond its limits, stays busy, or stops the job at its time limit - ends the run with exit code 5, and zpp says why on stderr.
- The compile timeout goes with the run - 5000 milliseconds, unless `--compile-timeout` says otherwise - and must be within the server's limit (see [Limits](#limits)): `0`, which switches the watchdog off here, is refused, as is a timeout above the limit.
- The text and the files are the server's, so numbers and dates come out in its culture and time zone, and the run takes its times from the server's clock unless `--now` gives it one (see [Culture, time zone and logs](#culture-time-zone-and-logs)). zpp's log stays in the server's log; `-v` says where the run went.

### Sending a job

A job is a `multipart/form-data` POST to `/v1/jobs`, with the plan as a file named `input`, as `--input` takes it:

```
curl -F input=@plan.zpp http://localhost:9770/v1/jobs
```

The answer is JSON:

```
{
  "jobId": "880c768fa78e4686aab361ddb9e338a4",
  "exitCode": 0,
  "stdout": "\n| Metrics                 | Values      |\n|-------------------------|-------------|\n...",
  "stderr": "",
  "metrics": { "ActivityRisk": 1.0, ... },
  "outputs": [],
  "transcript": [ { "kind": "display", "text": "| Metrics                 | Values      |\n..." } ]
}
```

- `exitCode` is the code zpp would have exited with (see [Exit codes](#exit-codes)), and `stdout` and `stderr` are what it would have printed - written by the server, in the server's culture. zpp's log is in neither: the server keeps its own. `curl -s ... | jq -j .stdout` prints the text exactly as zpp would.
- `metrics` holds the metrics as `--metrics-format json` writes them, whichever format the job asked for, or `null` when the job ended before it had any - when the plan did not compile, say.
- `outputs` lists each file the job produced, in the order zpp produces them: what it is (`kind`), the name zpp would give the file (`fileName`), its media type (`contentType`), and its `content` in base64.
- `transcript` records the job call by call, in the order it ran: each `line` it printed, each `display`ed block (with `hasErrors` when the block reports errors, which zpp shows in red), each `errorLine` on stderr, and each `output` as it was produced (its `index` in `outputs`). Played back in order, it prints and writes everything when zpp would have, and shows each block as zpp shows it.

The job's options go in a part named `options`, as JSON. They are zpp's own options, with its names, values and defaults, less the paths: where zpp writes an output to the file or directory you give it, here you ask for the output, and the answer carries it.

```
curl -F input=@plan.zpp \
     -F 'options={"metricsFormat":"json","gantt":{"format":"png","width":1600,"height":900},"arrow":{"format":"svg"}}' \
     http://localhost:9770/v1/jobs
```

| Option | zpp's | Value |
| ------ | ----- | ----- |
| `scenario` | `--scenario` | The scenario to load, by name or id (only with `input`) |
| `output` | `--output` | `true` to return the project, as zpp saves it |
| `export` | `--export` | `true` to return the scenario, as zpp exports it to Excel |
| `baseTheme` | `--base-theme` | `light` (the default) or `dark` |
| `metricsFormat` | `--metrics-format` | `markdown` (the default), `table` or `json` |
| `compileTimeout` | `--compile-timeout` | Milliseconds, from 1 to the server's limit - zpp's 5000 by default, or the limit if it is lower |
| `now` | `--now` | A time with its offset from UTC, such as `2026-09-27T12:00:00+01:00` |
| `gantt`, `resource`, `ev`, `scenarioChart` | `--gantt-*`, `--resource-*`, `--ev-*`, `--scenario-chart-*` | A chart: `{"format": "png", "width": 1600, "height": 900}`, where `format` is `jpeg` (the default), `png`, `bmp`, `webp` or `svg`, and the size is required |
| `arrow`, `vertex` | `--arrow-*`, `--vertex-*` | A graph: `{"format": "svg"}`, where `format` is `jpeg` (the default), `png`, `pdf`, `svg`, `graphml` or `dot` |

Names and values are read whatever their case, but otherwise strictly: an option the server does not know, or a number in quotes, is refused rather than ignored. The options can also come from a file - `-F options=@job.json` - which saves quoting JSON for the shell. (In Windows PowerShell, where `curl` names another command, call `curl.exe`.)

To get the files themselves, ask for a zip:

```
curl -F input=@plan.zpp \
     -F 'options={"gantt":{"format":"png","width":1600,"height":900},"arrow":{"format":"svg"}}' \
     -H 'Accept: application/zip' -o plan.zip \
     http://localhost:9770/v1/jobs
```

`plan.zip` holds `plan-gantt.png` and `plan-arrow.svg`, named as zpp names them, and `result.json`, which is the JSON answer less the files' contents.

A plan sent as `import` is imported, as `--import` imports it - from Excel only: import MS Project files with zpp itself. `/v1/scenarios` lists a project's scenarios, as `--list-scenarios` does, and gives them as JSON too:

```
curl -F import=@plan.xlsx -F 'options={"output":true}' -H 'Accept: application/zip' -o plan.zip http://localhost:9770/v1/jobs
curl -F input=@plan.zpp http://localhost:9770/v1/scenarios
```

### Endpoints and answers

| Endpoint | Does |
| -------- | ---- |
| `POST /v1/jobs` | Runs a job |
| `POST /v1/scenarios` | Lists a project's scenarios |
| `GET /v1/info` | Gives the server's version, the culture and time zone its jobs write in, and its limits |
| `GET /health/live` | Answers 200 once the server is listening |
| `GET /health/ready` | Answers 200 once it has warmed up, and 503 until then |

A job that runs is answered with 200, however it ends: a plan that does not compile, say, gets exit code 3, with its errors in `stdout` as zpp prints them. The `Zpp-Job-Id` header carries the job's id, which the server's log names it by. A request the server will not run gets problem details (`application/problem+json`), which say why in `detail`:

| Status | When |
| ------ | ---- |
| 400 | zpp would refuse the job as a usage error, or its options are not valid JSON, or they go beyond the server's limits |
| 401 | The request does not carry the server's API key (see below) |
| 413 | The request is larger than the server accepts |
| 415 | The request is not `multipart/form-data`, or the plan to import is not an Excel workbook |
| 503 | The server is running all the jobs it can, with as many waiting as it allows - `Retry-After` says when to try again |
| 504 | The job ran for longer than the server allows, and was stopped |

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

A job's time is checked between its steps, so a step already under way - a compile, or a chart being drawn - finishes first. Unlike zpp, a job cannot switch its compile timeout off.

### Beyond this machine

```
zpp serve --listen http://0.0.0.0:9770 --api-key-file /etc/zpp/api-key
```

- `--listen` takes `http` or `https`, then `localhost`, an IP address or `*` for every address the machine has, then a port. Give it more than once to listen on several.
- An address other machines can reach needs an API key - from the file `--api-key-file` names, or from `ZPP_API_KEY`, never from the command line, which other users of the machine can see - and without one the server refuses to start. Requests to `/v1` must then carry the key: `curl -H "Authorization: Bearer $ZPP_API_KEY" ...`. The health endpoints need no key, so that a load balancer can ask them.
- An `https` address needs `--certificate`: a `.pfx` or `.p12` file, with its password - if it has one - in `ZPP_CERTIFICATE_PASSWORD`, or a `.pem` or `.crt` file, with `--certificate-key` if its key is in a file of its own.
- `--unix-socket /tmp/zpp.sock` listens on a Unix domain socket instead of `localhost:9770`. Only the user running the server can connect to it (on Windows, the socket has its folder's access): `curl --unix-socket /tmp/zpp.sock -F input=@plan.zpp http://localhost/v1/jobs`.

### Culture, time zone and logs

Jobs write numbers and dates in the machine's culture, and times in its time zone, as zpp does. `--culture` sets the culture for all of them (`--culture en-GB`); on Linux and macOS, `TZ` sets the time zone (`TZ=Europe/London zpp serve`). A job cannot choose either, and `/v1/info` says which they are.

The log goes to stderr: starting, warming up, a line for each job with its id and its exit code - or why it was refused - and the warnings and errors of the jobs and of the web server. `-v` adds their informational output.

### Stopping it

Ctrl+C, or SIGTERM from whatever started it, stops the server: it takes no more requests, gives the jobs it is running up to their time limit to finish, and exits with code 0. It exits with code 2 when its options or settings cannot be used, and with code 1 when it cannot start - when its port is taken, say. Docker gives a container only 10 seconds to stop before it kills it, so give `docker stop` at least the job time limit (`docker stop -t 120`, or `stop_grace_period` in Compose).
