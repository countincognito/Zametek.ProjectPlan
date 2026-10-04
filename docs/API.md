# The zpp serve API

`zpp serve` runs zpp as a web server (see [Running zpp as a server](COMMAND-LINE.md#running-zpp-as-a-server)). This is the reference for the HTTP API it offers: what a request is, what each answer is, and every problem it can answer with. It is written for anyone who sends it projects - with `curl`, from a script, from a program of their own - and for the maintainers, who change it only as the [changelog](#changelog) records. [`openapi.yaml`](openapi.yaml) describes the same API for programs (OpenAPI 3.1), and the server [serves it](#fetch-the-description): where the two differ, the description wins and this document has a bug. zpp itself, run as `zpp --server`, is one client of it; [what it does with each answer](#what-zpp---server-does-with-each-answer) is below. The API follows the [RESTful API Guide](RESTFUL-API-GUIDE.md) and cites its rules by their identifiers (`ASY-1`); [where it does not follow one](#deviations-from-the-guide) is recorded below.

The API has four operations, all under `/v1`:

| Operation | Does |
| --------- | ---- |
| [`POST /v1/projects/compile`](#compile-a-project) | Compiles the project in the request, and answers with its metrics and the outputs - files - that the request asks for |
| [`POST /v1/projects/scenarios`](#list-a-projects-scenarios) | Lists the scenarios of the project in the request |
| [`GET /v1/info`](#ask-what-the-server-is) | Says what the server is: its version, culture, time zone and limits |
| [`GET /v1/openapi`](#fetch-the-description) | Gives the description of this API, `openapi.yaml`, for programs - without an API key |

The server keeps nothing between requests. A project is sent with each request, and nothing of it is stored: the two POSTs are only POSTs because a project is a file, which cannot go in a URL. Sending one again does the same again, and a request can go to any server of the same version.

## Quick start

Start a server, which listens on `http://localhost:9770` and needs no API key on this machine:

```
zpp serve
```

Compile a project, and print its metrics. Windows 10 and later have `curl.exe`; in Windows PowerShell, `curl` is another command, so write `curl.exe`:

```
curl -s -F project=@plan.zpp http://localhost:9770/v1/projects/compile
```

The answer is JSON: the project's [metrics](#metrics), and the outputs it was asked for - none, here. To ask for outputs, send the options as JSON in a part named `options`. The options go in a file, which saves quoting them for the shell:

```json
{
  "outputs": {
    "project": {},
    "ganttChart": { "format": "png", "width": 1600, "height": 900 },
    "arrowGraph": { "format": "svg" }
  }
}
```

```
curl -s -F project=@plan.zpp -F "options=@options.json;type=application/json" -H "Accept: application/zip" -o plan.zip http://localhost:9770/v1/projects/compile
```

`plan.zip` holds `plan.zpp`, `plan-gantt.png` and `plan-arrow.svg` - each named as zpp names it - and `result.json`, which is the JSON answer less the files' contents. Without `Accept: application/zip` the answer is JSON, with each file in the `outputs` list as base64 in its `content`.

A project that does not compile is not an answer but a [problem](#problems): `422`, with each of the compiler's errors listed. `curl --fail-with-body` turns that into a failed command - it exits with code 22, and prints the problem - which makes it a gate for a build:

```
curl --fail-with-body -s -F project=@plan.zpp http://localhost:9770/v1/projects/compile
```

The server also serves its own description, for any tool that reads OpenAPI ([more](#fetch-the-description)):

```
curl -s -o openapi.yaml http://localhost:9770/v1/openapi
```

## Conventions

Everything below applies to every operation unless it says otherwise.

**Media types.** A request that sends a file is `multipart/form-data`. An answer is `application/json`, or - when a request to compile asks for it with `Accept: application/zip` - a zip; the description of the API is YAML. A problem is always `application/problem+json`, whatever `Accept` says. `Accept` is negotiated by quality: `*/*` and `application/*` are taken for what they are, `q=0` refuses a type, and a request whose `Accept` offers nothing the operation answers with is `406`, which says what the operation can answer with. The answer of a compile varies with `Accept`, and says so with `Vary: Accept`.

**Names and values.** JSON is compact and written in `lowerCamelCase`; names are matched exactly as they are written, case and all. An enumerated value is a string in `lowerCamelCase`, never a number - `png`, `graphml`, `markdown`. A time is RFC 3339 with its offset, a date is `2024-01-06`, a duration is ISO 8601 (`PT5S`, `PT2M`) in days, hours, minutes and seconds and nothing longer, and a metric that cannot be worked out is `null`: it is known, and has no value. A request that names a member the server does not know is refused, and says which (`422`); an answer may gain members in a later version, which a client has to ignore.

**Request ids.** Every response - answer, problem, `401`, `404`, `OPTIONS`, probe - carries `Request-Id`, 32 hexadecimal characters, which is the `traceId` of its problem and the trace id on every line of the server's log that belongs to the request: send it to whoever runs the server, who finds the request in the log by it. A request that carries a [`traceparent`](https://www.w3.org/TR/trace-context/) keeps the trace it names, and its trace id is the request's id; one that does not is given an id of its own.

**Authentication.** A server on this machine alone needs no API key; one that other machines can reach has to have one, and everything under `/v1` then needs it, as `Authorization: Bearer <key>`. A request without it, or with another, is `401` with `WWW-Authenticate: Bearer`. The health endpoints and [the description of the API](#fetch-the-description) are the only paths that need no key. A key is at least 32 characters, and a random 256-bit key written in base64 is 44: `openssl rand -base64 32` makes one. The server keeps only a hash of the key and compares it in constant time, and logs each request it refuses - its method, its path and the trace id, never the key - at warning level. A key on a command line can be read by the other users of the machine, so give `curl` the header from a file - a file with the one line `Authorization: Bearer <key>` in it - with `-H @key-header.txt`.

**Transport.** Beyond this machine the API is served over https, which takes TLS 1.2 and 1.3, or over plain http behind a proxy that ends TLS (`--behind-tls-proxy`): the server refuses to listen on plain http that other machines can reach without being told so, as it would send the key and every project in the clear. On this machine it can listen on plain http, on a Unix domain socket - which only the user running the server can connect to, and which needs no key - or on both.

**Compression.** JSON, problems and the description of the API are compressed with brotli or gzip for a client that accepts them (`Accept-Encoding`), and say so with `Vary: Accept-Encoding`. A zip is not compressed again.

**Caching.** Every response says `Cache-Control: no-store` - what the server answers is made of somebody's project - except [`/v1/info`](#ask-what-the-server-is), which can be cached for a minute by whoever asked, and [the description of the API](#fetch-the-description), which can be cached for an hour by anyone; each is validated with its `ETag`.

**Limits.** [`/v1/info`](#ask-what-the-server-is) gives the server's limits, which its operator sets (see [Limits](COMMAND-LINE.md#limits)): how large a request and a chart may be, how long a job and a compilation may take, how many jobs run at once, and how many wait. A request beyond them is a problem that says which limit.

**Culture.** The text the server writes - a problem's `title` and `detail`, the `console` - is in the server's culture, which `/v1/info` names. It is not negotiated, and nothing a program reads depends on it: a program branches on a problem's `type` and on the `code` of its `errors`.

## Compile a project

`POST /v1/projects/compile`

### Request

A `multipart/form-data` body, no larger than the server's upload limit, with these parts:

| Part | Is | Notes |
| ---- | -- | ----- |
| `project` | A file | A project (`.zpp`), as `zpp --input` takes it. The file needs a name - the outputs are named after it - and its path, if it came with one, is dropped. Exactly one of `project` and `import` |
| `import` | A file | An Excel workbook (`.xlsx`) to import into a new project, as `zpp --import` does. MS Project files are not imported by the server: import them with zpp itself |
| `options` | JSON | Optional. A file or a field - the part's own type is not insisted on, so that `curl -F 'options={...}'` works - of at most 64 KB, nested at most 16 deep |

A part the operation does not take is refused, as are a `project` or an `import` sent as a field rather than a file. A query parameter, `include`, asks for more in the answer:

| Parameter | Is | Notes |
| --------- | -- | ----- |
| `include` | A comma-separated list | `console` adds the [console](#console) to the answer. Any other value, and any other parameter, is a problem with the request: a misspelt one is not ignored |

### Options

The options are zpp's own, with zpp's defaults, less the paths: where zpp names the file or directory an output goes to, the request asks for the output, and the answer carries it. What is asked for is spelt as it is answered - each member of `outputs` is the `kind` of an output of the answer.

| Option | zpp's | Is |
| ------ | ----- | -- |
| `scenario` | `--scenario` | The scenario to load, by name - without regard to case - or by id: the whole id, or a unique prefix of it of at least four hexadecimal characters. A name that matches wins over a prefix. Only with a `project` |
| `baseTheme` | `--base-theme` | `light` (the default) or `dark`: the theme of the charts and graphs |
| `metricsFormat` | `--metrics-format` | `markdown` (the default), `table` or `json`: how the [console](#console) shows the metrics. It shapes nothing else |
| `compileTimeout` | `--compile-timeout` | An ISO 8601 duration, from `PT0.001S` to the server's limit (`maxCompileTimeout`). Without it, zpp's `PT5S`, or the limit if that is shorter. Unlike zpp, a request cannot switch the limit off |
| `now` | `--now` | A time with its offset from UTC, such as `2026-10-03T09:00:00+01:00`: used in place of the clock for what the job stamps with a time |
| `outputs` | | What the job produces: see below |

The members of `outputs` are the outputs the job can produce, in the order it produces them. An output that is given is produced; one that is not, is not. The two that have no settings of their own - yet - are given as an empty object, which can later carry settings without breaking a request.

| Member | zpp's | Produces | Settings |
| ------ | ----- | -------- | -------- |
| `project` | `--output` | The project, as zpp saves it (`<name>.zpp`) | `{}` |
| `scenarioExport` | `--export` | The loaded scenario, as zpp exports it to Excel (`<name>.xlsx`) | `{}` |
| `ganttChart` | `--gantt-*` | The Gantt chart (`<name>-gantt.<format>`) | A chart |
| `arrowGraph` | `--arrow-*` | The arrow graph (`<name>-arrow.<format>`) | A graph |
| `vertexGraph` | `--vertex-*` | The vertex graph (`<name>-vertex.<format>`) | A graph |
| `resourceChart` | `--resource-*` | The resource chart (`<name>-resource.<format>`) | A chart |
| `earnedValueChart` | `--ev-*` | The earned value chart (`<name>-ev.<format>`) | A chart |
| `scenarioChart` | `--scenario-chart-*` | The scenario chart (`<name>-scenario.<format>`) | A chart |

`<name>` is the name of the project's file without its extension: `plan` for `plan.zpp`.

A **chart** is `{ "format": "png", "width": 1600, "height": 900 }`: `format` is `jpeg` (the default), `png`, `bmp`, `webp` or `svg`, and the size, in pixels, is required, from 1 to the server's chart limit. A **graph** is `{ "format": "svg" }`: `format` is `jpeg` (the default), `png`, `pdf`, `svg`, `graphml` or `dot`. Every option, and every setting of an output, can be `null`, which is as if it were not given.

### Answer

`200 OK`, when the project compiled and every output asked for was produced:

```json
{
  "metrics": { "networkDuration": 5, "projectFinishDays": 5, "projectFinishDate": "2024-01-06", ... },
  "outputs": [
    { "kind": "project", "fileName": "plan.zpp", "contentType": "application/json", "content": "ewogICJWZXJz..." },
    { "kind": "ganttChart", "fileName": "plan-gantt.png", "contentType": "image/png", "content": "iVBORw0KGgo..." }
  ]
}
```

- `metrics` are the project's [metrics](#metrics).
- `outputs` lists each output, in the order the job produced them - the order of the table above - and is `[]` when none was asked for. `kind` is which it is, `fileName` the name zpp gives its file, `contentType` its media type, and `content` the file, in base64.
- With `?include=console`, `console` is there too: see [console](#console).

With `Accept: application/zip`, the answer is a zip, which `Content-Disposition` names `<name>.zip`: each output as a file, under the name zpp gives it, and `result.json`, which is the answer above less the files' contents. Every entry is stamped with the time the job ran at - the time `now` gave it, if it did.

#### Metrics

Every metric the project has, as a number or `null`: the names are in words and in `lowerCamelCase`, and nothing in them is written in the server's culture. The costs, billings and margins are estimates the project works out in floating point, in the unit it keeps its figures in - which has no currency.

| Metric | Is |
| ------ | -- |
| `activityRisk`, `activityRiskWithStandardDeviationCorrection`, `criticalityRisk`, `fibonacciRisk`, `geometricActivityRisk`, `geometricCriticalityRisk`, `geometricFibonacciRisk` | The risk metrics |
| `networkCyclomaticComplexity` | The network's cyclomatic complexity, a whole number |
| `networkDuration` | The network's duration, in days, a whole number |
| `networkDurationManMonths` | The duration, in man-months |
| `projectFinishDays` | The days from the project's start to its finish: the network's duration |
| `projectFinishDate` | The date the project finishes on, in the working calendar the project keeps, as a date (`2024-01-06`). `null`, with `projectFinishDays`, when the project has no duration |
| `effortEfficiency` | The efficiency of the effort |
| `activityEffort`, `directEffort`, `indirectEffort`, `otherEffort`, `totalEffort` | The effort |
| `directCost`, `indirectCost`, `otherCost`, `totalCost` | The costs |
| `directBilling`, `indirectBilling`, `otherBilling`, `totalBilling` | The billings |
| `directMargin`, `indirectMargin`, `otherMargin`, `totalMargin` | The margins, as ratios |
| `directMarginAbsolute`, `indirectMarginAbsolute`, `otherMarginAbsolute`, `totalMarginAbsolute` | The margins, as amounts |

`zpp --metrics-format json` writes the metrics as they were first released - `ActivityRisk`, and a `ProjectFinish` that is text - and goes on doing so: they are the command line's, which this API's are not.

#### Console

What `zpp` would have printed and exited with, for a client that wants to print and write exactly that, as `zpp --server` does. It is there when the request asks for it with `?include=console` - in an answer, and in the [problems](#problems) that come of a job that ran:

```json
"console": {
  "exitCode": 0,
  "standardOutput": "\n| Metrics                 | Values      |\n...",
  "standardError": "",
  "transcript": [
    { "kind": "output", "index": 0 },
    { "kind": "display", "text": "| Metrics                 | Values      |\n..." }
  ]
}
```

- `exitCode` is the code zpp would have exited with (see [Exit codes](COMMAND-LINE.md#exit-codes)).
- `standardOutput` and `standardError` are what it would have printed on each, every line ending in `\n`, written by the server in its culture. zpp's log is in neither: the server keeps its own.
- `transcript` records the job call by call, in the order it ran: each `line` it printed, each `display`ed block - with `hasErrors` when the block reports errors, which zpp shows in red - each `errorLine` on standard error, and each `output` as it was produced, by its `index` in `outputs`. Played back in order, it prints and writes what zpp would have, when it would have.

## List a project's scenarios

`POST /v1/projects/scenarios`

The request is a `multipart/form-data` body with a part named `project`, and nothing else, as `zpp --list-scenarios` takes only `--input`; `?include=console` adds the [console](#console).

```json
{
  "scenarios": [
    { "path": "Alpha", "id": "8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5", "isTracked": true, "isCurrent": true },
    { "path": "Beta", "id": "17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4", "isTracked": false, "isCurrent": false }
  ]
}
```

`path` names the scenario by where it is in the project - a child of another is `Alpha/Beta` - and is what `scenario` takes, as the `id` is. The problems are those of a compile, as far as they apply.

## Ask what the server is

`GET /v1/info`

```json
{
  "version": "0.10.1",
  "culture": "en-GB",
  "timeZone": "Europe/London",
  "limits": {
    "maxJobs": 4,
    "maxQueue": 8,
    "maxUploadMegabytes": 50,
    "maxChartWidth": 5000,
    "maxChartHeight": 5000,
    "jobTimeout": "PT2M",
    "maxCompileTimeout": "PT1M"
  }
}
```

- `culture` is the culture the server writes numbers and dates in - an empty string for the invariant culture, which a server that is not told otherwise on Linux has - and `timeZone` the time zone it writes times in, by its IANA name: it says `Europe/London` whatever its system calls it.
- `maxJobs` jobs run at once and `maxQueue` more wait for one to finish; any more are turned away (`503 busy`). A request, with its files, is at most `maxUploadMegabytes` megabytes; a chart at most `maxChartWidth` by `maxChartHeight` pixels. A job that runs past `jobTimeout` is stopped (`503 job-timeout`), and a request's `compileTimeout` is at most `maxCompileTimeout`.
- It cannot change while the server runs, so it has a strong `ETag`, and may be kept for a minute (`Cache-Control: private, max-age=60`): `If-None-Match` with the `ETag` answers `304`. `HEAD` answers with the headers alone.

`OPTIONS` on any of the paths answers `204`, with `Allow` - `POST, OPTIONS`; `GET, HEAD, OPTIONS` for `/v1/info` and `/v1/openapi` - and `Accept-Post: multipart/form-data` for the POSTs. It needs the API key, as the rest of `/v1` does, but on `/v1/openapi`.

## Fetch the description

`GET /v1/openapi`

The description of this API, [`openapi.yaml`](openapi.yaml), as it was when the server was built: the file of the repository itself, byte for byte, so that a server says what it takes, and not what some other version of it took.

```
curl -s -o openapi.yaml http://localhost:9770/v1/openapi
```

```
HTTP/1.1 200 OK
Content-Length: 56562
Content-Type: application/openapi+yaml
Date: Sun, 04 Oct 2026 18:42:54 GMT
Cache-Control: public, max-age=3600
ETag: "327657af6f700650fad3968337480cff"
Vary: Accept
Request-Id: deb7630432becdbd9ed7809ae90af7b0
X-Content-Type-Options: nosniff
```

- It is YAML. A request asks for it as `application/openapi+yaml`, the media type of a description of OpenAPI - which is what a request that says nothing is answered with - or as `application/yaml`; any other `Accept` is `406`, which says which it can answer with. It is not offered as JSON.
- It needs no API key, and neither do `HEAD` and `OPTIONS` on it: it holds nothing that is not in the repository, and a tool has to read what a server takes before it is told the key. It is the only path of `/v1` that needs none.
- It cannot change while the server runs, so it has a strong `ETag`, and may be kept by anyone for an hour (`Cache-Control: public, max-age=3600`): `If-None-Match` with the `ETag` answers `304`. It varies with `Accept`, and says so, and it is compressed for a client that accepts it. `HEAD` answers with the headers alone.
- The first server it names is `/`: the server that a description was fetched from is the one that it describes. Read from the repository, it names no server, and a tool is given the address of one.

## Probes

`GET /health/live` answers `200` once the server is listening, and `GET /health/ready` answers `200` once it has warmed up - a few seconds after it starts - and `503` until then. Each answers in plain text, needs no API key, and says `Cache-Control: no-store`. Jobs sent before the server is ready still run, only more slowly.

## Problems

Whatever the server cannot answer, it answers with [problem details](https://www.rfc-editor.org/rfc/rfc9457), `application/problem+json`:

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#compilation-failed",
  "title": "The project did not compile",
  "status": 422,
  "detail": "The project has a compilation error.",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "errors": [
    { "pointer": "#/project", "code": "P0010", "detail": "Invalid activity dependencies:\n999 is invalid but referenced by: 1" }
  ]
}
```

- `type` says which kind of problem it is, and is what a program branches on: it is the address of the kind's section in this document, which never changes. `title` is the same for every problem of its type, and `detail` says what is wrong this time, for people.
- `status` is the HTTP status. A client can rely on the split: **`400`, the request cannot be understood; `422`, the request is understood and the project, or what is asked of it, cannot be processed; `500`, the server failed; `503`, the server is busy or ran out of time.** A script gating a build on whether a plan compiles can use `curl --fail-with-body` and the exit status.
- `traceId` is the request's id, which is also its `Request-Id`.
- `errors` lists every problem a request's content has - not the first - each with where it is, a `code` for the rule it breaks, and `detail`, a phrase that reads on from the place (`must be from 1 to 5000 pixels`). A **pointer** is a JSON pointer in URI-fragment form into the request as OpenAPI models a multipart body, which is an object whose properties are its parts: `#/project` is the file, `#/options/outputs/ganttChart/width` a member of the options. A problem with a query parameter has `parameter` in place of `pointer`.
- `console`, with `?include=console`, is in the problems of a job that ran. `metrics` and `outputs` are in `output-failed`: what the job had when it failed.

A request with problems of both kinds - the request cannot be understood, and what it says is not valid - is answered `400`, with every problem listed.

### Codes

The `code` of an error is one of these, which a program can rely on, or - for a project that did not compile - the compiler's own, such as `P0010`:

| Code | Means |
| ---- | ----- |
| `required` | Something that has to be there is not |
| `unknownProperty` | A member, a part or a parameter the operation does not know |
| `wrongType` | A value of another type than the member takes |
| `notAllowed` | A value the member does not take, or a member that is not allowed here |
| `invalidFormat` | A value that is not written as the member's format is, or JSON that is not JSON |
| `outOfRange` | A number or a duration outside the limits |
| `notAllowedWithImport` | `scenario` with a workbook to import: only a project has scenarios |
| `tooLarge` | A part larger than the limit |
| `unsupportedFormat` | A file of a format the server does not read |
| `unreadable` | A file that is not one the server can read |
| `notFound`, `ambiguous`, `hasNoData` | The scenario is not there, names several, or holds no data |
| `failed` | An output could not be produced |

### The kinds of problem

Each section is named for the last part of its `type`.

#### malformed-request

`400`. The request cannot be understood: its parts cannot be read as `multipart/form-data`, its `options` are not JSON, or a query parameter is not one the operation takes or a value `include` does not have. Nothing a client can mend by changing what it says; mend how it says it. Not worth sending again unchanged.

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#malformed-request",
  "title": "The request could not be understood",
  "status": 400,
  "detail": "The request has 2 problems.",
  "traceId": "56a374c353c621113aedeb980f329e0b",
  "errors": [
    { "parameter": "include", "code": "notAllowed", "detail": "'metrics' is not something the answer can include: use console" },
    { "pointer": "#/options", "code": "invalidFormat", "detail": "must be valid JSON (line 2, position 1)" }
  ]
}
```

#### validation-failed

`422`. The request is understood, and what it says is not valid: a part that is missing, given twice or not known; an option that is not known, mistyped, not one of its values or out of range; `scenario` with `import`; a file without a name. Every problem is in `errors`.

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#validation-failed",
  "title": "The request is not valid",
  "status": 422,
  "detail": "The request has 4 problems.",
  "traceId": "5c69656d2c55ff91fe9b25ed75481324",
  "errors": [
    { "pointer": "#/options/compileTimeout", "code": "outOfRange", "detail": "must be from PT0.001S to PT1M" },
    { "pointer": "#/options/outputs/ganttChart/width", "code": "outOfRange", "detail": "must be from 1 to 5000 pixels" },
    { "pointer": "#/options/outputs/ev", "code": "unknownProperty", "detail": "is not known here: check its spelling" },
    { "pointer": "#/options/colour", "code": "unknownProperty", "detail": "is not known here: check its spelling" }
  ]
}
```

#### project-not-readable

`422`. The file sent as the project, or as the workbook to import, is not one the server can read: `errors` has `unreadable` at `#/project` or `#/import`. With `include=console`, the console has zpp's own words for why on its standard error, and the exit code 1. This is what sending `sample.xlsx`, a workbook, as the `project` is answered with:

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#project-not-readable",
  "title": "The project could not be read",
  "status": 422,
  "detail": "The file could not be read as a project.",
  "traceId": "d7edba72f6893b71f0d947b1e0bb763d",
  "errors": [
    { "pointer": "#/project", "code": "unreadable", "detail": "The file could not be read as a project." }
  ],
  "console": {
    "exitCode": 1,
    "standardOutput": "",
    "standardError": "Unexpected character encountered while parsing value: P. Path '', line 0, position 0.\n",
    "transcript": [
      { "kind": "errorLine", "text": "Unexpected character encountered while parsing value: P. Path '', line 0, position 0." }
    ]
  }
}
```

#### compilation-failed

`422`. The project compiled with errors; `errors` has each of the compiler's errors, with its code, at `#/project` (or `#/import`) - the example [above](#problems) is one. With `include=console`, the console has the compiler's report as zpp prints it, and the exit code 3.

#### compilation-timed-out

`422`. The compilation did not finish within its `compileTimeout`, which says nothing of whether the project is valid. Raise `compileTimeout`, up to the server's limit. The exit code is 4.

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#compilation-timed-out",
  "title": "The compilation ran out of time",
  "status": 422,
  "detail": "The compilation did not finish within its time limit.",
  "traceId": "9f1c0e3a5d7b4c28a6e41b0d93f2c7a8"
}
```

#### scenario-not-selectable

`422`. The scenario `options.scenario` names cannot be selected: `errors` has `notFound`, `ambiguous` or `hasNoData` at `#/options/scenario`, and the exit code is 1.

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#scenario-not-selectable",
  "title": "The scenario cannot be selected",
  "status": 422,
  "detail": "No scenario matches 'Gamma'",
  "traceId": "51205d67f2ec394b8a00ec1b86f88e4c",
  "errors": [
    { "pointer": "#/options/scenario", "code": "notFound", "detail": "No scenario matches 'Gamma'" }
  ]
}
```

#### output-failed

`500`. The project compiled, and an output it was asked for could not be produced. `errors` names each one that was not at `#/options/outputs/<kind>`, with `failed`; `outputs` has the others, which were produced, and `metrics` the project's. The exit code is 1.

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#output-failed",
  "title": "An output could not be produced",
  "status": 500,
  "detail": "The gantt chart could not be produced.",
  "traceId": "c40b7e9a21d84f6b8d35a7e0f1926b4d",
  "errors": [
    { "pointer": "#/options/outputs/ganttChart", "code": "failed", "detail": "The gantt chart could not be produced." }
  ],
  "metrics": { "networkDuration": 5, "projectFinishDays": 5, "projectFinishDate": "2024-01-06", ... },
  "outputs": [
    { "kind": "project", "fileName": "plan.zpp", "contentType": "application/json", "content": "ewogICJWZXJz..." }
  ]
}
```

#### unexpected-error

`500`. Anything the server did not expect. What went wrong is in the server's log, under the request's trace id, and is not in the answer - nothing about the server, its paths or its code is. Tell whoever runs the server the `traceId`.

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#unexpected-error",
  "title": "The server could not process the request",
  "status": 500,
  "detail": "The server failed to process the request. Its log says why, under the request's trace id.",
  "traceId": "7be0d4a91c3f4e5a8b26f9d1c0e7a354"
}
```

#### busy

`503`, with `Retry-After: 5`. All the jobs the server runs at once are running, and as many wait as it allows. The request was not run, so it can be sent again - after `Retry-After`, and after longer each time, with a little at random, as a client does that every other waits with does not all come back at once.

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#busy",
  "title": "The server is busy",
  "status": 503,
  "detail": "The server is running all the jobs it can, with as many waiting as it allows. Try again shortly.",
  "traceId": "002ff9eeea0321b50b20f508d7d0b4e6"
}
```

#### job-timeout

`503`, with `Retry-After: 5`. The job ran for longer than the server allows a job, `jobTimeout` in `/v1/info`, and was stopped. It is a `503` because the server's own limit stopped it, which the [RESTful API Guide](RESTFUL-API-GUIDE.md) says is not `504` ([RL-5](RESTFUL-API-GUIDE.md#11-rate-limiting-and-resource-protection)); but it is not worth sending again unchanged, as it would run out of time again. Make it less: fewer outputs, or smaller charts.

```json
{
  "type": "https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#job-timeout",
  "title": "The job took too long",
  "status": 503,
  "detail": "The job ran for longer than the server's limit of 120 seconds, and was stopped.",
  "traceId": "4879f96e5055e1e6d6c598b43dee3fe0"
}
```

### Problems without a kind of their own

These are what their status says, with the `type` RFC 9110 gives it:

| Status | When |
| ------ | ---- |
| `401` | The request needs the API key and does not carry it, or carries another. `WWW-Authenticate: Bearer` |
| `404` | There is nothing at the path |
| `405` | The path does not take the method. `Allow` says which it takes |
| `406` | `Accept` offers nothing the operation answers with; `detail` says what it can |
| `413` | The request is larger than the server accepts, or the `options` are larger than 64 KB |
| `415` | The request is not `multipart/form-data`, or the file to `import` is not a workbook (`unsupportedFormat` at `#/import`) |

The web server itself refuses a request it cannot read at all - a malformed request line, a header too large, a request that is too slow - before the API sees it, with a status and no body.

## What zpp --server does with each answer

`zpp --server` sends the request this API takes - the project as `project`, or the workbook as `import`, its options less the paths, `?include=console`, `Accept: application/json` and a `traceparent` - and reads the answer by its status and its `type`. A run on a server ends as it would have ended here, whatever the server answered: the exit code is the console's.

| Answer | zpp does | Exits with |
| ------ | -------- | ---------- |
| `200` | Plays back the console's transcript, writing each output where its options say | 0 |
| `422` `project-not-readable`, `scenario-not-selectable`; `500` `unexpected-error` | Plays back the console, which has the message on standard error | 1 |
| `422` `compilation-failed` | Plays back the console: the compiler's report | 3 |
| `422` `compilation-timed-out` | Plays back the console | 4 |
| `500` `output-failed` | Writes the outputs that came, plays back the console | 1 |
| `503` `busy` | Waits what `Retry-After` says, and longer each time - doubling, to half a minute, and a little more at random - and tries again, for up to two minutes | then 5 |
| `503` `job-timeout`; `400`, `401`, `406`, `413`, `415`; `422` `validation-failed`; no answer; an answer it cannot read | Says why, from `detail` and `errors`, with the request's id | 5 |

An answer whose console does not end as its kind does - a `compilation-failed` with exit code 0 - is an answer zpp cannot read: exit code 5, and nothing printed or written.

## Operations

### The log

The log is on standard error, as zpp's is: the server starting, listening and warming up, a line for each request it answers, and the warnings and errors of its jobs and of the web server. `--verbose` adds their informational output. Each line that belongs to a request has its trace id, which is the `Request-Id` its response carried. `--log-format` chooses how a line is written:

```
[16:30:43 INF] e43883d465069541ad7661173c4ec4fc POST /v1/projects/compile: 200 ok, exit code 0, after 35 ms
[16:30:44 WRN] 75343173ee6b17eac0d809a803f55737 Compilation failed with 1 error(s)
[16:30:44 INF] 75343173ee6b17eac0d809a803f55737 POST /v1/projects/compile: 422 compilation-failed, exit code 3, after 18 ms
```

for people (`--log-format text`, the default), where the time is the server's own, and for a program, an object a line (`--log-format json`):

```json
{"timestamp":"2026-10-04T15:30:53.582Z","level":"information","message":"POST /v1/projects/compile: 200 ok, exit code 0, after 40 ms","traceId":"dff96d60bf13644b7826fa3c90d66985","properties":{"method":"POST","path":"/v1/projects/compile","statusCode":200,"problem":"ok","exitCode":0,"elapsedMilliseconds":40,"sourceContext":"Zametek.ProjectPlan.CommandLine.ProjectEndpoints","requestId":"0HNP25HS0SVIJ:00000001","requestPath":"/v1/projects/compile","connectionId":"0HNP25HS0SVIJ"}}
```

Its members are `timestamp` (UTC, RFC 3339), `level` (`verbose`, `debug`, `information`, `warning`, `error` or `fatal`), `message`, `traceId` - for a line that belongs to a request - `exception` - for a line that has one - and `properties`, in `lowerCamelCase`, which are what the line was written with. `problem` is `ok` for an answer, the kind of problem - `compilation-failed` - for one that has a kind, and `refused` for one that has not (a `415`, a `406`). The web server adds properties of its own - `requestId` and `connectionId` are its - and a program that reads the log should ignore those it does not know.

A request the server refuses for its API key is logged at warning level, with no key in it: `GET /v1/info: refused, no API key`, or `refused, API key not accepted`.

### Objectives

`zpp serve` is a tool its users run for themselves, so it states no availability objective. Its latency is bounded by `jobTimeout`; a warm server compiles a small plan in tens of milliseconds, and a larger one in as long as the plan takes.

## Security review

The API Security Top 10 of OWASP, 2023, against the API as it stands (each release repeats the review, as [SEC-20](RESTFUL-API-GUIDE.md#105-operating-securely) of the guide requires):

| Risk | Where the API stands |
| ---- | -------------------- |
| API1 Broken object level authorization | Nothing is stored, so there are no objects to authorise: a request is about the project it carries |
| API2 Broken authentication | One shared key, at least 32 characters, kept as a hash, compared in constant time, sent as a bearer token, and over TLS beyond this machine; refusals are logged without it; a Unix domain socket is the user's alone |
| API3 Broken object property level authorization | A request's members are checked, one by one; one that is not known is refused, so a property cannot be assigned that was not meant to be; the answer holds only what the operation produces |
| API4 Unrestricted resource consumption | A limit on the size of a request, of its `options` and their depth, of a chart, of a job's time and of a compilation's, on the jobs that run at once and those that wait; the rest are turned away with `Retry-After` |
| API5 Broken function level authorization | One role: whoever has the key may use every operation; the health endpoints, which reveal only whether the server is ready, and the description of the API, which is the repository's, need none |
| API6 Unrestricted access to sensitive business flows | The operations compute from what they are sent and keep nothing; the limits bound what one client can ask for |
| API7 Server side request forgery | The server never fetches a URL: nothing in a request names one |
| API8 Security misconfiguration | TLS 1.2 and 1.3; plain http beyond this machine refused unless a proxy is said to end TLS; no `Server` header; `nosniff` and `no-store` on every response; a failure the server did not expect is answered generically, with nothing of its paths, types or code; no CORS - no browser calls it |
| API9 Improper inventory management | One version, `/v1`; `openapi.yaml` and this document describe every path, a test holds the description to the server, and the server serves the description at `/v1/openapi`; `/v1/info` says what the server is |
| API10 Unsafe consumption of APIs | The server calls no other API |

## Deviations from the guide

The API follows the [RESTful API Guide](RESTFUL-API-GUIDE.md), version 1.1, and records here where it does not follow a rule, or takes an option that a rule gives it, and why ([section 0.5](RESTFUL-API-GUIDE.md#05-deviations) of the guide). The description of the API records the same, as [DOC-2](RESTFUL-API-GUIDE.md#13-documentation-testing-and-governance) asks, and a test keeps its list of rules in step with this one:

| Rule | Deviation | Reason |
| ---- | --------- | ------ |
| [ASY-1](RESTFUL-API-GUIDE.md#9-long-running-and-bulk-operations) | A job is answered in its request, bounded by `jobTimeout`; past it the job is stopped (`503`), not carried on as a `202` | The server keeps nothing between requests: a job carried on would need results, owners and an expiry to be kept, and ASY-1 lets a service that keeps nothing stop the job at its bound instead |
| [REP-7](RESTFUL-API-GUIDE.md#3-representations) | The costs, billings and margins in `metrics` are floating-point numbers | The engine works them out so, and a project has no currency: they are estimates in the unit the project keeps its figures in, not amounts of money, which REP-7 lets be JSON numbers |
| [COL-1](RESTFUL-API-GUIDE.md#51-the-collection-representation), [COL-2](RESTFUL-API-GUIDE.md#52-paging-sorting-filtering-and-selection) | `scenarios` and `outputs` are plain arrays, not paged `items` | They are bounded views of what the caller sent, not collections the server holds: COL-1 says that a bounded array inside a representation is a plain array under a descriptive name |
| [ACT-4](RESTFUL-API-GUIDE.md#12-actions-procedural-concepts), [REQ-1](RESTFUL-API-GUIDE.md#4-requests) | `POST /v1/projects/scenarios` only reads, and requests are `multipart/form-data` | A project is a file, which cannot go in a URL |
| [RL-3](RESTFUL-API-GUIDE.md#11-rate-limiting-and-resource-protection) | No `RateLimit` fields | They are still an Internet-Draft, and the limit is a cap on jobs at once, not a rate: `Retry-After` and `limits` say the rest |
| [OPS-3](RESTFUL-API-GUIDE.md#14-operations) | No availability objective | See [Objectives](#objectives) |
| [ERR-1](RESTFUL-API-GUIDE.md#7-errors) | `408`, `414` and `431`, and a request the web server cannot read, have no body | The web server answers them before the API is reached, and cannot be made to write one |
| [SEC-3](RESTFUL-API-GUIDE.md#101-transport), [SEC-15](RESTFUL-API-GUIDE.md#104-input-and-resources) | No HSTS, no CORS | No browser calls this API; a proxy that ends TLS can add HSTS |

## Changelog

The API is versioned as a whole, by its path (`/v1`), as [VER-1 to VER-3](RESTFUL-API-GUIDE.md#12-versioning-and-evolution) of the guide have it: a change that breaks a client - a removed or renamed member, a changed status or problem `type`, a stricter rule - would be a new version; a member or an operation added would not.

### Unreleased

The first version of the API.

- `POST /v1/projects/compile`, `POST /v1/projects/scenarios`, `GET /v1/info`, `GET /v1/openapi`, `HEAD` and `OPTIONS`, and the health probes.
- The outcome is the status: `200` for a project that compiled and every output produced, `422` for one that cannot be processed, `500` for a server that failed, `503` for one that is busy or ran out of time; every problem is `application/problem+json`, with its request's id and every error listed.
- `?include=console` adds what zpp would have printed and exited with, to an answer or a problem.
- Optional: zip answers, `Accept` negotiation, compression, `ETag`.
