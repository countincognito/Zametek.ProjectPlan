# The project file

A project file (`.zpp`) is a JSON document. The application, `zpp` and `zpp serve` read it and write it. A program can also write a plan as JSON. It can then give the plan to `zpp -i`, or send it to `POST /v1/projects/compile`. A plan that a program writes keeps its ids. An import gives new ids.

This page gives the rules of the format that a schema cannot say. The [glossary](GLOSSARY.md) gives the terms.

## The schema

[`zpp.schema.json`](zpp.schema.json) is the JSON Schema (draft 2020-12) of the file. `zpp serve` also gives it, at [`GET /v1/schemas/project`](API.md#fetch-the-schema-of-a-project-file).

- The schema describes the latest version of the format, `v0.6.2`. `zpp` and the application also read the older versions, and they save each project in the latest version. Thus, `zpp -i old.zpp -o new.zpp` changes a file to the latest version.
- The schema is strict about the names of the members. `zpp` ignores a member that it does not know, and it finds a name in capitals or in small letters. Thus, a misspelt member changes nothing, and `zpp` does not tell you. Check a plan against the schema before you send it.
- The schema requires only the members that a plan cannot do without. These are the version, and the ids of the nodes, the files, the activities, the resources and the work streams. `zpp` gives each other member its default. A scenario with no `GraphSettings`, or with an empty list in them, gets the default list, as an import does.
- An enumeration is a number. The description of each enumeration in the schema gives the name of each number.
- `zpp` writes no member that is `null`. A program can leave out a member that has no value.
- A test makes the schema from the classes that `zpp` writes the file from. Thus, the schema and `zpp` always agree. When the classes change, set `ZPP_REGENERATE_SCHEMA` to `1`, and run the test `ProjectFileSchemaTests.Regenerate`.

## A plan that a program can write

This plan has one scenario with three activities in a chain:

```json
{
  "Version": "v0.6.2",
  "Root": "6f1c2a3b-0000-4000-8000-000000000001",
  "Current": "6f1c2a3b-0000-4000-8000-000000000002",
  "Nodes": [
    {
      "Id": "6f1c2a3b-0000-4000-8000-000000000002",
      "ParentId": "6f1c2a3b-0000-4000-8000-000000000001",
      "NodeType": 0,
      "Name": "Plan"
    }
  ],
  "Files": [
    {
      "NodeId": "6f1c2a3b-0000-4000-8000-000000000002",
      "Scenario": {
        "ProjectStart": "2026-11-02T00:00:00+00:00",
        "DependentActivities": [
          { "Activity": { "Id": 1, "Name": "Design", "MostLikelyDuration": 5 } },
          { "Activity": { "Id": 2, "Name": "Build", "Duration": 3 }, "Dependencies": [1] },
          { "Activity": { "Id": 3, "Name": "Test", "OptimisticDuration": 2, "MostLikelyDuration": 4, "PessimisticDuration": 12 }, "Dependencies": [2] }
        ]
      }
    }
  ]
}
```

`zpp` schedules the design for 5 days and the build for 3 days. The test has the estimates 2, 4 and 12, and thus `zpp` schedules it for 5 days (refer to the text that follows). The plan finishes on day 13. A test of `zpp` compiles this plan, and it makes sure that the result is the same.

## The tree of scenarios

- `Root` is the id of the root of the tree. The root is not a node. A node at the top of the tree has the id of the root as its `ParentId`.
- `Nodes` has each folder and each scenario. `NodeType` is `0` for a scenario and `1` for a folder. `ParentId` is the id of the folder that the node is in.
- `Files` has the data of each scenario: one item for each scenario, with the id of its node as `NodeId`. A folder has no item.
- `Current` is the id of the scenario that `zpp` and the application load. When no item of `Files` has this id, they load the scenario of the last node, if it has an item.
- A plan with no nodes gets one new, empty scenario. Then `zpp` ignores `Files`.
- `Tags` has the labels of the nodes.

## The durations of an activity

An activity has three estimates of its duration, in working days: `OptimisticDuration` (O), `MostLikelyDuration` (ML) and `PessimisticDuration` (P). The compiler schedules the expected duration, (O + 4 × ML + P + 3) / 6, in whole days. `Duration` holds the expected duration.

- An activity with no `MostLikelyDuration` has `Duration` as its most likely duration.
- An activity with no `OptimisticDuration` or no `PessimisticDuration` has ML in its place. An activity with only ML has no uncertainty, and its duration is ML.
- An optimistic duration above ML is ML, and a pessimistic duration below ML is ML. A most likely duration below 0 is 0.
- `zpp` calculates `Duration` again when it reads the file. Thus, set `MostLikelyDuration`. When an activity has `MostLikelyDuration`, a change to `Duration` alone changes nothing. A plan that has no estimates can set `Duration` and leave out the estimates.

## The values that zpp calculates

The file holds the result of the last compile. `zpp` calculates these values again when it compiles a scenario, and a program can leave them out:

- In an activity: `Duration`, `EarliestStartTime`, `LatestFinishTime`, `FreeSlack` and `AllocatedToResources`.
- In a dependent activity: `ResourceDependencies` and `Successors`.
- In a scenario: `Metrics` and `ResourceMetrics`.

`SimulationSummary` is the result of the last run of the simulation. A scenario without it has no result. Refer to [Simulate the schedule](COMMAND-LINE.md#simulate-the-schedule).

`zpp` compiles only the scenario that it loads, and the tracked scenarios when you ask it to. The scenario chart shows the metrics that the file holds for each other tracked scenario. Thus, a program that changes a scenario and does not compile it leaves its metrics stale. `zpp --compile-tracked` and `compileTracked` in the API compile each tracked scenario again.

## Times and constraints

- A time of an activity is a number of working days from `ProjectStart`. The calendar of the scenario gives the working days.
- `MinimumEarliestStartTime` is the earliest day on which an activity can start. `MinimumEarliestStartDateTime` gives the same constraint as a date. When an activity has both, the date takes the place of the number.
- `MaximumLatestFinishTime` and `MaximumLatestFinishDateTime` are the latest day by which an activity must finish, with the same rule.
- A date is an ISO 8601 date and time with its offset from UTC, for example `2026-11-02T00:00:00+00:00`. An id of a node is a GUID. An id of an activity, a resource or a work stream is a whole number.
