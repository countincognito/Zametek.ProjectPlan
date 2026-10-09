# The zpp serve API

`zpp serve` starts `zpp` as a web server (refer to [zpp as a server](COMMAND-LINE.md#zpp-as-a-server)). This document is the reference for the HTTP API of the server. It tells what a request is, what each answer is, and which problems the server can answer with.

The readers of this document send projects to the server. They use `curl`, a script or a program of their own. They are also the maintainers, who change the API only as the [changelog](#changelog) records.

The file [`openapi.yaml`](openapi.yaml) describes the same API for programs (OpenAPI 3.1), and the server [supplies it](#fetch-the-description). If the two differ, the description is correct and this document has an error. `zpp` itself is one client of the API when you start it with `zpp --server`. The section [What zpp --server does with each answer](#what-zpp---server-does-with-each-answer) describes its behavior.

The API follows the [RESTful API Guide](RESTFUL-API-GUIDE.md), and this document refers to the rules of the guide by their identifiers, for example `ASY-1`. The section [Deviations from the guide](#deviations-from-the-guide) lists the rules that the API does not follow.

The API has four operations, all under `/v1`. The table that follows lists them:

| Operation | Description |
| --------- | ----------- |
| [`POST /v1/projects/compile`](#compile-a-project) | This operation compiles the project in the request. It answers with the metrics of the project and the outputs (files) that the request asks for. |
| [`POST /v1/projects/scenarios`](#list-a-projects-scenarios) | This operation lists the scenarios of the project in the request. |
| [`GET /v1/info`](#ask-what-the-server-is) | This operation gives information about the server: its version, culture, time zone and limits. |
| [`GET /v1/openapi`](#fetch-the-description) | This operation gives the description of this API, `openapi.yaml`, for programs. It needs no API key. |

The server keeps nothing between requests. Each request carries its project, and the server does not store it. The two operations use POST only because a project is a file, and a file cannot go in a URL. A request that you send again has the same result. You can send a request to any server of the same version.

## Quick start

Start a server. It listens on `http://localhost:9770`, and it needs no API key on this computer:

```
zpp serve
```

Compile a project and print its metrics. Windows 10 and later have `curl.exe`. In Windows PowerShell, `curl` is a different command, and thus you must write `curl.exe`:

```
curl -s -F project=@plan.zpp http://localhost:9770/v1/projects/compile
```

The answer is JSON. It has the [metrics](#metrics) of the project and the outputs that the request asked for. This request asked for none. To ask for outputs, send the options as JSON in a part named `options`. Put the options in a file. This prevents problems with quotation marks in the shell:

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

`plan.zip` contains `plan.zpp`, `plan-gantt.png` and `plan-arrow.svg`, each with the name that `zpp` gives it. It also contains `result.json`, which is the JSON answer without the contents of the files. Without `Accept: application/zip`, the answer is JSON. Then each file is in the `outputs` list, as base64 in its `content`.

A project that does not compile gets a [problem](#problems) and not an answer. The status is `422`, and the problem lists each error of the compiler. `curl --fail-with-body` makes the command fail in this case. The exit code is 22, and `curl` prints the problem. Thus, you can use the command as a gate in a build:

```
curl --fail-with-body -s -F project=@plan.zpp http://localhost:9770/v1/projects/compile
```

The server also supplies its own description, for each tool that reads OpenAPI ([more information](#fetch-the-description)):

```
curl -s -o openapi.yaml http://localhost:9770/v1/openapi
```

## Conventions

Each rule in this section applies to each operation, unless the text says otherwise.

**Media types.** A request that sends a file is `multipart/form-data`. An answer is `application/json`. When a request to compile asks for a zip with `Accept: application/zip`, the answer is a zip. The description of the API is YAML. A problem is always `application/problem+json`, whatever `Accept` says.

The server negotiates `Accept` by quality. It takes `*/*` and `application/*` for what they are, and `q=0` refuses a type. If `Accept` offers nothing that the operation can answer with, the status is `406`, and the problem says what the operation can answer with. The answer of the operation `compile` varies with `Accept`, and the response says so with `Vary: Accept`.

**Names and values.** The server writes compact JSON in `lowerCamelCase`. It matches names exactly as written, with the case. An enumerated value is a string in `lowerCamelCase`, and never a number, for example `png`, `graphml` or `markdown`.

A time is RFC 3339 with its offset. A date is `2024-01-06`. A duration is ISO 8601 (`PT5S`, `PT2M`) in days, hours, minutes and seconds, and not in longer units. A metric that the server cannot calculate is `null`. The metric is known, and it has no value.

The server refuses a request that names a member that it does not know, with status `422`, and it says which member. An answer can have more members in a later version, and a client must ignore them.

**Request ids.** Each response has the header `Request-Id`. This applies to an answer, a problem, `401`, `404`, `OPTIONS` and a probe. The id has 32 hexadecimal characters. It is the `traceId` of the problem. It is also the trace id on each line of the server log that belongs to the request.

Give the id to the person who operates the server, who can find the request in the log with it. A request that has a [`traceparent`](https://www.w3.org/TR/trace-context/) keeps the trace that it names, and the trace id is the id of the request. The server gives an id of its own to a request that has no `traceparent`.

**Authentication.** A server that only this computer can reach needs no API key. A server that other computers can reach must have a key. Then each path under `/v1` needs the key, in the header `Authorization: Bearer <key>`. A request without the key, or with an incorrect key, gets status `401` with `WWW-Authenticate: Bearer`. The health endpoints and [the description of the API](#fetch-the-description) are the only paths that need no key.

A key has a minimum of 32 characters. A random 256-bit key in base64 has 44 characters, and `openssl rand -base64 32` makes such a key. The server keeps only a hash of the key and compares it in constant time. It logs each request that it refuses, at warning level, with the method, the path and the trace id, but never the key.

Other users of the computer can read a key on a command line. Thus, give `curl` the header from a file with `-H @key-header.txt`. The file has the one line `Authorization: Bearer <key>`.

**Transport.** The API is available to other computers over HTTPS, which accepts TLS 1.2 and TLS 1.3. It is also available over plain HTTP behind a proxy that ends TLS (`--behind-tls-proxy`). The server refuses to listen on plain HTTP that other computers can connect to, unless you tell it so with this option. Plain HTTP sends the key and each project in clear text.

On this computer, the server can listen on plain HTTP, on a Unix domain socket, or on both. Only the user of the server can connect to a Unix domain socket, and it needs no key.

**Compression.** The server compresses JSON, problems and the description of the API with brotli or gzip for a client that accepts them (`Accept-Encoding`). The response says so with `Vary: Accept-Encoding`. The server does not compress a zip again.

**Caching.** Each response has `Cache-Control: no-store`, because the answers contain the project of a user. Two responses are exceptions. A client can cache the response from [`/v1/info`](#ask-what-the-server-is) for one minute. Any cache can keep the response with [the description of the API](#fetch-the-description) for one hour. Each of them has an `ETag` for validation.

**Limits.** [`/v1/info`](#ask-what-the-server-is) gives the limits of the server, which the operator of the server sets (refer to [Limits](COMMAND-LINE.md#limits)). The server has limits for:

- The size of a request and of a chart
- The time of a job and of a compilation
- The number of jobs at one time and the number of jobs that wait.

A request beyond a limit gets a problem that names the limit.

**Culture.** The text that the server writes is in the culture of the server, which `/v1/info` names. This text is the `title` and the `detail` of a problem, and the `console`. A client cannot negotiate the culture. A program does not depend on this text. It uses the `type` of a problem and the `code` of its `errors`.

## Compile a project

`POST /v1/projects/compile`

### Request

The request is a `multipart/form-data` body with these parts. Its size is not more than the upload limit of the server:

| Part | Type | Notes |
| ---- | ---- | ----- |
| `project` | A file | A project (`.zpp`), as `zpp --input` takes it. The file needs a name, because the outputs get their names from it. The server drops its path, if it has one. A request must have exactly one of `project` and `import`. |
| `import` | A file | An Excel workbook (`.xlsx`) to import into a new project, as `zpp --import` does. The server does not import MS Project files. Import them with `zpp` itself. |
| `options` | JSON | Optional. A file or a field. The server does not insist on the content type of the part, and thus `curl -F 'options={...}'` operates. The maximum size is 64 KB, and the maximum nesting depth is 16. |

The server refuses a part that the operation does not take. It also refuses a `project` or an `import` that you send as a field and not as a file. The query parameter `include` asks for more in the answer:

| Parameter | Type | Notes |
| --------- | ---- | ----- |
| `include` | A comma-separated list | The value `console` adds the [console](#console) to the answer. Any other value and any other parameter is a problem with the request. The server does not ignore a parameter that has a spelling error. |

### Options

The options are the options of `zpp`, with the defaults of `zpp`, but without the paths. `zpp` gives the file or directory for an output. A request asks for the output instead, and the answer carries it. A request spells what it asks for in the same way as the answer. Each member of `outputs` is the `kind` of an output in the answer.

| Option | Option of zpp | Description |
| ------ | ------------- | ----------- |
| `scenario` | `--scenario` | The scenario to load, by name or by id. The name is not case-sensitive. The id is the whole id, or a unique prefix of a minimum of four hexadecimal characters. A name that matches has priority over a prefix. Only with a `project`. |
| `baseTheme` | `--base-theme` | `light` (the default) or `dark`: the theme of the charts and graphs. |
| `metricsFormat` | `--metrics-format` | `markdown` (the default), `table` or `json`: how the [console](#console) shows the metrics. It has no other effect. |
| `compileTimeout` | `--compile-timeout` | An ISO 8601 duration, from `PT0.001S` to the limit of the server (`maxCompileTimeout`). Without it, the value is `PT5S` as for `zpp`, or the limit if that is shorter. A request cannot disable the limit, but `zpp` can. |
| `now` | `--now` | A time with its offset from UTC, for example `2026-10-03T09:00:00+01:00`. The job uses it in place of the clock for each item that it stamps with a time. |
| `outputs` | | What the job produces (refer to the table that follows). |

The members of `outputs` are the outputs that the job can produce, in the order in which it produces them. The job produces an output that the request gives. It does not produce an output that the request does not give. Two outputs have no settings of their own at this time. The request gives them as an empty object. Later, the object can carry settings, and this does not break a request.

| Member | Option of zpp | Produces | Settings |
| ------ | ------------- | -------- | -------- |
| `project` | `--output` | The project, as `zpp` saves it (`<name>.zpp`) | `{}` |
| `scenarioExport` | `--export` | The loaded scenario, as `zpp` exports it to Excel (`<name>.xlsx`) | `{}` |
| `ganttChart` | `--gantt-*` | The Gantt chart (`<name>-gantt.<format>`) | A chart |
| `arrowGraph` | `--arrow-*` | The arrow graph (`<name>-arrow.<format>`) | A graph |
| `vertexGraph` | `--vertex-*` | The vertex graph (`<name>-vertex.<format>`) | A graph |
| `resourceChart` | `--resource-*` | The resource chart (`<name>-resource.<format>`) | A chart |
| `earnedValueChart` | `--ev-*` | The earned value chart (`<name>-ev.<format>`) | A chart |
| `scenarioChart` | `--scenario-chart-*` | The scenario chart (`<name>-scenario.<format>`) | A chart |

`<name>` is the name of the file of the project, without its extension. For `plan.zpp`, `<name>` is `plan`.

A **chart** is `{ "format": "png", "width": 1600, "height": 900 }`. The `format` is `jpeg` (the default), `png`, `bmp`, `webp` or `svg`. The size is in pixels, from 1 to the chart limit of the server, and the request must give it.

A **graph** is `{ "format": "svg" }`. The `format` is `jpeg` (the default), `png`, `pdf`, `svg`, `graphml` or `dot`. Each option, and each setting of an output, can be `null`. This is the same as if the request does not give it.

### Answer

The status is `200 OK` when the project compiled and the job produced each output that the request asked for:

```json
{
  "metrics": { "networkDuration": 5, "projectFinishDays": 5, "projectFinishDate": "2024-01-06", ... },
  "outputs": [
    { "kind": "project", "fileName": "plan.zpp", "contentType": "application/json", "content": "ewogICJWZXJz..." },
    { "kind": "ganttChart", "fileName": "plan-gantt.png", "contentType": "image/png", "content": "iVBORw0KGgo..." }
  ]
}
```

- `metrics` has the [metrics](#metrics) of the project.
- `outputs` lists each output, in the order in which the job produced them. This is the order of the table above. The list is `[]` when the request asked for no output. `kind` shows which output it is. `fileName` is the name that `zpp` gives to its file. `contentType` is its media type. `content` is the file, in base64.
- With `?include=console`, the answer also has `console` (refer to [console](#console)).

With `Accept: application/zip`, the answer is a zip. `Content-Disposition` names it `<name>.zip`. The zip contains each output as a file, with the name that `zpp` gives it. It also contains `result.json`, which is the answer above without the contents of the files. The time of each entry is the time at which the job ran, or the time that `now` gave.

#### Metrics

The answer has each metric of the project, as a number or `null`. The names are words in `lowerCamelCase`, and the server does not write them in its culture. The costs, billings and margins are estimates. The project calculates them in floating point, in the unit of its figures. This unit has no currency.

| Metric | Description |
| ------ | ----------- |
| `activityRisk`, `activityRiskWithStandardDeviationCorrection`, `criticalityRisk`, `fibonacciRisk`, `geometricActivityRisk`, `geometricCriticalityRisk`, `geometricFibonacciRisk` | The risk metrics. |
| `networkCyclomaticComplexity` | The cyclomatic complexity of the network, a whole number. |
| `networkDuration` | The duration of the network, in days, a whole number. |
| `networkDurationManMonths` | The duration, in person-months. |
| `projectFinishDays` | The number of days from the start of the project to its finish. This is the duration of the network. |
| `projectFinishDate` | The date on which the project finishes, in the working calendar of the project, as a date (`2024-01-06`). The value is `null`, with `projectFinishDays`, when the project has no duration. |
| `effortEfficiency` | The efficiency of the effort. |
| `activityEffort`, `directEffort`, `indirectEffort`, `otherEffort`, `totalEffort` | The effort. |
| `directCost`, `indirectCost`, `otherCost`, `totalCost` | The costs. |
| `directBilling`, `indirectBilling`, `otherBilling`, `totalBilling` | The billings. |
| `directMargin`, `indirectMargin`, `otherMargin`, `totalMargin` | The margins, as ratios. |
| `directMarginAbsolute`, `indirectMarginAbsolute`, `otherMarginAbsolute`, `totalMarginAbsolute` | The margins, as amounts. |

`zpp --metrics-format json` writes the metrics in the form of the first release, for example `ActivityRisk` and a `ProjectFinish` that is text. It continues to do so. These metrics belong to the command line, and the metrics of this API are different.

#### Console

The console has the text that `zpp` prints and the exit code of `zpp` in a local run. A client that wants to print and write exactly this uses it, as `zpp --server` does. The console is in the answer when the request asks for it with `?include=console`. It is also in the [problems](#problems) of a job that ran:

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

- `exitCode` is the exit code of `zpp` in a local run (refer to [Exit codes](COMMAND-LINE.md#exit-codes)).
- `standardOutput` and `standardError` are the text that `zpp` prints on each stream in a local run. Each line ends with `\n`, and the server writes the text in its culture. The log of `zpp` is in neither of them, because the server keeps its own log.
- `transcript` records the job step by step, in the order in which it ran. It has each `line` that the job printed, and each `display` block. A `display` block has `hasErrors` when it reports errors, which `zpp` shows in red. It also has each `errorLine` on the standard error stream, and each `output` when the job produced it, by its `index` in `outputs`. A client that plays back the transcript in order prints and writes the same text and files as a local run, in the same sequence.

## List a project's scenarios

`POST /v1/projects/scenarios`

The request is a `multipart/form-data` body with a part named `project` and no other part, because `zpp --list-scenarios` takes only `--input`. `?include=console` adds the [console](#console).

```json
{
  "scenarios": [
    { "path": "Alpha", "id": "8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5", "isTracked": true, "isCurrent": true },
    { "path": "Beta", "id": "17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4", "isTracked": false, "isCurrent": false }
  ]
}
```

- `path` names the scenario by its location in the project. A child of another scenario has the path `Alpha/Beta`. `scenario` takes the `path`, and it also takes the `id`.
- `isTracked` shows that the scenario is *tracked*. The scenario chart shows the metrics of the scenario as a point, against the metrics of the axes of the chart. Tracking does only this. It does not change how the scenario compiles, what its metrics are, or what any other output holds. It is not the progress tracking of activities.
- `isCurrent` shows that the scenario is the *current* scenario of the project. This is the scenario that was open when a user saved the project. `zpp` loads it when the request does not name a scenario with `scenario`. A project has a maximum of one current scenario.

The problems are the problems of a compile, where they apply.

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

- `culture` is the culture in which the server writes numbers and dates. It is an empty string for the invariant culture, which a server on Linux has when nobody sets a culture. `timeZone` is the time zone in which the server writes times. It gives the IANA name, for example `Europe/London`, and not the name that the system uses.
- `maxJobs` jobs can run at one time, and `maxQueue` more jobs can wait for a free place. The server turns away more requests (`503 busy`).
- A request with its files has a maximum of `maxUploadMegabytes` megabytes. A chart has a maximum of `maxChartWidth` by `maxChartHeight` pixels.
- The server stops a job that uses more time than `jobTimeout` (`503 job-timeout`). The `compileTimeout` of a request has a maximum of `maxCompileTimeout`.
- The information cannot change while the server operates. Thus, the response has a strong `ETag`, and a client can keep it for one minute (`Cache-Control: private, max-age=60`). A request with `If-None-Match` and the `ETag` gets status `304`. `HEAD` gets the headers only.

`OPTIONS` on each of the paths gets status `204` with the header `Allow`. The value is `POST, OPTIONS`, and it is `GET, HEAD, OPTIONS` for `/v1/info` and `/v1/openapi`. The two operations with POST also get `Accept-Post: multipart/form-data`. `OPTIONS` needs the API key, as the other requests under `/v1` do, but not on `/v1/openapi`.

## Fetch the description

`GET /v1/openapi`

This operation gives the description of this API, [`openapi.yaml`](openapi.yaml), in the version from the time of the build of the server. It is the file of the repository, byte for byte. Thus, a server describes what it takes, and not what a different version of it took.

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

- The description is YAML. A request asks for it as `application/openapi+yaml`, the media type of a description of OpenAPI, or as `application/yaml`. A request that gives no `Accept` gets `application/openapi+yaml`. Any other `Accept` gets status `406`, and the problem names the types that the server can answer with. The server does not offer the description as JSON.
- The description needs no API key, and `HEAD` and `OPTIONS` on it need none. It holds only what is in the repository, and a tool must read what a server takes before it receives the key. It is the only path under `/v1` that needs no key.
- The description cannot change while the server operates. Thus, the response has a strong `ETag`, and any cache can keep it for one hour (`Cache-Control: public, max-age=3600`). A request with `If-None-Match` and the `ETag` gets status `304`. The response varies with `Accept` and says so, and the server compresses it for a client that accepts compression. `HEAD` gets the headers only.
- The first server that the description names is `/`. Thus, the server from which a client fetches the description is the server that the description describes. The file in the repository names no server, and a tool receives the address of a server.

## Probes

`GET /health/live` answers `200` when the server listens. `GET /health/ready` answers `200` after the warm-up of the server, which takes some seconds after the start, and `503` before that. Each probe answers in plain text, needs no API key, and sends `Cache-Control: no-store`. The server also processes jobs that arrive before it is ready, but more slowly.

## Problems

When the server cannot answer a request, it answers with [problem details](https://www.rfc-editor.org/rfc/rfc9457) in `application/problem+json`:

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

- `type` shows the kind of the problem, and a program uses it to select its action. It is the address of the section of this kind in this document, and it never changes. `title` is the same for each problem of its type. `detail` says, for persons, what is wrong this time.
- `status` is the HTTP status. A client can rely on this split. Status `400` means that the server cannot understand the request. Status `422` means that the server understands the request, but it cannot process the project or what the request asks for. Status `500` means that the server failed. Status `503` means that the server is busy or ran out of time. A script that gates a build on a compile can use `curl --fail-with-body` and the exit status.
- `traceId` is the id of the request, which is also its `Request-Id`.
- `errors` lists each problem in the content of the request, and not only the first. Each item has the location of the problem, a `code` for the rule that it breaks, and `detail`. The `detail` is a phrase that continues from the location, for example `must be from 1 to 5000 pixels`. A **pointer** is a JSON pointer in URI-fragment form. It points into the request as OpenAPI models a multipart body: an object whose properties are its parts. For example, `#/project` is the file, and `#/options/outputs/ganttChart/width` is a member of the options. A problem with a query parameter has `parameter` in place of `pointer`.
- With `?include=console`, `console` is in the problems of a job that ran. `metrics` and `outputs` are in `output-failed`: they have what the job had when it failed.

A request can have both kinds of problem: the server cannot understand it, and what it says is not valid. The server answers it with status `400`, and it lists each problem.

### Codes

The `code` of an error is one of the codes below, and a program can rely on them. For a project that did not compile, the code is the code of the compiler, for example `P0010`:

| Code | Means |
| ---- | ----- |
| `required` | Something that must be there is not there. |
| `unknownProperty` | A member, a part or a parameter that the operation does not know. |
| `wrongType` | A value of a different type than the member takes. |
| `notAllowed` | A value that the member does not take, or a member that is not allowed here. |
| `invalidFormat` | A value that is not in the format of the member, or JSON that is not valid JSON. |
| `outOfRange` | A number or a duration outside the limits. |
| `notAllowedWithImport` | `scenario` with a workbook to import. Only a project has scenarios. |
| `tooLarge` | A part larger than the limit. |
| `unsupportedFormat` | A file in a format that the server does not read. |
| `unreadable` | A file that the server cannot read. |
| `notFound`, `ambiguous`, `hasNoData` | The scenario does not exist, the name matches more than one scenario, or the scenario holds no data. |
| `failed` | The job cannot produce an output. |

### The kinds of problem

The name of each section is the last part of its `type`.

#### malformed-request

`400`. The server cannot understand the request. These are the possible causes:

- The server cannot read the parts as `multipart/form-data`.
- The `options` are not JSON.
- A query parameter is not a parameter that the operation takes.
- A query parameter has a value that `include` does not have.

A client cannot correct this by a change to what it says. It must correct how it says it. Do not send the request again without a change.

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

`422`. The server understands the request, but what it says is not valid. A request is not valid in these cases:

- A part is missing, the request gives a part twice, or a part is not known.
- An option is not known, it has an incorrect type, it is not one of its values, or it is out of range.
- The request gives `scenario` with `import`.
- A file has no name.

`errors` has each problem.

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

`422`. The server cannot read the file that the request sends as the project or as the workbook to import. `errors` has `unreadable` at `#/project` or `#/import`. With `include=console`, the console has the message of `zpp` that gives the reason on its standard error stream, and the exit code is 1. This is the answer to a request that sends `sample.xlsx`, a workbook, as the `project`:

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

`422`. The project compiled with errors. `errors` has each error of the compiler, with its code, at `#/project` (or `#/import`). The example [above](#problems) is such a problem. With `include=console`, the console has the report of the compiler as `zpp` prints it, and the exit code is 3.

#### compilation-timed-out

`422`. The compilation did not end within its `compileTimeout`. This does not show if the project is valid. Increase `compileTimeout`, up to the limit of the server. The exit code is 4.

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

`422`. The server cannot select the scenario that `options.scenario` names. `errors` has `notFound`, `ambiguous` or `hasNoData` at `#/options/scenario`, and the exit code is 1.

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

`500`. The project compiled, but the job did not produce an output that the request asked for. `errors` names each output that the job did not produce, at `#/options/outputs/<kind>`, with `failed`. `outputs` has the other outputs, which the job produced, and `metrics` has the metrics of the project. The exit code is 1.

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

`500`. This is each failure that the server did not expect. The reason is in the log of the server, under the trace id of the request. It is not in the answer, and the answer has no information about the server, its paths or its source code. Give the `traceId` to the person who operates the server.

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

`503`, with `Retry-After: 5`. The server runs the maximum number of jobs at one time, and the maximum number of jobs wait. The server did not run the request, and thus you can send it again. Wait for the time in `Retry-After`. Wait longer each time, with a small random change. Then the clients that wait do not all come back at the same time.

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

`503`, with `Retry-After: 5`. The job used more time than the server allows for a job (`jobTimeout` in `/v1/info`), and the server stopped it. The status is `503` and not `504`, because the own limit of the server stopped the job. The [RESTful API Guide](RESTFUL-API-GUIDE.md) gives this rule ([RL-5](RESTFUL-API-GUIDE.md#11-rate-limiting-and-resource-protection)). Do not send the request again without a change, because the job reaches the time limit again. Make the job smaller: use fewer outputs or smaller charts.

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

These problems have only their status, with the `type` that RFC 9110 gives to it:

| Status | When |
| ------ | ---- |
| `401` | The request needs the API key and does not have it, or it has an incorrect key. The response has `WWW-Authenticate: Bearer`. |
| `404` | There is nothing at the path. |
| `405` | The path does not take the method. `Allow` shows the methods that it takes. |
| `406` | `Accept` offers nothing that the operation can answer with. `detail` says what it can answer with. |
| `413` | The request is larger than the server accepts, or the `options` are larger than 64 KB. |
| `415` | The request is not `multipart/form-data`, or the file to `import` is not a workbook (`unsupportedFormat` at `#/import`). |

The web server itself refuses a request that it cannot read at all. Examples: a malformed request line, a header that is too large, or a request that is too slow. It does this before the API sees the request, and it answers with a status and no body.

## What zpp --server does with each answer

`zpp --server` sends the request that this API takes. The request has the project as `project`, or the workbook as `import`, the options of `zpp` without the paths, `?include=console`, `Accept: application/json` and a `traceparent`. `zpp` reads the answer by its status and its `type`. A run on a server ends in the same way as a local run, for each answer of the server. The exit code is the exit code of the console.

| Answer | What zpp does | Exit code |
| ------ | ------------- | --------- |
| `200` | Plays back the transcript of the console, and writes each output where its options say. | 0 |
| `422` `project-not-readable` or `scenario-not-selectable`, and `500` `unexpected-error` | Plays back the console, which has the message on the standard error stream. | 1 |
| `422` `compilation-failed` | Plays back the console: the report of the compiler. | 3 |
| `422` `compilation-timed-out` | Plays back the console. | 4 |
| `500` `output-failed` | Writes the outputs that came, and plays back the console. | 1 |
| `503` `busy` | Waits for the time in `Retry-After`, and longer each time. The wait doubles, up to half a minute, with a small random addition. Then it tries again, for up to two minutes. | Then 5 |
| `503` `job-timeout`, the statuses `400`, `401`, `406`, `413` and `415`, `422` `validation-failed`, no answer, and an answer that `zpp` cannot read | Shows the reason from `detail` and `errors`, with the id of the request. | 5 |

An answer whose console has an exit code that does not match its kind is an answer that `zpp` cannot read. An example is a `compilation-failed` with exit code 0. Then `zpp` exits with code 5, and it prints and writes nothing.

## Operations

### The log

The server writes its log to the standard error stream, as `zpp` does. The log has these items:

- The start, the listening and the warm-up of the server
- A line for each request that the server answers
- The warnings and errors of its jobs and of the web server.

`--verbose` adds the informational output of the jobs and of the web server. Each line that belongs to a request has its trace id, which is the `Request-Id` of its response. `--log-format` selects the format of a line:

```
[16:30:43 INF] e43883d465069541ad7661173c4ec4fc POST /v1/projects/compile: 200 ok, exit code 0, after 35 ms
[16:30:44 WRN] 75343173ee6b17eac0d809a803f55737 Compilation failed with 1 error(s)
[16:30:44 INF] 75343173ee6b17eac0d809a803f55737 POST /v1/projects/compile: 422 compilation-failed, exit code 3, after 18 ms
```

This is the format for persons (`--log-format text`, the default). The time is the time of the server. The format for a program is one object for each line (`--log-format json`):

```json
{"timestamp":"2026-10-04T15:30:53.582Z","level":"information","message":"POST /v1/projects/compile: 200 ok, exit code 0, after 40 ms","traceId":"dff96d60bf13644b7826fa3c90d66985","properties":{"method":"POST","path":"/v1/projects/compile","statusCode":200,"problem":"ok","exitCode":0,"elapsedMilliseconds":40,"sourceContext":"Zametek.ProjectPlan.CommandLine.ProjectEndpoints","requestId":"0HNP25HS0SVIJ:00000001","requestPath":"/v1/projects/compile","connectionId":"0HNP25HS0SVIJ"}}
```

The JSON object has these members:

- `timestamp`: the time in UTC, RFC 3339
- `level`: `verbose`, `debug`, `information`, `warning`, `error` or `fatal`
- `message`
- `traceId`, for a line that belongs to a request
- `exception`, for a line that has an exception
- `properties`, in `lowerCamelCase`, which are the values that the server used to write the line.

`problem` is `ok` for an answer. For a problem that has a kind, it is the kind, for example `compilation-failed`. For a problem that has no kind, for example a `415` or a `406`, it is `refused`. The web server adds properties of its own. `requestId` and `connectionId` are examples. A program that reads the log must ignore properties that it does not know.

The server logs a request that it refuses for its API key at warning level, and the line has no key. Examples: `GET /v1/info: refused, no API key`, or `refused, API key not accepted`.

### Objectives

`zpp serve` is a tool that its users operate for themselves. Thus, it states no availability objective. `jobTimeout` limits its latency. A warm server compiles a small plan in tens of milliseconds, and it compiles a larger plan in the time that the plan needs.

## Security review

This table compares the OWASP API Security Top 10 (2023) with the API in its present form. Each release repeats the review, as [SEC-20](RESTFUL-API-GUIDE.md#105-operating-securely) of the guide requires:

| Risk | Where the API stands |
| ---- | -------------------- |
| API1 Broken object level authorization | The server stores nothing, and thus it has no objects to authorize. A request is about the project that it carries. |
| API2 Broken authentication | The API has one shared key. It has a minimum of 32 characters, and the client sends it as a bearer token. The server keeps it as a hash and compares it in constant time. Beyond this computer, TLS protects it. The server logs refusals without the key. A Unix domain socket belongs to the user alone. |
| API3 Broken object property level authorization | The server checks each member of a request. It refuses a member that it does not know, and thus a request cannot assign a property that it must not assign. The answer holds only what the operation produces. |
| API4 Unrestricted resource consumption | The server limits the size of a request, the size and the depth of its `options`, and the size of a chart. It also limits the time of a job and of a compilation, and the number of jobs that run at one time and that wait. It turns away the other requests with `Retry-After`. |
| API5 Broken function level authorization | The API has one role. Whoever has the key can use each operation. The health endpoints need no key, because they show only if the server is ready. The description of the API needs no key, because it is the description of the repository. |
| API6 Unrestricted access to sensitive business flows | The operations calculate from what they receive, and they keep nothing. The limits restrict what one client can ask for. |
| API7 Server side request forgery | The server never fetches a URL, because nothing in a request names a URL. |
| API8 Security misconfiguration | The API accepts TLS 1.2 and TLS 1.3. It refuses plain HTTP beyond this computer, unless you say that a proxy ends TLS. It sends no `Server` header. Each response has `nosniff` and `no-store`. The server answers a failure that it did not expect in a generic way, with no information about its paths, types or source code. The API has no CORS, because no browser calls it. |
| API9 Improper inventory management | The API has one version, `/v1`. `openapi.yaml` and this document describe each path. A test holds the description to the server, and the server supplies the description at `/v1/openapi`. `/v1/info` shows what the server is. |
| API10 Unsafe consumption of APIs | The server calls no other API. |

## Deviations from the guide

The API follows the [RESTful API Guide](RESTFUL-API-GUIDE.md), version 1.2. This section records each rule that the API does not follow, or each option that a rule gives to the API, and the reason ([section 0.5](RESTFUL-API-GUIDE.md#05-deviations) of the guide). The description of the API records the same information, as [DOC-2](RESTFUL-API-GUIDE.md#13-documentation-testing-and-governance) asks. A test makes sure that its list of rules matches this list:

| Rule | Deviation | Reason |
| ---- | --------- | ------ |
| [ASY-1](RESTFUL-API-GUIDE.md#9-long-running-and-bulk-operations) | The API answers a job in its request, and `jobTimeout` limits the job. If a job uses more time, the server stops it (`503`) and does not continue it as a `202`. | The server keeps nothing between requests. To continue a job, the server must keep results, owners and an expiry. ASY-1 lets a service that keeps nothing stop the job at its limit instead. |
| [REP-7](RESTFUL-API-GUIDE.md#3-representations) | The costs, billings and margins in `metrics` are floating-point numbers. | The engine calculates them in this way, and a project has no currency. They are estimates in the unit of the figures of the project, and not amounts of money. REP-7 lets such values be JSON numbers. |
| [COL-1](RESTFUL-API-GUIDE.md#51-the-collection-representation), [COL-2](RESTFUL-API-GUIDE.md#52-paging-sorting-filtering-and-selection) | `scenarios` and `outputs` are plain arrays and not paged `items`. | They are bounded parts of what the caller sent, and not collections that the server holds. COL-1 says that a bounded array inside a representation is a plain array under a descriptive name. |
| [ACT-4](RESTFUL-API-GUIDE.md#12-actions-procedural-concepts), [REQ-1](RESTFUL-API-GUIDE.md#4-requests) | `POST /v1/projects/scenarios` only reads, and requests are `multipart/form-data`. | A project is a file, and a file cannot go in a URL. |
| [RL-3](RESTFUL-API-GUIDE.md#11-rate-limiting-and-resource-protection) | The API has no `RateLimit` fields. | They are still an Internet-Draft. The limit is a cap on the jobs at one time, and not a rate. `Retry-After` and `limits` give the other information. |
| [OPS-3](RESTFUL-API-GUIDE.md#14-operations) | The API has no availability objective. | Refer to [Objectives](#objectives). |
| [ERR-1](RESTFUL-API-GUIDE.md#7-errors) | `408`, `414` and `431`, and a request that the web server cannot read, have no body. | The web server answers them before the API gets them, and it cannot write a body. |
| [SEC-3](RESTFUL-API-GUIDE.md#101-transport), [SEC-15](RESTFUL-API-GUIDE.md#104-input-and-resources) | The API has no HSTS and no CORS. | No browser calls this API. A proxy that ends TLS can add HSTS. |

## Changelog

The API has one version for the whole API, in its path (`/v1`), as [VER-1 to VER-3](RESTFUL-API-GUIDE.md#12-versioning-and-evolution) of the guide require. A change that breaks a client makes a new version. Examples: a member that you remove or rename, a changed status or problem `type`, or a stricter rule. A member or an operation that you add does not make a new version.

### Unreleased

This is the first version of the API.

- The operations `POST /v1/projects/compile`, `POST /v1/projects/scenarios`, `GET /v1/info` and `GET /v1/openapi`, the methods `HEAD` and `OPTIONS`, and the health probes.
- The status shows the result. `200` is for a project that compiled and where the job produced each output. `422` is for a project that the server cannot process. `500` is for a server that failed. `503` is for a server that is busy or ran out of time. Each problem is `application/problem+json`, with the id of its request and a list of each error.
- `?include=console` adds the text and the exit code of a local run to an answer or a problem.
- Optional features: zip answers, `Accept` negotiation, compression and `ETag`.
