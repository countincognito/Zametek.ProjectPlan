# Plan for PERT estimates and the schedule simulation

This document is the plan to add three-point (PERT) estimates and a Monte Carlo schedule simulation to the application. It records the decisions, the design, the phases and the results of the measurements. The work is on the branch `mc-sim` of this repository and of the repository Zametek.Maths.Graphs.

Status: phase 0 (measure and prototype) is in progress. Nothing else is started. The sections that follow phase 0 describe what the team intends to do. They can change when the measurements of phase 0 show a problem.

## What the work gives to a user

A user gives each activity three estimates of its duration. The estimates are optimistic (O), most likely (ML) and pessimistic (P). The application then does two things:

- It schedules the plan with the PERT expected duration of each activity. The expected duration is (O + 4 × ML + P) / 6.
- It runs a Monte Carlo simulation. Each iteration draws a random duration for each activity, schedules the plan again and records the finish time. Many iterations show how much the finish time can change.

The simulation gives these results:

- the minimum, the maximum, the mean and the standard deviation of the finish time
- the percentiles of the finish time, for example the P80
- the chance that the plan finishes by a target date
- how often each path through the plan was the critical path
- how often each activity was critical (its criticality index)
- a histogram of the finish times, with each bar split by critical path, and a cumulative curve (S-curve).

## The reference tool

