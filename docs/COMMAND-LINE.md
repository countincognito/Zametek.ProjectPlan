# Command line tool (zpp)

The repository includes a command line tool, `zpp` (the project `Zametek.ProjectPlan.CommandLine`). It has no user interface. `zpp` opens or imports a plan, compiles it, and makes any combination of outputs without the desktop application. You can use it for scripts, CI pipelines and batch processing.

`zpp` can also operate as a server. The server stays warm from one job to the next. `zpp` can send its runs to a server (refer to [zpp as a server](#zpp-as-a-server)).

To build `zpp`, use the standard SDK commands in [Build from source](BUILDING.md). To make a self-contained, single-file build, use `make publish-cli`.

## Usage

Compile a plan and print its metrics:

```
zpp -i plan.zpp
```

Make chart and graph images. A size is `<width>:<height>` in pixels:

```
zpp -i plan.zpp --gantt-directory out --gantt-size 1200:800 --gantt-format png --arrow-directory out --arrow-format svg --scenario-chart-directory out --scenario-chart-size 1200:800
```

To work with scenarios, use these commands. The first command lists the scenarios of a plan. The second command selects a scenario and saves the plan to a new file:

```
zpp -i plan.zpp --list-scenarios
zpp -i plan.zpp --scenario "Iteration 2" -o plan-iter2.zpp
```

The option `--scenario` accepts a name or an id. The id is the full id that `--list-scenarios` shows, or a unique prefix of it, as in Git. The prefix must have a minimum of four hexadecimal characters. An exact name match has priority over an id prefix. This is the same as in Git, where a ref has priority over an abbreviated commit hash.

The option `--list-scenarios` prints a table of the scenarios of the plan. This is the table for the sample plan [two-scenarios.zpp](assets/two-scenarios.zpp):

```
| Scenario | Id                                   | Tracked | Current |
|----------|--------------------------------------|---------|---------|
| Alpha    | 8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5 | Yes     | *       |
| Beta     | 17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4 |         |         |
```

A scenario in a folder, or under another scenario, has a name that shows its location: `Folder/Scenario`.

The column **Tracked** shows that the scenario is on the scenario chart. The chart plots the metrics of the scenario as a point, against the metrics of the axes of the chart. The option `--scenario-chart-directory` writes the chart. Tracking does only this. It does not change how the scenario compiles, what its metrics are, or what any other output holds. It is not the progress tracking of activities.

The column **Current** (`*`) marks the current scenario of the plan. This is the scenario that was open when a user saved the plan. `zpp` loads it when `--scenario` does not name another scenario.

To get metrics that a program can read, use JSON. The standard output has only the JSON document, and thus you can send it through a pipe:

```
zpp -i plan.zpp --metrics-format json
```

Import a plan from Microsoft Project or Excel, and convert it to a plan file:

```
zpp -m plan.mpp -o plan.zpp
```

For the full list of options, enter `zpp --help`. For the options of the server, enter `zpp serve --help`. The exports of charts and graphs use the display settings that the plan file holds. The theme is an exception: use `--base-theme Dark` for dark output.

`zpp` writes diagnostic messages to the standard error stream, and never to the standard output. Warnings and errors always show. The option `--verbose` adds informational messages about the lifecycle.

`zpp` and `zpp serve` read their options strictly. They refuse an invalid option with exit code 2, and they do not ignore it. These are the invalid options:

- An option that needs a value, but has none
- A value for a switch such as `--verbose`
- A size with a value in an invalid format
- An option that you give more than once. The exception is `--listen`, which listens on each address that you give
- A word that is not an option and is not the value of an option.

A value comes after its option, or after `=`. This is the same for the long and the short form of an option. These four forms are the same: `--output plan.zpp`, `--output=plan.zpp`, `-o plan.zpp` and `-o=plan.zpp`. A value that starts with `-` goes after `=`, for example `--scenario=-draft` or `-s=-draft`. Without `=`, `zpp` reads the value as an option. A negative number is an exception, and it does not need `=`.

`zpp` writes text in these places: the standard output (not the `--help` text), the saved plan files, and the GraphML and Dot exports. In all of them, each line ends with `\n` on every platform. Thus, the line endings are the same on all operating systems. The desktop application saves plan files and exports GraphML and Dot in the same way.

A watchdog controls each compilation. The option `--compile-timeout` gives the watchdog a budget in milliseconds. The default is 5000. If a compilation uses more time than the budget, the watchdog cancels it, and `zpp` exits with code 4.

Large plans can need more time. For a batch run that you must not interrupt, increase the budget, or use `--compile-timeout 0` to disable the limit. The desktop application uses the same budget. It reads the value from `CompilationTimeoutMilliseconds` in its settings file.

A run takes its times from the clock:

- When `zpp` saves a plan, it marks the scenario that it loaded as modified at this time.
- An import creates its plan at this time. It also takes the current date as the "today" date of the plan, unless the file says otherwise.
- An Excel export records the time when `zpp` writes it.

The option `--now` gives the run a time of its own. It includes the offset from UTC, for example `--now 2026-09-27T12:00:00+01:00`, or `Z` for UTC itself. Thus, the same command writes the same files at any time. An import is the exception: it gives new ids to the plan that it creates, each time. In all cases, `zpp` shows the times in the time zone of the computer. Thus, two computers write the same times only if they have the same time zone.

## Exit codes

The exit codes are a contract for scripts and CI gates. The test suite `Zametek.ProjectPlan.CommandLine.Tests` makes sure that they stay the same:

| Code | Meaning |
| ---- | ------- |
| 0 | Success |
| 1 | Runtime failure: an incorrect path, a file that `zpp` cannot read, an output that `zpp` cannot write, or an unexpected error |
| 2 | Incorrect use: invalid options or combinations of options |
| 3 | The plan compiled with errors |
| 4 | A compilation used more time than `--compile-timeout` gives, and `zpp` cancelled it |
| 5 | `zpp` sent the run to a server, and the server did not run it (refer to [Send runs to a server](#send-runs-to-a-server)) |

A chart or graph can be impossible to write, for example because another program has the file open. This does not stop the run. `zpp` writes the error to the standard error stream, and it still makes the other outputs and prints the metrics. Then the run exits with code 1.

## zpp as a server

The command `zpp serve` starts `zpp` as a web server. The server starts one time, and then it takes one job after another. Thus, its jobs do not pay the start-up cost of `zpp`, which is most of the time of a one-shot run. A plan that takes a new `zpp` process roughly 750 milliseconds takes a warm server about 25 milliseconds.

Each job operates exactly as the same run of `zpp` on the local computer. The server answers with the metrics of the plan and the files of a local run, byte for byte. If the request asks for it, the server also answers with the exit code and the text of a local run.

```
zpp serve
```

By default, the server listens on `http://localhost:9770`. Only the computer of the server can connect to this address. When the server starts, it does a warm-up with a sample plan that it contains. The warm-up takes some seconds. After the warm-up, `/health/ready` answers 200. The server also processes jobs that arrive before the end of the warm-up, but more slowly.

`zpp` can act as a client of a server and send its runs to it with the option `--server`. Each program that speaks HTTP can also send plan data to the server (refer to [The API](#the-api)).

For a step-by-step guide, refer to the [Client-server quick start](SERVER.md). It shows `zpp serve` on Windows with HTTPS or on a Unix domain socket. It also shows `zpp` when it sends its runs to the server from a second window. It has sample plans to download.

### Send runs to a server

The option `--server` makes `zpp` a client of `zpp serve`. `zpp` sends the run to a server and does not do it in its own process. The command and the options are the same, and the run prints the same text, writes the same files and gives the same exit code:

```
zpp -i plan.zpp --gantt-directory out --gantt-size 1200:800 --server http://localhost:9770
```

`zpp` sends the plan and its options, without the paths. The server does the job and answers with the text and the files of a local run. `zpp` prints and writes them on the local computer. It writes each file where its options say, with the name that `zpp` gives it.

`zpp` itself still starts, but its engine does not. On Windows, a run took 700 milliseconds on the local computer and 250 milliseconds on a warm server. A run with five exports took 350 milliseconds on the server, and 1400 milliseconds on the local computer.

To use a server by default, set `ZPP_SERVER` and do not use the option `--server`. Then each run goes to that server. The option `--local` overrides this and does the run on the local computer, also when `ZPP_SERVER` has a value. An MS Project import must run on the local computer, because a server imports Excel workbooks only. Thus, `zpp` refuses to send a file of this type.

- The value of `--server` is the `http` or `https` address of the server. The address can have a path, if a reverse proxy puts the server under a path. The value can also be `unix:` and the path of the socket of the server: `--server unix:/tmp/zpp.sock`, or on Windows `--server unix:C:\tmp\zpp.sock` (refer to [On a Unix domain socket](#on-a-unix-domain-socket)). For HTTPS, `zpp` trusts the certificates that this computer trusts.
- If a server needs an API key, `zpp` gets the key from `ZPP_API_KEY` or from the file that `--api-key-file` gives. `zpp` never gets the key directly from the command line.
- If a server is busy, `zpp` tries again when the server says to. The delay increases each time, with a small random change, and `zpp` tries again for up to two minutes. If a server stops a job at its time limit, `zpp` does not try again, because the job reaches the time limit again.
- The checks of `zpp` itself come first. A usage error, a missing plan or a missing directory fails in the same way as without a server. An export to a file that `zpp` does not write also fails in the same way. In these cases, `zpp` sends nothing to the server.
- If a server does not run the job, the run ends with exit code 5. `zpp` writes the reason to the standard error stream, with the id of the request. The log of the server uses the same id.
- A server does not run the job in these cases. `zpp` cannot connect to the server. The server needs its key. The server refuses the request, because the request is invalid or beyond its limits. The server stays busy. The server stops the job at its time limit.
- If a server runs the job, and the job fails, the run ends in the same way as the same run on the local computer. `zpp` writes the same text to the standard error stream, for each status of the answer. For example, the plan does not compile, `zpp` cannot read a file, or `zpp` cannot select a scenario.
- The compile timeout goes with the run. It is 5000 milliseconds, unless `--compile-timeout` gives another value. It must be within the limit of the server (refer to [Limits](#limits)). The server refuses `0`, which disables the watchdog on the local computer, and it refuses a timeout above the limit.
- The text and the files come from the server. Thus, numbers and dates use the culture and time zone of the server. The run takes its times from the clock of the server, unless `--now` gives a time (refer to [Culture, time zone and logs](#culture-time-zone-and-logs)). The log of `zpp` stays in the log of the server. The option `-v` shows where the run went.

### The API

To compile a plan, send a `multipart/form-data` POST to `/v1/projects/compile`. Send the plan as a file in a part named `project`, in the same way as `--input` takes it:

```
curl -F project=@plan.zpp http://localhost:9770/v1/projects/compile
```

The answer is JSON. It contains the metrics of the plan and the outputs that the request asked for. The request names the outputs in its options. The options are JSON in a part named `options`. They are the options of `zpp`, with the same names, values and defaults, but without the paths.

`zpp` writes an output to the file or directory that it receives. The request asks for the output instead, and the answer carries it in base64:

```
curl -F project=@plan.zpp \
     -F 'options={"outputs":{"ganttChart":{"format":"png","width":1600,"height":900},"arrowGraph":{"format":"svg"}}}' \
     http://localhost:9770/v1/projects/compile
```

To get the files, request a zip file. It contains each file with the name that `zpp` gives it. It also contains `result.json`, which is the answer without the contents of the files:

```
curl -F project=@plan.zpp \
     -F 'options={"outputs":{"ganttChart":{"format":"png","width":1600,"height":900},"arrowGraph":{"format":"svg"}}}' \
     -H 'Accept: application/zip' -o plan.zip \
     http://localhost:9770/v1/projects/compile
```

In Windows PowerShell, `curl` is another command. Use `curl.exe`, and put the options in a file: `-F "options=@options.json;type=application/json"`. This prevents problems with the quotation marks of JSON in the shell.

To import a workbook, send it in a part named `import`, in the same way as `--import` takes it. The server imports Excel workbooks only. `/v1/projects/scenarios` lists the scenarios of a plan, as `--list-scenarios` does, and gives them as JSON:

```
curl -F project=@plan.zpp http://localhost:9770/v1/projects/scenarios
```

When the server cannot answer a request, it sends a problem in `application/problem+json`. The status shows the reason:

- `422` for a plan that does not compile. The problem lists the errors.
- `422` also for a request that is not valid. The problem lists each problem of the request.
- `500` when the server fails.
- `503`, with `Retry-After`, when the server is busy.

`curl --fail-with-body` makes the command fail in these cases, and thus you can use it as a gate in a build. To get the text that a local run prints, and its exit code, add `?include=console` to the request. The server gives this with a problem and with an answer. Each response has the header `Request-Id`. The log of the server names the request with the same id.

| Endpoint | Description |
| -------- | ----------- |
| `POST /v1/projects/compile` | This endpoint compiles a plan. It answers with the metrics and the outputs that the request asks for. |
| `POST /v1/projects/scenarios` | This endpoint lists the scenarios of a plan. |
| `GET /v1/info` | This endpoint gives the version of the server, the culture and time zone for its jobs, and its limits. |
| `GET /v1/openapi` | This endpoint gives the description of this API (OpenAPI 3.1, in YAML). It needs no API key. |
| `GET /health/live` | This endpoint answers 200 when the server listens. |
| `GET /health/ready` | This endpoint answers 200 after the warm-up of the server, and 503 before it. |

[The zpp serve API](API.md) is the reference. It lists each option, answer and problem, and it has the security review and the changelog. The file [openapi.yaml](openapi.yaml) describes the API for programs (OpenAPI 3.1). The server supplies this file at `/v1/openapi`.

### Limits

| Limit | Default | Option | Setting |
| ----- | ------- | ------ | ------- |
| Jobs at once | The number of processors | `--max-jobs` | `MaxJobs` |
| Jobs that wait for another job to finish | Twice the jobs at once | `--max-queue` | `MaxQueue` |
| Largest request, in MB | 50 | `--max-upload` | `MaxUploadMegabytes` |
| Largest chart, in pixels | 5000 x 5000 | `--max-chart-size <width>:<height>` | `MaxChartWidth`, `MaxChartHeight` |
| Time of a job, in seconds | 120 | `--job-timeout` | `JobTimeoutSeconds` |
| Compile timeout of a job, in milliseconds | 60000 | `--max-compile-timeout` | `MaxCompileTimeoutMilliseconds` |

Each limit has a default. You can change a limit in three ways:

- With its setting in `zpp-serve.json`, which is in the same folder as `zpp`
- With an environment variable that has the name of the setting after `ZPP_` (`ZPP_MaxJobs=4`)
- With its option.

Each way overrides the one before it. The server reads the file one time, when it starts:

```
{
  "MaxJobs": 4,
  "JobTimeoutSeconds": 300
}
```

`GET /v1/info` gives the limits that a server uses. It gives times as ISO 8601 durations (`"jobTimeout": "PT2M"`). The server checks the time of a job between the steps of the job. Thus, a step that is in progress, such as a compile or the drawing of a chart, ends first. A job cannot disable its compile timeout, but `zpp` can.

### Other computers

```
zpp serve --listen https://0.0.0.0:9771 --certificate server.pfx --api-key-file /etc/zpp/api-key
```

- The option `--listen` takes `http` or `https`, then `localhost`, an IP address, or `*` for each address of the computer, and then a port. It also takes `unix:` and the path of a Unix domain socket (refer to [On a Unix domain socket](#on-a-unix-domain-socket)). To listen on more than one address, give the option more than once.
- An address that other computers can connect to needs an API key. The key comes from the file that `--api-key-file` gives, or from `ZPP_API_KEY`. The key never comes from the command line, because other users of the computer can see the command line. Without a key, the server refuses to start.
- A key has a minimum of 32 characters. The server checks this when it starts. A random 256-bit key in base64 has 44 characters, and `openssl rand -base64 32` makes such a key.
- Each request to `/v1` must then have the key in the header `Authorization: Bearer <key>`. `curl` can read this header from a file with `-H @key-header.txt`. Thus, the key is not on a command line, where other users of the computer can read it.
- The health endpoints need no key, and thus a load balancer can ask them. The description of the API also needs no key, because a tool reads it before it has the key.
- An `https` address needs `--certificate`. The value is a `.pfx` or `.p12` file, or a `.pem` or `.crt` file. If a `.pfx` or `.p12` file has a password, put the password in `ZPP_CERTIFICATE_PASSWORD`. If the key of a `.pem` or `.crt` file is in a different file, give that file with `--certificate-key`. The server accepts TLS 1.2 and TLS 1.3, and no older version.
- Plain `http` that other computers can connect to sends the API key and each plan in clear text. Thus, the server refuses to start with such an address.
- The option `--behind-tls-proxy` is an exception. It says that a proxy in front of the server ends TLS, and that only the proxy can connect to the address: `zpp serve --listen http://10.0.0.5:9770 --api-key-file /etc/zpp/api-key --behind-tls-proxy`. The server then writes a warning to its log each time it starts.
- The server refuses `--behind-tls-proxy` without such an address. It also refuses plain HTTP together with an HTTPS address that other computers can connect to. A client can use the address that is not protected.

### On a Unix domain socket

```
zpp serve --listen unix:/tmp/zpp.sock
```

A Unix domain socket is a file. Programs on this computer connect to it, in the same way as they connect to a port. The address of a socket is `unix:` and its path. Use this address for `--listen` and for `zpp --server`, which sends its run through the socket. If you give a server only sockets, it listens on them and not on `localhost:9770`. To listen on both, give `--listen` a port also.

- On Linux and macOS, the address is `unix:/tmp/zpp.sock` or, as Docker writes it, `unix:///tmp/zpp.sock`. On Windows, the path is a Windows path, `unix:C:\tmp\zpp.sock`. You can also write it with `/`, or as a file URI, `unix:///C:/tmp/zpp.sock`. A relative path starts at the folder where you enter the command. The full path can have a maximum of about 100 characters, which is the maximum that the system allows for a socket. `zpp serve` refuses a longer path (exit code 2).
- Only the user of the server can connect to the socket. `zpp serve` gives the socket to that user alone, also when the folder allows more. It does this before the server takes a connection. On Windows, the socket has an access list with only that user in it, and nothing inherited. Otherwise, a socket has the access of its folder, and in a folder such as `C:\tmp` that is every signed-in user. On Linux and macOS, the mode of the socket is 600.
- A server cannot always do this. The rules of the folder can prevent a change to the access of the socket. Then the server does not start (exit code 1), and it does not listen to everybody. Thus, a server that listens only on sockets needs no API key and no certificate. Other clients need an `http` or `https` address, and a key.
- Each program that speaks HTTP over a socket can use the socket. Example: `curl --unix-socket /tmp/zpp.sock -F project=@plan.zpp http://localhost/v1/projects/compile`. In Windows PowerShell, use `curl.exe`. The server does not use the host name in the URL. The log shows the socket in the form of the web server: `Now listening on: http://unix:/tmp/zpp.sock`. This is not an address for `--listen` or `--server`. For them, use `unix:` and the path.
- The folder for the socket must exist. If it does not exist, the server does not start (exit code 2).
- A server that stops removes its socket. A server that a program kills leaves its socket behind. The next server removes the old socket when it starts, if no process listens on it, and writes a message about this to its log.
- A server does not remove a socket in three cases, and it exits in each case. First, another server listens on the socket (exit code 1, the same as for a port that another process uses). Second, `zpp serve` has no permission to connect to the socket to see if a server listens on it (exit code 1, with the reason). Third, the path holds something that is not a socket, for example a folder or a file (exit code 2).
- On Linux and macOS, `zpp serve` cannot tell an empty file from a socket, and thus it takes an empty file for a socket.

### Culture, time zone and logs

Jobs write numbers and dates in the culture of the computer, and times in its time zone, as `zpp` does. The option `--culture` sets the culture for all jobs, for example `--culture en-GB`. On Linux and macOS, the variable `TZ` sets the time zone, for example `TZ=Europe/London zpp serve`. A job cannot select the culture or the time zone. `/v1/info` shows both. It gives the time zone by its IANA name, also when the system uses a different name.

The server writes its log to the standard error stream. The log has these items:

- The start and the warm-up of the server
- A line for each request. The line shows the status, the exit code and the time that the request took. If the server refused the request, the line shows the reason, the method and the path, but never the key
- The warnings and errors of the jobs and of the web server.

The option `-v` adds their informational messages.

Each line that belongs to a request begins with the id of the request. This id is the `Request-Id` of the response and the `traceId` of the problem. Thus, you can find a request in the log from its answer. You can also find it from the trace in which it came: a request that has a `traceparent` keeps its trace id. The option `--log-format json` writes each line as a JSON object, for a program to read (refer to [The log](API.md#the-log)).

### Stop the server

To stop the server, press Ctrl+C, or send SIGTERM from the program that started it. The server then takes no more requests. It gives the jobs that it processes time to end, up to their time limit. Then it exits with code 0. The server exits with code 2 when it cannot use its options or settings. It exits with code 1 when it cannot start, for example when another process uses its port or another server listens on its socket.

Docker gives a container only 10 seconds to stop before Docker kills it. Thus, give `docker stop` a minimum of the job time limit: `docker stop -t 120`, or `stop_grace_period` in Compose.
