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

- The file format moves to v0_6_2. The member `Duration` of an activity keeps its meaning: it is the duration that the compiler schedules, which is the expected duration. Three new members hold the estimates: `MostLikelyDuration`, `OptimisticDuration` and `PessimisticDuration`.
- Each new member is optional, and a member that is null follows the one before it. No `MostLikelyDuration` is the `Duration`. No `OptimisticDuration` or `PessimisticDuration` is the most likely duration. Thus, a model that sets only `Duration` is an activity with no uncertainty. This is true for a file of an older version, for an import, and for a model that a test builds.
- `Duration` keeps its meaning for a reason. The graph labels, the Gantt chart, the earned value chart and the resource scheduling all read it from the same model, as the scheduled duration. A different meaning in the file would change them without a message.
- A new format is necessary. An older application that reads the new file would schedule ML and not the expected duration, and the schedule would change without a message. Going down to v0.6.1 drops the estimates and keeps `Duration`, and thus the older application schedules what this one scheduled.
- The activities grid shows the columns "Most Likely", "Optimistic" and "Pessimistic", which a user can edit, and the read-only columns "Expected" and "Std Dev". The old column "Duration" is now "Most Likely". The menu of the grid has "Reset Estimates", which makes the optimistic and pessimistic durations of the selected activities follow the most likely duration again. The table that "Copy Table" gives has the same columns.
- The edit dialog for many activities has no duration, and thus it does not change.
- The Excel export has three new columns, `MostLikelyDuration`, `OptimisticDuration` and `PessimisticDuration`. A blank cell is an estimate that nobody gave. The column `Duration` of a workbook is the expected duration. When a row has a most likely duration, the import uses the estimates, and it ignores the `Duration` of that row. A row with no most likely duration uses its `Duration`, as before.
- An import from MS Project puts its duration into ML. So does an import from an Excel workbook that has no estimate columns.
- The managed activity gives the expected duration to the compiler as the `Duration` of the library activity. Thus, the compiler and the library do not change for this phase.

How a managed activity behaves, as built:

- The activity writes the most likely duration, the two estimates that the user gave, and `Duration` together, under one lock. A compilation therefore never copies a mix of two edits.
- Setting `Duration` sets the most likely duration. The Gantt chart sets a duration when a user drags the end of a bar, and so it sets the most likely duration too. An activity with estimates of its own then shows the expected duration. This can differ from the length that the user dragged to. The maintainer wants to decide this again later (refer to the entry in [TODO](TODO.md)).
- A value outside the rule is limited to the nearest value inside it, and the activity reports an error for that property. A milestone (a most likely duration of 0) has an optimistic and a pessimistic duration of 0.
- When the most likely duration moves past an optimistic or pessimistic duration that the user gave, that duration moves to it. A value that the user did not give follows the most likely duration.
- An activity that is made from a model takes its estimates from the model, and it works its `Duration` out from them. When a model's `Duration` and its estimates disagree, the estimates win.
- The rules are in one place, `EstimateHelper`. The activity and the later simulation use it, and thus they cannot disagree.

