# Glossary

This document lists the terms that the other documents use in a special way. It also states the rules for the language of the documentation.

## Language of the documentation

The documentation follows the writing rules of [ASD-STE100 Simplified Technical English Issue 9](https://www.asd-ste100.org/assets/files/ASD-STE100_ISSUE9.pdf) loosely. These points show what loosely means:

- **Sentences.** A sentence has a maximum of 20 words in a procedure and 25 words in a description. A paragraph has a maximum of six sentences, and its first sentence gives the topic. The text uses the active voice and the simple tenses, with one idea in each sentence.
- **Vocabulary.** The text uses ordinary technical words. It does not limit itself to the approved words of the STE dictionary. Where an approved word is natural, the text uses it. The dictionary has its own copyright, and thus this repository does not include it.
- **Words with another meaning.** Some words have an approved STE meaning that is different from their meaning in software. The documentation avoids three of these words (refer to [Words that the documentation avoids](#words-that-the-documentation-avoids)).
- **Words for requirements.** The [RESTful API Guide](RESTFUL-API-GUIDE.md) uses MUST, MUST NOT, SHOULD, SHOULD NOT and MAY in capitals, as RFC 2119 and RFC 8174 define them. They are defined terms of the guide. The documentation keeps them, although STE does not approve "should" and "may".
- **Spelling.** The documentation uses American English.
- **Text that does not change.** Source code, command lines, output of programs, quotations and the titles of works keep their original form. The text that programs print does not follow these rules. Examples are the output of `--help`, error messages and resource files.
- **The README.** The file [README.md](../README.md) is also a page for the brand of the project. Its text keeps the style that the owner chose, and this style can differ from these rules.

### Words that the documentation avoids

| Word | Reason | Use this |
| --- | --- | --- |
| view | In software, a view is a part of the user interface. STE approves "view" only for the ability to see something. | "UI component". The names of types (for example `MainView`) and the term "view model" stay. |
| solution | In software, a solution is a set of projects that a tool builds together. STE approves "solution" for a liquid with a dissolved material and for the answer to a problem. | The name of the file, for example `Zametek.ProjectPlan.slnx`. |
| code | In software, code is the text of a program. STE approves "code" for a sequence of symbols that identifies something. A "status code" or an "error code" has this meaning and stays. | "Source code". |

## Terms for plans

| Term | Meaning |
| --- | --- |
| plan | The data of a project plan. The application stores a plan in a `.zpp` file. The HTTP API uses the word "project" for a plan. |
| project | In most documents, a .NET project: one `.csproj` file with the source files that belong to it. In the HTTP API, a plan. |
| scenario | A version of a plan. A plan has one or more scenarios. |
| tracked scenario | A scenario that the scenario chart shows as a point. Tracking does only this. It is not the progress tracking of activities. |
| current scenario | The scenario that was open when a user saved the plan. `zpp` loads it when a request does not name a scenario. A plan has a maximum of one current scenario. |
| activity | A task in a plan. |
| compile | To calculate the schedule of a scenario with the vertex-graph compiler. The result is the compilation (`GraphCompilation`). |
| build cascade | The sequence of the seven `Build*` methods that `RunBuildCascade()` calls after a compile. It makes the arrow graph, the vertex graph, the series for resources and tracking, and the three kinds of metrics. |
| output | A result that the desktop application, `zpp` or the server makes from a compiled plan. Examples are the metrics, a chart, a graph and an exported plan. |
| metrics | The numbers that describe a compiled scenario. There are network, risk and financial metrics. |
| chart | A plot of data, for example the Gantt chart, the earned value chart, the resource chart or the scenario chart. |
| graph | A diagram of the activities and their dependencies. The arrow graph is activity-on-arrow. The vertex graph is activity-on-vertex. |

## Terms for the software

| Term | Meaning |
| --- | --- |
| host | A project that starts the shared parts of the application on one kind of platform. `Zametek.ProjectPlan.Desktop` is the desktop host. `Zametek.ProjectPlan.Browser` is the web host. [TODO](TODO.md) also calls a host a "head". |
| view model | An object that holds the state and the logic of a part of the UI, with no UI elements of its own. |
| core | The view model `CoreViewModel`. It owns the editable state of a plan, the compiler and the outputs that the compiler gives. |
| manager | A view model that subscribes to the core and rebuilds its own outputs when an input changes. The outputs are plots, graphs and grids. |
| engine | The library `Zametek.Engine.ProjectPlan`. It runs a job with no user interface. `zpp` and `zpp serve` use it. |
| `zpp` | The command line tool of the repository. It has no user interface. |
| `zpp serve` | The mode of `zpp` that starts a web server with an HTTP API. |
| `zpp --server` | The option that sends the jobs of `zpp` to a server. |
| job | One request for work. A job loads a plan, compiles it and makes the requested outputs. |
| warm server | A server that did its warm-up with a sample plan at start-up. A job on a warm server does not pay the start-up cost of `zpp`. |
| problem | An error response in the format of RFC 9457 (`application/problem+json`). The `type` of a problem is a URI that points to a section of [API.md](API.md). |
| deviation | A rule of the [RESTful API Guide](RESTFUL-API-GUIDE.md) that the API does not follow. [API.md](API.md) and [openapi.yaml](openapi.yaml) record each deviation. |

## Terms for the reactive pipeline

[Architecture](ARCHITECTURE.md) explains these terms in full.

| Term | Meaning |
| --- | --- |
| emission | A value that moves through an observable pipeline. |
| emission time | The moment when a state change pushes a value into a pipeline. The operators before `ObserveOn` run at this time. |
| delivery time | The later moment when the scheduler runs the subscriber. The operators after `ObserveOn` run at this time. |
| payload | The value that a pipeline delivers to a subscriber. It is a snapshot from the emission time, and thus it can be stale. |
| gate | In a pipeline, a check that suppresses emissions, for example `.Where(_ => !IsBulkUpdating)` or `MuteWhile`. In [TODO](TODO.md), it is a check that the pre-commit hook runs. It is also a condition that must be true before a task can start, for example the release of a package. |
| bulk update | One logical operation that rewrites large parts of the model in sequence: a load, an import or a reset. |
| conflate | To keep only the latest of the values that a gate suppresses, and to replay it when the gate opens. `MuteWhile` does this. |
| settled signal | The counter `CompilationOutputRevision`. `RunBuildCascade()` increments it after it builds all the outputs. |

## Abbreviations

| Abbreviation | Meaning |
| --- | --- |
| CI | Continuous integration. |
| DAG | Directed acyclic graph. |
| DS | The display settings view model, `ProjectScenarioDisplaySettingsViewModel`. |
| GC | Garbage collector. |
| R2R | ReadyToRun, a form of ahead-of-time compilation in .NET. |
| RID | Runtime identifier, for example `win-x64`. |
