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

- An optimistic duration above ML is ML, and a pessimistic duration below ML is ML. A most likely duration below 0 is 0. The schema gives each duration and each time of an activity a minimum of 0, because a value below 0 is probably a mistake.

- A duration above 100,000 days does not compile (`P0080`).

- `zpp` calculates `Duration` again when it reads the file. Thus, set `MostLikelyDuration`. When an activity has `MostLikelyDuration`, a change to `Duration` alone changes nothing. A plan that has no estimates can set `Duration` and leave out the estimates.

## The values that zpp calculates

The file holds the result of the last compile. `zpp` calculates these values again when it compiles a scenario, and a program can leave them out:

- In an activity: `Duration`, `EarliestStartTime`, `LatestFinishTime`, `FreeSlack` and `AllocatedToResources`.
- In a dependent activity: `ResourceDependencies` and `Successors`.
- In a scenario: `EffectiveProjectStart`, `Metrics` and `ResourceMetrics`.

`SimulationSummary` is the result of the last run of the simulation. A scenario without it has no result. Refer to [Simulate the schedule](COMMAND-LINE.md#simulate-the-schedule).

`zpp` compiles only the scenario that it loads, and the tracked scenarios when you ask it to. The scenario chart shows the metrics that the file holds for each other tracked scenario. Thus, a program that changes a scenario and does not compile it leaves its metrics stale. `zpp --compile-tracked` and `compileTracked` in the API compile each tracked scenario again.

## Times and constraints

- A time of an activity is a number of working days from the effective start. The effective start is the first working day on `ProjectStart` or after it. Day 0 is the effective start, and day n is the nth working day after it. The calendar of the scenario gives the working days (refer to [The calendar](#the-calendar)).

- `ProjectStart` is the start that you give, and `zpp` does not change it. `zpp` writes the effective start as `EffectiveProjectStart`, and it calculates it again when it reads the file. The metrics also give it: `projectStartDate` in the API, and `ProjectStartDate` in `zpp --metrics-format json`. Thus, with weekends, a plan that starts on a Saturday does its first day of work on the Monday after it.

- A change to the calendar can move the effective start. For example, a holiday on `ProjectStart` moves it to the next working day. When the holiday goes, the effective start goes back to `ProjectStart`.

- An activity that starts at time n and has a duration of d days does its work on days n to n + d − 1. Its finish time is n + d, which is the start of the day after its last day of work.

- `MinimumEarliestStartTime` is the earliest day on which an activity can start. `MinimumEarliestStartDateTime` gives the same constraint as the date of that day. When an activity has both, the date takes the place of the number.

- `MaximumLatestFinishTime` is the latest finish time of an activity. `MaximumLatestFinishDateTime` gives it as a date: the date of the first day after the last day of work. For example, with weekends, to let an activity finish its work on Friday 2027-01-15, give Monday 2027-01-18. `UseClassicDates` does not change this, because it changes only how the application and `zpp` show the date of a finish. When an activity has both, the date takes the place of the number.

- A constraint date that is not a working day counts as the next working day.

- An activity cannot have both `MinimumFreeSlack` and a maximum latest finish: the compile then gives `P0030`. The compile also gives `P0030` when the minimum earliest start and the duration go past the maximum latest finish. Refer to [the codes of the compiler](API.md#codes-of-the-compiler).

- A date is an ISO 8601 date and time with its offset from UTC, for example `2026-11-02T00:00:00+00:00`. An id of a node is a GUID. An id of an activity, a resource or a work stream is a whole number.

## What zpp checks before the load

`zpp` and `zpp serve` check a plan before they load it. They refuse a plan that has one of these problems. `zpp` then gives exit code 1, with one line for each problem on its standard error stream. `zpp serve` gives `422` `project-not-readable`, with each problem at its place in the project.

- `required`: the scenario that the plan loads has no `ProjectStart`.

- `outOfRange`: `ProjectStart` is not from 1900-01-01 to 2999-12-31, or a constraint date is more than 100,000 days from `ProjectStart`.

- `notAllowed`: a choice is not one of the numbers that the schema gives, for example a `NonWorkingDayMode` of 7.

- `duplicateId`: two activities, two resources or two work streams of the scenario have the same `Id`.

- `hasNoData`: the plan has nodes, but `Files` has no item for the scenario that the plan loads (refer to [The tree of scenarios](#the-tree-of-scenarios)).

The check looks at the scenario that the plan loads, and at the nodes and the display settings of the project. A tracked scenario that the check refuses does not compile, and it has no metrics.

The check also counts the activities, the active resources and the work streams of the scenario. A plan that is out of the limits of the compiler gets `P0080` at once, without the load. Before, the load of a large plan took seconds before the compile refused it.

## The calendar

`NonWorkingDayMode` in the `DisplaySettings` of a scenario selects the days that are not working days:

- `0` (`None`): each day is a working day. A scenario that does not give the mode has this mode.

- `1` (`Weekends`): Saturday and Sunday are not working days. The holidays of the scenario have no effect.

- `2` (`CustomCalendar`): only the holidays in the `HolidaySettings` of the scenario are not working days. Thus, the weekends are working days, unless a holiday makes them days off. The default holidays of the application have a holiday for the weekends (`FREQ=WEEKLY;BYDAY=SA,SU`), Christmas Day and New Year's Day.

A holiday has a `RecurrencePattern`, which is a recurrence rule of iCalendar (RFC 5545). Its rule starts on its `StartDateTime`, or on `ProjectStart` when it has no `StartDateTime`. A holiday of one day has its date as `StartDateTime` and the rule `FREQ=DAILY;COUNT=1`.

The compile gives `P0080` when the calendar cannot give the dates of the plan:

- The calendar has no working day in the 366 days from `ProjectStart`. For example, a holiday with the rule `FREQ=DAILY` and no `COUNT` or `UNTIL` makes each day a day off.

- Before the schedule finishes, the calendar has more than 366 days in a row that are not working days.