An optimistic duration of 0 on an activity that takes time is allowed. A draw can then round to 0 days. Phase 2 decided this: the draw stays 0 (refer to [Phase 2: the results](#phase-2-the-results)).

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

### The panels, the saving and the command line (phase 5)

The maintainer asked for these changes after they used the panel of phase 3. The decisions in the answers of the maintainer are part of the plan.

- **Names.** The panel "Schedule Simulation" becomes "Simulation Chart".
- **Three docks.**
  - "Simulation" sits beside "Scenarios" by default. It has the name of the scenario that the simulation uses, the settings (the iterations, the distribution, the shape value for the modified beta-PERT, the seed, "New seed each run" and the target finish), the buttons Run Simulation and Cancel Simulation, and the progress bar with the time that remains. The progress and the warning for a result that is stale are fixed at the bottom.
  - "Simulation Chart" keeps its place and its menu (Reset, Copy Image, Save As and Export CSV Files).
  - "Simulation Metrics" sits beside "Resource Metrics" by default. It has the tabs "Statistics", "Critical paths" and "Criticalities". Each tab has "Copy Table" and keeps the layout of its columns, as "Resource Metrics" does. The tab "Critical paths" shows each path as activity IDs, in the order of the path, to save space. A tooltip has the names. The CSV files and the legend of the chart keep the names.
  - A saved dock layout does not have the new docks, and thus the application uses the default layout once.
- **The panels follow the scenario.** All three panels clear and reset when a scenario loads, when a project closes and when a new project starts, as the other panels do. A run that is in progress stops.
- **The title bar.** The title did not reset when a project closes or loads. A change to the project settings was necessary. The cause is the commit `8c109e7d`, which stopped the title pipeline from clearing its trigger. A value that does not change raises no event. A new test shows the fault, and the fix comes first.
- **Saving.** The scenario file holds the settings of the simulation (the iterations, the distribution, the shape value, the seed, "New seed each run" and the target) and, when it exists, the last result. The result has the statistics, the percentiles, the histogram, the critical paths and the criticalities. The histogram makes the chart possible after a load. The file does not hold the iterations. The core owns the data, as it owns `Metrics`, and the file format v0_6_2 has the members, because v0_6_2 is not released.
  - A result that a file holds is trusted. The application does the same with the metrics of a scenario that a file holds when the compile is off.
  - A change to a setting or a new result marks the scenario as changed. A load does not.
  - The Excel export has the sheets for the settings, the statistics, the histogram, the critical paths and the criticalities. The import reads them.
- **The command line.** `--simulate` becomes `--run-simulation`, and `zpp` does not run a simulation by default. The settings come from the input file. The seed is the saved seed, also when "New seed each run" is on, and thus a run repeats. The options for the iterations, the seed, the distribution, the shape value, the target and the CSV directory go. `--simulation-directory`, `--simulation-format` and `--simulation-size` stay for the outputs. The directory has the chart and the CSV files side by side.
  - When the input file has a saved result and `--run-simulation` is not present, `zpp` prints the statistics of the saved result after the metrics, and `--simulation-directory` draws it.
  - `-o` saves the settings and the result.
- **The API.** It follows `zpp`. The options object `simulation` becomes `runSimulation`. One output, `simulation`, takes a format and a size. The answer lists the chart and the CSV files as outputs, one for each file. The answer has `simulation` for the statistics of a run or of a saved result. A file with more iterations than the limit of the server is a problem with status 422.

## The phases

The team stops after each phase for a review.

| Phase | Work | Where |
| --- | --- | --- |
| 0 | Measure the cost of one iteration. Prototype a lean path. Check the prototype against the reference tool. Decide the default iteration count. | Library and this repository |
| 1 | The estimates: the new file format, the rules, the grid and the dialog, the imports and the export. | This repository |
| 2 | The simulation core, the random number generator and the samplers, with tests, then release 3.3.0 and the Rust port. Done (not published). | Library |
| 3 | The desktop panel. Done (not pushed). | This repository |
| 4 | The options of `zpp`, then the API of `zpp serve`, with the documentation. Done (not pushed). | This repository |
| 5 | The three docks, the title bar, the saving in the file and in Excel, and the new options of `zpp` and the API. Done (not pushed). | This repository |

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

## Phase 2: the results

Phase 2 finished on 2026-10-16. All the work is in the library repository, on the branch `mc-sim`, in the commits `e5a952c` (random numbers and samplers), `c6a68c9` (the simulator), `f239d36` (whole simulations in the baseline) and `df644cb` (the Rust port, the documentation and the version 3.3.0). Nothing is published. The version is 3.3.0 in the library, and this repository reads the library source through a local file that git ignores (refer to [Reading the library source](#reading-the-library-source)).

### What the library has now

- **Types** (in Primitives): `DurationEstimate`, `DurationDistribution`, `SimulationSettings`, `SimulationResult<T>`, `CriticalPath<T>`, `CriticalPathStep<T>` and `CriticalPathLink`. `GraphLimits.MaximumSimulationIterations` is 1,000,000.
- **The simulator** (in Compilers): `ScheduleSimulator<T, TResourceId, TWorkStreamId>`. The constructor takes the activities, the resources, the work streams and a dictionary of estimates. It compiles the plan once, and `CompilationErrors` gives the errors. `Run` takes the settings, an optional `IProgress<int>` and a cancellation token.
- **The result is raw.** For each iteration it holds the finish time (`-1` for a failed iteration), one bit for each activity (critical or not) and the critical paths. The application makes the statistics.
- **One internal seam.** `SimulationIterationRunner` does the scheduling of one iteration. Nothing else touches the graph builder. The tests hold it to a full compile for many random durations, and a runner that leaves out the reset of the resource state, or the second critical-path calculation, fails them.
- **Same result for any thread count.** The iterations go in chunks of 64. The seed of a chunk is the output of splitmix64 that follows the main seed, a number of steps on that is the number of the chunk. A run of fewer iterations is therefore the start of a longer run with the same seed.
- **Same result in Rust, bit for bit.** The generator (xoshiro256**), the logarithm, the exponential and all the samplers are written out in C# and in Rust, and they use only operations that IEEE 754 fixes. The file `SimulationBaseline.txt` in the .NET tests holds the exact bits. It includes four whole simulations. The Rust test passed on the first run.

### Decisions in phase 2

1. **Zero-day draws.** A draw of 0 for an activity that takes time stays 0. The activity is then as short as a milestone in that iteration. A test shows that an iteration still agrees with the compile when this happens.
2. **Plans with no resources.** By default the iteration stops after the first critical-path calculation. The finish times are those of a compile. The critical activities are those of the plan alone, without the links of the synthetic resources that a compile makes up. The setting `ScheduleOntoSyntheticResources` gives the behaviour of the compile.
3. **What a critical path is.** It is a chain of critical activities in which each one starts at the moment that the one before it finishes (a tight link). The link is a dependency of the plan, or a resource link where the resource is busy with the one before. The walk takes activities and predecessors in the order of the plan, so that C# and Rust find the same paths in the same order.
4. **Milestones are not on a path.** This follows the reference tool, which names only the activities that have a duration. It also stops two chains that differ only by a milestone from counting as two paths, which would split the weight of a tied iteration with no real difference. The criticality of a milestone is still in the critical bits of the result. The maintainer confirmed this on 2026-10-16.
5. **Tied paths are capped.** An iteration records at most 32 critical paths by default (`MaximumCriticalPathsPerIteration`), and the result says when the list is not complete. The phase 3 code splits the weight of an iteration among the paths that the result gives.
6. **Folders.** The new types follow the kind folders that the library already has (`Engines`, `Compilers`, and `Entities` and `Enums` in Primitives).
7. **The modified beta.** Its mean is the PERT mean, but its variance is `(mean - O)(P - mean) / (shape + 3)`. That is larger than the PERT variance when ML is near the middle of the range, and smaller when ML is near O or P. The earlier text of this plan said "larger", which is not true in every case.

### Reading the library source

The tracked file `Directory.Build.targets` imports `Zametek.Maths.Graphs.local.targets` when the file exists. The file is in `.gitignore`. It removes the package references to `Zametek.Maths.Graphs.Primitives` and `Zametek.Maths.Graphs.Compilers`, and adds project references to the library source in the folder next to this repository. After a change to the file, run `dotnet restore Zametek.ProjectPlan.slnf`. Delete the file, and restore again, to return to the packages. A build on CI has no such file, and so it uses the packages. Before this work merges, the packages must be published at 3.3.0, and the two package references must change to that version.

## Phase 3: the results

Phase 5 changed some of what this section says: the panel is three docks, and the settings are in the project. [Phase 5: the results](#phase-5-the-results) says how.

Phase 3 finished on 2026-10-16. All the work is in this repository, on the branch `mc-sim`, in the commits `09816b80` (the statistics and the CSV files), `3abf30bd` (the view model and the chart), `0462c5ce` (the UI component and its place in the application) and the commit that holds this section (the target date and the documents). Nothing is pushed.

### What the application has now

- **The panel.** "Schedule Simulation" is a tab next to the charts. It has the settings (iterations, distribution, the shape value of the modified beta-PERT, the seed with a box "New seed each run", the target finish, Run and Cancel), a progress bar with the time that is left, a warning when the result is stale, the chart, and three tabs: the statistics, the critical paths and the criticality of the activities.
- **The statistics.** `SimulationAnalyzer` makes them from the raw result: the mean, the standard deviation, the minimum and the maximum, the percentiles 5, 10, 25, 50, 75, 80, 90, 95 and 99 (nearest rank), the share of the iterations that finish by the target, the histogram (one bin for each day, and wider bins after 100 bins), the table of the 25 heaviest critical paths, and the criticality index of each activity.
- **The chart.** `SimulationChartBuilder` draws a stacked histogram (the eight heaviest paths and a group for the rest), the S-curve on the axis at the right, and lines at the mean, at P50, P80 and P90, at the planned finish and at the target. A legend entry has the rank of the path, which is the rank of its row in the table, and its share. It has the names of the activities only when they are short.
- **The CSV files.** One name in the save dialog gives five files: `-statistics`, `-histogram`, `-paths`, `-activities` and `-iterations`.
- **The copy of the plan.** `CoreViewModel.CaptureSimulationInput()` takes it, with the steps of the snapshot of a compilation (refer to [Architecture](ARCHITECTURE.md), section 7, rule 13).

### Decisions in phase 3

1. **Not in the cascade.** A simulation runs only when the user asks. It copies the plan, runs on the task pool, and never reads a live activity again.
2. **A stale result.** The panel marks a result as stale when the outputs of the plan have a new revision, or when they no longer match the plan. It never starts a run by itself.
3. **The seed.** The seed box always shows the seed that the last run used. With "New seed each run" on, the panel makes a new seed for each run. With it off, the panel uses the seed in the box. A user can therefore make a result again.
4. **The target.** The target is a number of days. When the dates are on display, it is a date, and the panel finds the first finish time that finishes on that date or after it.
5. **Changes for the UI thread.** A run reports its progress from the threads of the simulation, and a bound control can change only on the UI thread. Everything that a run changes for the UI goes through `IUIDispatcher.Defer`, in order.
6. **Percentiles and the deviation.** A percentile is the smallest finish time that at least that share of the iterations reach. The standard deviation divides by the number of valid iterations.

### What is not done

- The settings of the panel are not saved in the plan or in the settings of the application. They start again with the application.
- A plan that resources decide has very many critical paths that are nearly equal. The sample plan has 92 % of its weight in "Other paths" and about 1 % in each of the eight paths that the chart shows. The chart is correct but it says little. [TODO](TODO.md) has an entry for a grouping that is more useful for such plans.
- The criticality table counts each activity in each iteration, one call at a time. This takes seconds for 1,000,000 iterations of a large plan. A method of the library that counts the bits would be faster.

## Phase 4: the results

Phase 5 changed the options of `zpp` and the API that this section describes. The settings of the simulation are the project's, and `--simulate` is `--run-simulation`. [Phase 5: the results](#phase-5-the-results) says how.

Phase 4 finished on 2026-10-16. All the work is in this repository, on the branch `mc-sim`, in the commits `8257abbc` (the panel writes one CSV file at a time, tells the date of a finish, and logs its progress), `b6ac8cb8` (the simulation in the engine, the options of `zpp`, and the API of `zpp serve` with its documents and tests), `bd753a3f` (a cancel of a job reaches a simulation that has not begun) and the commit that holds this section. Nothing is pushed.

### What exists now

- **The engine.** `JobRequest.Simulation` (a `SimulationRequest`) asks for a simulation. The job resolves the same panel view model as the desktop, sets its settings, and runs it after the scenario chart. The statistics are in `JobResult.Simulation`. The chart and the five CSV files are the last six values of `JobOutput`, in this order: `SimulationChart`, `SimulationStatistics`, `SimulationHistogram`, `SimulationPaths`, `SimulationActivities`, `SimulationIterations`. The job token cancels a simulation between two chunks of its iterations.
- **The options of `zpp`.** `--simulate` runs the simulation. The options `--simulation-iterations`, `--simulation-seed`, `--simulation-distribution`, `--simulation-shape` and `--simulation-target` set it, and `--simulation-chart-directory`, `--simulation-chart-format`, `--simulation-chart-size` and `--simulation-csv-directory` write its outputs. Each of them needs `--simulate` (exit code 2 without it). [COMMAND-LINE](COMMAND-LINE.md#simulate-the-schedule) has the table.
- **The metrics.** After the metrics, `zpp` prints a row "Schedule Simulation" and then the statistics as the panel shows them (the seed, the distribution, the mean, the standard deviation, the earliest and latest finish, the percentiles, the chance of the target). With `--metrics-format json`, a `Simulation` object follows the metrics, with raw values. Without `--simulate`, the output is what it was.
- **The API.** The option `simulation`, six outputs (`simulationChart` and the five CSV files), the member `simulation` of an answer (and of the problem `output-failed`), the code `notAllowedWithoutSimulation`, and the limit `maxSimulationIterations`. The server option is `--max-simulation-iterations`. [openapi.yaml](openapi.yaml), [API.md](API.md) and the contract tests changed in the same commit.
- **The parity.** The tests run `zpp`, the server and `zpp --server` on the same plans and options, with the sample plan (it has resources) and a plan with estimates (`estimated.zpp`). They compare the exit code, the text and each file, byte for byte.

### Decisions in phase 4

1. **One simulator.** A job does not have a simulator of its own. It drives the panel view model, and thus the panel, `zpp` and the server make the same numbers in the same way.
2. **A fixed seed.** The seed is 1 when nothing gives it. The panel draws a new seed for each run, but `zpp` and the API repeat, as everything else of `zpp` does. A user who wants another sample gives another seed. The seed of the last run of the panel makes that result again.
3. **The seed is text in JSON.** The seed can be any whole number up to 18446744073709551615, and the guide says that an integer that can be larger than 2^53 - 1 must be a string ([REP-7](RESTFUL-API-GUIDE.md#3-representations)). The API reads and writes it as text, and so does the JSON of `zpp`. The client always sends the seed, because a seed of 0 is not the default and the client leaves out what is default.
4. **The statistics are beside the metrics.** In the API, `simulation` is a member of the answer and not of `metrics`: the metrics are the metrics of the compiled plan, and a simulation has settings of its own. In the output of `zpp`, the statistics follow the metrics. This leaves the contract of `metrics` and the JSON of the first release as they were.
5. **A strict rule for the options.** `zpp` refuses each option of the simulation that has no `--simulate`, and the API refuses each output of the simulation that has no `simulation`. A silent ignore would hide a typing error. The shape value is valid only with the modified beta-PERT distribution.
6. **One output for each file in the API, and one directory in `zpp`.** The API is exact: a request asks for the files that it wants. `zpp` writes the five files with one option, as the panel does with one name.
7. **The limits.** The server limits the iterations (`MaxSimulationIterations`, 100000 by default, at most 1000000). The time of a job (`JobTimeoutSeconds`) limits a simulation of a large plan, and the simulation stops between two chunks of its iterations. A request with no iterations on a server whose limit is lower than 5000 gets the limit.
8. **Progress is in the log.** A run of `zpp` or of a server has no progress bar. The panel view model writes the settings, the progress (about every two seconds) and the time to the log, at the information level. `--verbose` shows these lines.
9. **The header row.** The table of the metrics wraps a cell of more than about 40 characters. A name with a prefix on each row made the longest row wrap, and thus the table has one row, "Schedule Simulation", before the statistics.
10. **The target is in days.** `--simulation-target` and `targetFinishDays` are whole days from the start of the project. The panel also takes a date.
11. **A cancel is never lost.** The first version of the engine registered `CancelSimulation()` with the token of the job. That call does nothing when the run has not yet made its own token, and so a cancel in that window (the time limit of a job, for example) let the simulation run to its end. The run now takes the token of the job and links it to its own (`RunSimulationAsync(CancellationToken)`). The hook found this: a test of the time limit failed one time in three. Its other cause was a bound of 30 seconds on the answer, which the test projects of a build, running side by side, can exceed. The test now asks for the problem `job-timeout` and no more, and a test of the engine, with no clock, says that the run stops.

### What is not done

- A target as a date for `zpp` and the API.
- `zpp` cannot write only some of the CSV files. The API can.
- A client of `zpp --server` sees no progress while the server simulates. The log of the server has it.
- One job uses all the processors for its iterations. A server that runs many simulations at one time asks the pool for more threads than it has processors. The result does not change, but the jobs slow each other. The limit of iterations and the job time bound the cost.
- The cancel of `zpp` (Ctrl+C) kills the process and does not cancel the job.

## Phase 5: the results

Phase 5 finished on 2026-10-16. All the work is in this repository, on the branch `mc-sim`. It has six steps, and each step is a commit: `255c545c` (the title bar), `b6bab7a7` (the settings and the result in the scenario and in the file), `40a9aff2` (the three docks), `6d848a1b` (Excel), `4e98d8fe` (`zpp` and `zpp serve`) and the commit that holds this section. Nothing is pushed.

### What exists now

- **Three docks.** "Simulation" sits beside "Scenarios". It has the name of the scenario, the settings, and the buttons Run Simulation and Cancel Simulation. Fixed at the bottom, it has the progress, the status and the warning for a result that is out of date. "Simulation Chart" has the chart and its menu. "Simulation Metrics" sits beside "Resource Metrics". It has the tabs Statistics, Critical paths and Criticalities, and each tab has "Copy Table" and keeps the layout of its columns. The tab "Critical paths" shows the activity IDs of a path, and its tooltip has the names. One view model, `SimulationChartManagerViewModel`, has the simulation. The other two docks are thin view models that hold it.
- **The panels follow the scenario.** All three clear when a scenario loads, when a project closes and when a project begins. A run that is in progress stops, and its result is thrown away.
- **The title bar.** It follows a project that closes or a scenario that loads with no edit. This was a fault that `8c109e7d` caused, and it is on the branch `mc-sim` only.
- **The scenario holds the simulation.** The core owns the settings (`SimulationSettings`) and the last result (`SimulationSummary`), as it owns the metrics. The project file (v0.6.2, which is not released) holds both. It holds the statistics, the percentiles, the histogram, the critical paths and the criticalities. It does not hold the iterations. A change of a setting, or a new result, marks the scenario as modified. A load does not. A result that a file holds is trusted, as the metrics of a file are.
- **Excel.** The export has the sheets "Simulation Settings", "Simulation Statistics", "Simulation Percentiles", "Simulation Histogram", "Simulation Paths" and "Simulation Criticalities", and the import reads them. A scenario with the default settings and no result has none of them.
- **`zpp` and the API.** `--run-simulation` (`runSimulation`) runs the simulation with the settings and the saved seed of the project. A project that holds a result gives it without a run. `--simulation-directory`, `--simulation-format` and `--simulation-size` (the output `simulation`) write the chart and the CSV files side by side. The new problem `simulation-not-possible` is for a simulation that the project cannot give. [COMMAND-LINE](COMMAND-LINE.md#simulate-the-schedule) and [API](API.md) have the details.

### Decisions in phase 5

1. **The core owns it.** The settings and the result belong to the scenario, and not to the panel. A job, the panel and a file then agree.
2. **A result that a file holds is trusted.** The panel does not say that it is out of date. The metrics of a scenario that a file holds, with the compile off, are the same.
3. **A saved seed.** A run of a job uses the saved seed, also when the project draws a new seed each time. It leaves the settings as they are. A run of the panel draws a seed and saves it.
4. **The seed in Excel is text.** A cell keeps a number to 15 digits only, as Excel does, and a seed has up to 20 digits.
5. **One column for each group of paths, and numbers as numbers.** The histogram has a column for the weight of each group of paths, and not a list in one cell. The import reads a number from its cell. It does not read the text of the cell with the culture of the computer.
6. **The settings come from the project.** The options for the iterations, the seed, the distribution, the shape value and the target are gone from `zpp` and from the API. Otherwise, each setting has two places.
7. **A limit is on a run.** A server refuses a run of a project that has more iterations than its limit (`validation-failed`). It gives a result that the project holds without a limit, as that costs nothing.
8. **A kind of problem of its own.** A simulation that cannot be done is `simulation-not-possible`, and not `validation-failed`. The client then ends as the local run does (exit code 1). The refusal for the limit has no local analogue, and thus it stays a refusal (exit code 5).
9. **The run goes first.** The simulation runs before the project and the workbook are written, so that they hold its result. Its chart and its files come last in the outputs, as before.
10. **A failure keeps no old result.** A run that fails does not give the result that the project had before it.
11. **A seam for a job.** The job has `SimulationSettings` in the place of the settings of the project. The warm-up of the server and the tests use it. The job also has `MaximumSimulationIterations` for the limit of a server.

### Lessons

- A property chain in ReactiveUI (`WhenAnyValue`) drops a value that did not change. This caused the fault of the title bar, and a counter fixed it.
- Two pipelines that present the same value race. The first version of the pipeline for a result that a file held took it as shown when the pipeline for the display settings had shown it. Then the result looked out of date. A test found it. The pipeline now asks whether the panel made the result.
- A contract that three documents and the code share needs one commit. The tests of the contract failed whenever one of them was behind.
- The state of a command is an event on the thread that changed it. A project closes on the task pool, and it cleared the result there. The menu item "Export CSV Files" of the chart was open, and it threw an exception about the thread. The Run and Cancel buttons had the same fault. `ForTheUiThread` in the panel now hands the state of each command to the dispatcher. Two tests failed before the fix and pass after it.

### Changes after the first look

The maintainer looked at the docks in the application and asked for changes. They are in the commits that follow phase 5.

- **Names.** `ScheduleSimulation*` is now `SimulationChart*`. This is true for the view, the view model, the interface and the tests. Each dock has a folder of its own: `SimulationChartManagement`, `SimulationManagement` and `SimulationMetricManagement`.
  The dock "Simulation" first had the names `SimulationControl*`. They are now `SimulationManagement`, `SimulationManagerViewModel`, `ISimulationManagerViewModel` and `SimulationManagerView`, as the names of the other docks have no "Control".
- **Tab order.** In the default layout, the tab "Simulation Chart" is now before the tab "Scenario Chart". A layout that a person saved keeps its own order until the person resets the layout.
- **Inputs.** The iterations, the seed and the target finish use the numeric controls of the Activity Manager. The iterations and the target use `NumericIntUpDown`. The seed uses `NumericULongUpDown`, because the seed has 64 bits and an `int` cannot hold it. A limit of the library (`GraphLimits`) is the maximum of the iterations and of the target.
- **Colours.** An input that a person can change has the background of an editable cell, as in the Activity Manager. The seed has it only when "New seed each run" is off. When that box is on, the seed is read-only and has no background.
- **The empty chart.** A chart with no result had the light theme at the start and after a reset. This was so also when the theme was dark. The chart that has no result now has the theme of the application, as the other charts do.

### What is not done

- Nobody has looked at the three docks in the running application. A headless render of each view looks right. A person must look at them with the real theme, and at the layout of the docks.
- A saved result does not say which seed, distribution and shape value made it. The statistics that `zpp` prints for a saved result use the settings that the project holds, and a person can change them after the run. [TODO](TODO.md) has an entry.
- A project does not hold the iterations of a run. The file of the iterations needs a run.
- A job cannot make only some of the CSV files of a simulation. At the end of phase 4, the API had an output for each file. Phase 5 replaced them with the one output `simulation`, which makes the chart and all the files. [TODO](TODO.md) has an entry.
- The CSV files of a simulation are not in one Excel workbook with a worksheet for each file. [TODO](TODO.md) has an entry to explore this.

## Performance work to explore later

The maintainer decided that phase 2 builds none of the work in this section. The section records what phase 0 learned, so that the team can start from facts if speed becomes a priority. The library commits `1a73f3a` and `86d6c1e` (branch `mc-sim`) have the measurements. The efforts below are estimates, and nobody measured them. [TODO](TODO.md) has an entry for this section.

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
