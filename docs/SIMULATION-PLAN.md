# Plan for PERT estimates and the schedule simulation

This document is the plan to add three-point (PERT) estimates and a Monte Carlo schedule simulation to the application. It records the decisions, the design, the phases and the results of the measurements. The work is on the branch `mc-sim` of this repository and of the repository Zametek.Maths.Graphs.

Status: phase 0 (measure and prototype) is finished. The maintainer decided the four points that it raised (refer to [Decisions of phase 0](#decisions-of-phase-0)). Phase 1 is not started. The sections about phases 1 to 4 describe what the team intends to do.

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
- **Samplers.** The core has four samplers: beta-PERT, triangular, normal and uniform. The normal sampler stays in the range from O to P. Each sampler rounds its result to the nearest whole day.
- **Two betas.** The reference tool uses a beta with the PERT mean and the PERT variance. The usual modified PERT beta has its mode at ML and a shape value of 4, and its variance is larger. The two give different results (refer to [The reference sample](#the-reference-sample)). The default is the beta of the reference tool, and the core also offers the modified PERT beta.
- **Random numbers.** The core uses a generator that the library specifies. It does not use `System.Random`, because the sequence of that class exists only in .NET.
- **Seeds.** The core splits the iterations into fixed chunks. The seed of a chunk comes from the main seed and the number of the chunk. Thus, the result is the same for any number of threads.
- **Each iteration.** It sets the durations, schedules the resources and calculates the critical path. It returns the finish time, the critical activities and the path that sets the finish time. A link between two activities that share a resource is a different kind of link in the path.
- **Failures.** When the resource scheduler stalls, the core counts the iteration as failed and continues. When a user cancels, the core stops between iterations.

### The results and the desktop panel (phase 3)

- The application makes the statistics, the percentiles, the histogram bins, the path table and the criticality indexes from the raw iterations.
- When an iteration has more than one critical path, its weight is split equally among those paths. The path shares then add to 100 %.
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

- **Speed.** An iteration that schedules the resources again costs as much as a compile. Phase 0 measured this cost. It is small for a plan like the sample plan and large for a plan with 500 or more activities. The panel shows progress and the time that remains, and it has a Cancel button. The server limits the number of iterations or the time of a simulation.
- **Ties.** Whole days make a tie between paths much more likely than in the reference tool. Phase 0 measured about 20 % of the iterations, against 0.2 %. A "Multiple" group of this size is not useful, and thus a tie splits the weight of the iteration among the tied paths.
- **Floating-point numbers.** The same seed gives the same result on one platform. Windows, Linux and Rust can differ in the last bit of `Log` and `Pow`. Tests that compare platforms must use a tolerance.
- **The browser version.** It has one thread. The simulation must run in small batches so that the UI does not freeze.

## Phase 0: the results

Phase 0 finished on 2026-10-15. Its work is in the library repository, on the branch `mc-sim`, in the commit `1a73f3a`. It is a spike in the test project `Zametek.Maths.Graphs.Compilers.Tests`, in the folder `Compilers/SimulationSpike`. The library itself did not change.

The tests that check results run with the other tests. The tests that measure are explicit, and a reader can run them again. The comments at the top of `SimulationSpikeTests.cs` give the command.

The measurements use two kinds of plan. One is the sample plan of this repository ([sample.zpp](assets/sample.zpp), with 46 activities and 15 resources). The others are layered plans of 100 to 1,000 activities. The machine has two cores and four logical processors. Each time is the best of three rounds after a warm-up. Repeated runs differ by up to a quarter, and thus the times show proportions and not exact values.

### The cost of an iteration

An iteration that schedules the resources again costs about as much as a compile. The lean path is a scheduling pass that keeps only the steps that change the finish time. It saves 10 to 25 %.

| Plan | Activities | Resources | A new compile | The lean path | 10,000 iterations on one thread |
| --- | --- | --- | --- | --- | --- |
| Sample plan | 46 | 15 | 1.09 ms | 0.82 ms | 8 s |
| Layered | 100 | 10 | 1.95 ms | 1.49 ms | 15 s |
| Layered | 250 | 20 | 9.04 ms | 7.64 ms | 76 s |
| Layered | 500 | 20 | 33.9 ms | 30.2 ms | 5 minutes |
| Layered | 1,000 | 20 | 118 ms | 107 ms | 18 minutes |

The cost grows about with the square of the number of activities. The priority list is 48 to 78 % of an iteration. The tick loop is 9 to 24 %, and the two copies of the graph are 2 to 15 %. The first and the second critical-path calculations are 6 % or less together. Thus, a faster iteration needs a faster priority list and a faster scheduler, and removing the checks around the compile does not help much.

The library limit is 2,000 activities. The notes of the library give about 0.4 seconds for a compile at this size, and the table suggests the same for an iteration. Thus, 10,000 iterations take more than an hour on one thread.

### The threads

The design for the threads works. Each chunk of 64 iterations has its own random number generator, and its seed comes from the main seed and the number of the chunk. A test shows that one thread and three threads give the same results.

The speed does not scale well on this machine. The table gives the iterations for each second.

| Plan | 1 thread | 2 threads | 3 threads | 4 threads |
| --- | --- | --- | --- | --- |
| Sample plan | 1,173 | 1,960 | 2,778 | 3,093 |
| Layered, 250 activities | 128 | 207 | 228 | 227 |

The machine has two physical cores. Each iteration also allocates much memory (4 to 72 MB for one compile, in the notes of the library). Thus, the table shows the limit of this machine, and a machine with more cores can scale better. A kernel that allocates less can also scale better.

### The lean path against the compile

The lean path gives the finish time and the critical activities of a compile. A test shows this for the sample plan with and without its resources, for a layered plan and for the reference sample. Each test uses one lean scheduler for all the random durations, and thus it also finds state that an iteration leaves behind. The test fails when the lean path leaves out the second critical-path calculation or the reset of the resource state. Thus, the test can detect an error.

When a plan has no resources, the compile still schedules onto synthetic resources. A tie between the finish of one activity and the start of the next can then make a chain of resource links critical. The finish time is the same, but the compile marks more activities as critical than the plain critical-path calculation does. This happened in 4 of 60 iterations of the reference sample. The shortcut for plans with no resources (stop after the first critical-path calculation) gives the same finish time and a smaller critical set.

### The reference sample

A replica of the arithmetic of the reference tool, in hundredths of a day, reproduces its published results. The table has 100,000 iterations of the replica and the whole-day version. The path shares for whole days are in the next paragraph.

| Arithmetic | Mean | Standard deviation | P80 | A > D > F > H > L > O | A > D > I > M > O | A > E > J > M > O | More than one path |
| --- | --- | --- | --- | --- | --- | --- | --- |
| The reference tool (published, 10,000 iterations) | 52.39 | 1.59 | not given | 70.1 % | 24.6 % | 5.2 % | 0.2 % |
| The replica, in hundredths | 52.38 | 1.60 | 53.7 | 70.0 % | 24.6 % | 5.3 % | 0.2 % |
| Whole days | 52.41 | 1.71 | 54 | 59.6 % | 17.5 % | about 3 % | 19.5 % |

Rounding to whole days has two effects. The standard deviation is 7 % larger, because each activity adds noise from its rounding. About 20 % of the iterations have more than one critical path, because many paths now have exactly the same length.

The two betas give different results. With the usual modified PERT beta (shape value 4), the standard deviation is 1.76 in the reference arithmetic. With the beta of the reference tool, it is 1.60. The P80 is 53.9 and 53.7. The triangular and uniform distributions are wider (standard deviations of 2.06 and 2.82).

### Scheduling the resources again, or keeping their order

The alternative to the scheduling of the resources in each iteration is to keep the resource order of the compiled plan as extra dependencies. Each iteration is then only a critical-path calculation. The table uses the same random durations for both modes.

| Plan | Mode | Mean finish | Standard deviation | P80 | P90 | One iteration |
| --- | --- | --- | --- | --- | --- | --- |
| Sample plan | Schedule again | 226.08 | 8.54 | 233 | 237 | 0.8 ms |
| Sample plan | Keep the order | 226.97 | 8.49 | 234 | 238 | 1.9 µs |
| Layered, 250 activities | Schedule again | 82.92 | 2.60 | 85 | 86 | 7.6 ms |
| Layered, 250 activities | Keep the order | 85.08 | 2.10 | 87 | 88 | 5.6 µs |

The finish times differ in 48 % of the iterations of the sample plan and in 75 % of those of the layered plan. For the sample plan, the mean differs by 0.4 % and the standard deviation by 0.6 %. For the layered plan, the resources decide the schedule. The mean differs by 2.6 % and the standard deviation by 24 %.

Keeping the order is 430 to 1,400 times faster. In both plans it gives a larger mean. The likely reason is that the kept order cannot give a resource to a different activity when a duration is shorter.

### What the results mean

- Scheduling the resources in each iteration is practical for plans like the sample plan. Ten thousand iterations take 8 seconds on one thread, or 3 seconds on four.
- It is not practical for plans with 500 or more activities. Ten thousand iterations take 5 minutes or more.
- The lean path is not enough to change this. A flat kernel for the priority list and the scheduler, on arrays and without copies of the graph, is the way to a large gain. The team did not build it in phase 0, and thus its gain is not measured.
- The mode that keeps the resource order is nearly free, and it is accurate for plans that resources do not decide.
- A tie between paths is common with whole days, and the "Multiple" group of the reference tool is too large to be useful.

### Decisions of phase 0

The maintainer made these decisions on 2026-10-15, after the results above.

1. **The default beta.** The default is the beta of the reference tool. It matches the "Expected" and "Standard deviation" columns of the grid, and it reproduces the reference tool. The core also offers the modified PERT beta (shape value 4), which has its mode at ML.
2. **The attribution of a tie.** The weight of an iteration with more than one critical path is split equally among the tied paths. The path shares add to 100 %, and the criticality index of an activity stays correct.
3. **The resource mode.** There is one mode. Each iteration schedules the resources again. The plan does not add a mode that keeps the resource order. A large plan therefore takes minutes or more. The panel shows progress and the time that remains, from the iterations that are done, and it has a Cancel button. The server limits the number of iterations or the time of a simulation.
4. **A flat kernel.** Phase 2 does not build it. Phase 2 uses the lean path, which the tests hold to the compile. The team can build the kernel later if the speed is a problem.

The proposal for the number of iterations is 5,000 by default. The expected time for the sample plan is a few seconds. A limit of 1,000,000 stays, and phase 3 can change the default.

## Performance work to explore later

The maintainer decided that phase 2 builds none of the work in this section. The section records what phase 0 learned, so that the team can start from facts if speed becomes a priority. The library commits `1a73f3a` and `86d6c1e` (branch `mc-sim`) have the measurements. The efforts below are estimates, and nobody measured them.

### Where the time goes

The priority list is about half of one iteration in a profile of the list alone. Removing it completely made an iteration faster. The factor was 3.1 for the sample plan, 3.2 for a layered plan of 100 activities and 5.5 for a plan of 250 activities. This is the ceiling for any exact change to the list. The profile and the end-to-end figure differ for the sample plan, and nobody has explained the difference.

Inside the list, 80 to 85 % of the time is the incremental update of the critical-path engine. The engine does one update for each activity that the list places. The two scans over all activities are only 4 to 13 %, and the copy of the graph is 1 to 4 %. The incremental update never gave up and fell back to a full calculation in the plans of phase 0. Thus, a better structure for the selection would win little. A faster list needs a faster incremental update.

### The possibilities

| Possibility | Rough effort | Likely gain | Effect on the accuracy |
| --- | --- | --- | --- |
| A mode that keeps the resource order of the compiled plan | 1 to 2 days, with the UI | 430 to 1,400 times | None for plans that the resources do not decide. For a plan that they decide, the mean moved by 2.6 % and the standard deviation by 24 % in one measurement. |
| A faster incremental update in the critical-path engine (about 600 lines of source code) | About a week in C# and a few days for the Rust port | Up to 2 to 3 times for the sample plan. Not proven. | None. The same list comes out. |
| A flat kernel for the list and the scheduler, on arrays and without copies of the graph | 2 to 4 weeks for each language | Not measured | None, if the tests show that it gives the same schedules. |
| An approximate priority list | A few days | Up to the ceiling above | A loss that nobody can predict. The team does not recommend it. |

The first possibility is the cheapest and the largest. The maintainer declined it for now, and it stays possible later. It changes only the panel and the call to the library, because each iteration is then a critical-path calculation with extra dependencies.

The approximations of phase 0 show why the last possibility is not safe. The frozen list is the exact list for the most likely durations, used in every iteration. It gave a different finish in about 30 % of the iterations of the sample plan and of a plan of 100 activities. It was exact for a plan of 250 activities.

The static order sorts the activities once by total slack and then by earliest start. It was exact for the sample plan and for 250 activities. In the plan of 100 activities, the resources decide the schedule. There it was 8.5 % too late on average, in 99.9 % of the iterations. Thus, the same shortcut is exact for one plan and wrong for another.

### What keeps these possibilities cheap

- The scheduling of one iteration stays behind one internal call of the library. The app, the panel, `zpp` and the server then do not change when the team replaces what is behind the call.
- The library does not expose its builder or its copies of the graph in the simulation API.
- The comparison between the lean path and a full compile stays in the test suite. The test `PriorityListEquivalenceTests` already pins the exact priority list, ties included. Together they are the oracle for any exact change.
- The measurement tests of phase 0 stay too, and a later change can show its gain with them.
- Each change is necessary twice, because the Rust port must give the same iterations for the same seed.
- A new kernel must also give the critical activities that a compile gives. For a plan with no resources, the compile uses synthetic resources. Ties can then mark more activities as critical than the plain critical-path calculation does.