The work follows a tool by C. Yamahata. The tool has a [CodePen version](https://codepen.io/Christophe-Yamahata/full/jENXKrL) and an [Observable notebook](https://observablehq.com/@christophe-yamahata/pert-monte-carlo-schedule-analysis). The CodePen version is the newer one.

The tool works in this way:

- Each activity has O, ML and P. The tool calculates the mean and the variance with the PERT formulas. The variance is ((P − O) / 6)².
- A user selects one of four distributions: beta-PERT, triangular, normal or uniform.
- Each iteration draws a duration for each activity and makes a full critical-path calculation. It records the finish time and the critical path. When two paths tie, it records "multiple critical paths".
- The tool shows a stacked histogram, a dashed line at the mean and an S-curve on a second axis. It also gives the statistics, a table of the critical paths, CSV files and SVG files.

The tool has these weaknesses, and the application must not copy them:

- It has no seed, and thus a user cannot repeat a result.
- Its normal distribution can give a negative duration.
- It has no percentiles and no chance of meeting a target date.
- It ignores resources.
- Each different path is a separate legend entry. The legend is not readable for a real plan.

### The test data from the reference tool

The sample of the tool gives numbers that this work uses as a check. The sample has 15 activities (A to O). The estimates are in the table. A `Finish` milestone follows K, N and O.

| Activity | Predecessors | O | ML | P |
| --- | --- | --- | --- | --- |
| A | Start | 2 | 4 | 6 |
| B | Start | 4 | 5 | 12 |
| C | B | 3 | 5 | 7 |
| D | A | 7 | 10 | 13 |
| E | A | 11 | 12 | 13 |
| F | B, D | 5 | 6 | 7 |
| G | C | 9 | 10 | 11 |
| H | C, F | 5 | 7 | 9 |
| I | D | 7 | 9 | 11 |
| J | E | 1 | 5 | 9 |
| K | G | 15 | 17 | 19 |
| L | H | 6 | 8 | 10 |
| M | I, J | 9 | 10 | 17 |
| N | G | 12 | 14 | 16 |
| O | L, M | 16 | 17 | 18 |

The schedule with the PERT mean durations has a finish time of 52.00. Its critical path is A > D > F > H > L > O. With the beta-PERT distribution and 10,000 iterations, the tool gave these results.

| Result | Value |
| --- | --- |
| Mean finish time | 52.39 |
| Standard deviation | 1.59 |
| Minimum and maximum | 46.82 and 59.29 |
| Critical path A > D > F > H > L > O | 70.1 % of the iterations |
| Critical path A > D > I > M > O | 24.6 % of the iterations |
| Critical path A > E > J > M > O | 5.2 % of the iterations |
| More than one critical path | 0.2 % of the iterations |

The tool draws real numbers. The application schedules whole days, and thus the results of the application are not equal to these numbers. The tests use a calculation with real numbers to check the samplers, and they use a tolerance for the scheduler that rounds.

## The decisions

The maintainer made these decisions on 2026-10-15.

- **Estimates.** The existing activity duration becomes the most likely duration (ML). Two new fields hold the optimistic (O) and the pessimistic (P) duration. O and P follow ML until a user edits them. After a direct edit, they keep their own value.
- **Rules for O and P.** The rule is 0 ≤ O ≤ ML ≤ P. If an edit of ML would put ML outside the range, the application moves the value that ML crossed to ML. A "Reset to ML" command makes O and P follow ML again. A milestone has a duration of 0.
- **The scheduled duration.** The compiler schedules the PERT expected duration, rounded to the nearest whole day, with halves up. When O = ML = P, the expected duration is ML, and thus an existing plan does not change.
- **Resources.** Each iteration schedules the resources again, with the same scheduler as a normal compile. Thus, each iteration draws whole days.
- **Where the simulation lives.** The simulation core goes into the library Zametek.Maths.Graphs, as release 3.3.0. The library returns the raw result of each iteration. The application makes the statistics, the charts and the CSV files.
- **The Rust port.** The Rust port of the library gives the same iterations for the same seed. Both ports use one specified random number generator and the same sampling algorithms. The tests allow for a difference of one unit in the last place in `Log` and `Pow` between platforms.
- **The first release.** It has the desktop panel, the outputs of `zpp` and the API of `zpp serve`. It does not have the activity-on-node box for the vertex graph.

## The design

### The estimates (phase 1)

- The file format moves to v0_6_2. The existing member `Duration` holds ML. Two new members hold the own value of O and of P, and null means that the value follows ML.
- A new format is necessary. An older application that reads the new file would schedule ML and not the expected duration, and the schedule would change without a message.
- The activities grid shows "Most likely", "Optimistic" and "Pessimistic" and the read-only columns "Expected" and "Standard deviation". The edit dialog and the XLSX export get the same fields.
- An import from MS Project or Excel puts its duration into ML.
- The managed activity gives the expected duration to the compiler as the `Duration` of the library activity. Thus, the compiler and the library do not change for this phase.

### The simulation core (phase 2)

- **Input.** The core takes the compiled plan: the activities, their dependencies, the resources and the three estimates of each activity. It copies this data once, and each iteration then changes only the durations.
- **Samplers.** The core has four samplers: beta-PERT, triangular, normal and uniform. The beta-PERT sampler has a shape value of 4 by default. The normal sampler stays in the range from O to P. Each sampler rounds its result to the nearest whole day.
- **Random numbers.** The core uses a generator that the library specifies. It does not use `System.Random`, because the sequence of that class exists only in .NET.
- **Seeds.** The core splits the iterations into fixed chunks. The seed of a chunk comes from the main seed and the number of the chunk. Thus, the result is the same for any number of threads.
- **Each iteration.** It sets the durations, schedules the resources and calculates the critical path. It returns the finish time, the critical activities and the path that sets the finish time. A link between two activities that share a resource is a different kind of link in the path.
- **Failures.** When the resource scheduler stalls, the core counts the iteration as failed and continues. When a user cancels, the core stops between iterations.

### The results and the desktop panel (phase 3)

- The application makes the statistics, the percentiles, the histogram bins, the path table and the criticality indexes from the raw iterations.
- The panel "Schedule Simulation" has the controls (distribution, number of iterations, seed, target date, Run and Cancel) and a progress display.
- The chart is a stacked histogram with the eight most frequent paths and a group "Other". It has lines at the mean, at P50, P80 and P90, and at the compiled finish. The S-curve uses the axis on the right.
- The panel does not run inside the build cascade. It copies the compiled plan under the lock and then runs on the task pool. It marks a result as stale when the plan compiles again. Refer to [Architecture](ARCHITECTURE.md), section 7.
- The existing image export and copy work for the new chart. A new export makes the CSV files.

### The outputs of zpp and the API of zpp serve (phase 4)

- `zpp` gets options for the iteration count, the distribution, the seed and the simulation outputs. The statistics go into the metrics.
- The server needs a limit for the iteration count or for the time of a simulation. The reason is that each iteration schedules the resources again.
- A change to the API changes [openapi.yaml](openapi.yaml), [API.md](API.md) and the contract tests in the same commit.
- The parity tests compare the output of `zpp` and of the server for the simulation, byte for byte.

## The phases

The team stops after each phase for a review.

| Phase | Work | Where |
| --- | --- | --- |
| 0 | Measure the cost of one iteration. Prototype a lean path. Check the prototype against the reference tool. Decide the default iteration count. | Library and this repository |
| 1 | The estimates: the new file format, the rules, the grid and the dialog, the imports and the export. | This repository |
| 2 | The simulation core, the random number generator and the samplers, with tests, then release 3.3.0 and the Rust port. | Library |
| 3 | The desktop panel. | This repository |
| 4 | The options of `zpp`, then the API of `zpp serve`, with the documentation. | This repository |

## The risks

- **Speed.** An iteration that schedules the resources again costs as much as a compile. Phase 0 measures this cost before the team commits to the design.
- **Ties.** Whole days make a tie between paths more likely than in the reference tool. The chart groups the less frequent paths as "Other".
- **Floating-point numbers.** The same seed gives the same result on one platform. Windows, Linux and Rust can differ in the last bit of `Log` and `Pow`. Tests that compare platforms must use a tolerance.
- **The browser version.** It has one thread. The simulation must run in small batches so that the UI does not freeze.

## Phase 0

This section records the measurements and the decisions of phase 0. The measurements are not yet in this document.
