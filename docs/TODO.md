# TODO

This file lists the maintainer-internal engineering intents that must version with the source code. GitHub issues take the bugs and the feature requests of users. Items that stay valid after the spin-out of Zametek.Graphs.Avalonia belong in [src/Zametek.Graphs.Avalonia/TODO.md](../src/Zametek.Graphs.Avalonia/TODO.md). Put a date on each entry when you add it. Delete the entry when you complete the work.

- [ ] **Do a check of each holiday rule before the calendar reads it** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  `DateTimeCalculator` gives the text of a holiday rule to Ical.Net (`new RecurrencePattern(...)`). Then it reads each occurrence in its range of days into a list. No path does a check of the text: not the load of a file, not the import of a workbook, and not `PlanCheck`.

  A rule with `FREQ=SECONDLY` has about 2.6 million occurrences in 30 days, and they give 30 dates. Each time that the calculator makes its range longer, it reads each rule again, under its lock. This work is in the load and in the build cascade. Thus, the limit of a compile does not end it.

  Nobody ran such a file, and the cost depends on Ical.Net 5.2.3. Run one first.

  Accept the frequencies `DAILY`, `WEEKLY`, `MONTHLY` and `YEARLY` only, and no part for hours, minutes or seconds. Give the number of the holidays a limit. Report a bad rule as a problem of the plan, and as a problem of its cell for a workbook.

- [ ] **Decide if the projected finish of the earned value chart needs a bound from the plan** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  `ResourceSchedulingService` divides the planned value by the slope of a line fit, and the result is the day of the projected finish. A slope near zero gave a day number of many millions. The review reported that the chart with dates then counted to the end of the range of dates and threw.

  Since 0.11.0-beta.3, a finish after the largest time of the compiler (100,000 days) is no finish (`ProjectedFinishTime`). But `DateTimeCalculator.AddDays` still counts to a far finish one day at a time, under the lock of the calculator. On 2026-11-23, the first count of 100,000 days took about 5 s, with weekends or with the default holidays. The next count of the same range took 3 ms.

  A bound of a multiple of the finish of the plan, for example 10 times, keeps this cost small. Such a projection is also of no use in the chart. Decide the multiple.

- [ ] **Decide if a tracked scenario that passes the limit of a compile ends the job** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  `JobRunner` loads and compiles each other tracked scenario for `compileTracked` and for the scenario chart. A tracked scenario with compilation errors gets its errors in the answer, and the job goes on. But a tracked scenario that passes the limit of a compile throws `GraphCompilationTimeoutException` out of the loop. The job then ends with exit code 4 (`compilation-timed-out`), with no metrics and no outputs. The message does not name the scenario.

  [The command line tool](COMMAND-LINE.md#exit-codes) says that a tracked scenario that does not compile does not change the exit code. No test has a tracked scenario that passes the limit.

  If the limit must not end the job, catch the exception in the loop, and record it as the error of that scenario. If it must, name the scenario in the message, and say so in the documentation.

- [ ] **Send the intermediate certificates of the server** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  `ServeSettingsHelper` loads the certificate of the server with `X509Certificate2.CreateFromPemFile` or with `X509CertificateLoader.LoadPkcs12FromFile`. Each of them gives one certificate. `JobServer` sets `ServerCertificate` only, and nothing sets `ServerCertificateChain`. Thus, the server loses the intermediate certificates of a `fullchain.pem` file, or of a `.pfx` file with its chain.

  The server then sends its own certificate alone, unless the system has the intermediate certificates or can get them. On a computer that has neither, for example a container with no network, a client probably refuses the certificate. The tests use certificates that sign themselves. Nobody tried a certificate from a certificate authority.

  Load the collection of the file, and give the other certificates to `ServerCertificateChain`. Also say in the documentation that the server reads the certificate one time, at its start. A new certificate needs a new start.

- [ ] **Write the output of `zpp` as UTF-8 on each system** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  `Program.Main` uses `Console.Out` and `Console.Error` as they are, and nothing in the project sets `Console.OutputEncoding`. On Windows, the encoding is then the code page of the console, for example 850. The JSON of the metrics does not escape a letter that is not ASCII. Thus, after `zpp ... > metrics.json`, a name with such a letter gives a file that is not UTF-8. A letter that the code page does not have becomes `?`.

  The same applies to the list of the scenarios, to the tables, and to the text that `zpp --server` shows again. The comment in `StandardConsole` says that the bytes are the same on Windows and on Linux. The tests read the output through writers, and thus they cannot see the encoding. Nobody looked at the bytes of such a file on Windows.

  At the start of `Main`, set the encoding of the two streams to UTF-8 with no byte order mark.

- [ ] **Read a project file one time** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  `ProjectFileOpen` reads the stream into a string, and it parses the string into a `JObject`. Then it writes the tree to a second string, with indents, and it reads that string into the model of its version. Each step of the upgrade makes one more model. `PlanCheck` runs after all of this. `SettingServiceBase` reads the settings in the same way.

  Thus, a job holds some forms of its file at one time, and only the limit of the upload (50 MB by default) bounds them. The operation that lists the scenarios pays the same cost, and it uses only the nodes. Nobody measured the memory. The other entries about the memory of a job are about the work after the compile.

  Read the version with a reader that goes forward only, or make the model from the tree (`ToObject`). Measure a job on `docs/assets/erp-migration.zpp` and on a file of 50 MB, before and after the change.

- [ ] **Do four smaller things for the server and the command line tool** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  1. **A defect that looks like a bad file.** `JobRunner.IsUnreadable` is true for each exception, except a cancellation and `OutOfMemoryException`. Thus, a `NullReferenceException` in a mapper becomes `project-not-readable` (422). `ProjectEndpoints` writes no error to the log for this answer. Write the inner exception to the log as a warning when it is not an exception of a reader.
  2. **The answer in the memory of the client.** `JobClient` reads each answer into memory, and its `HttpClient` has no `MaxResponseContentBufferSize`. A wrong address that sends a body with no end makes `zpp` hold as much as 2 GB. Set the largest size from the limits of the server.
  3. **The limits of the connections.** `JobServer` sets `MaxRequestBodySize` and no other limit of Kestrel. Thus, the number of the connections has no limit. The paths that have no key (`/v1/openapi`, `/v1/schemas/project` and `/health/*`) are outside the limiter of the jobs, and the server compresses the description for each request. Set `MaxConcurrentConnections`, and keep the compressed description.
  4. **Two status codes of the read of a form.** The review reported that an `options` field of more than 4 MB gets 400 and not 413. The limit of ASP.NET Core for a value comes first. It also reported that a body below the minimum data rate of Kestrel gets 400 and not 408. Nobody sent these requests.

- [ ] **Let one reader read the holiday rules** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  The calendar reads a holiday rule with Ical.Net. The dock of the holidays reads the same text with a reader of its own, `RecurrencePatternHelper`, which knows less of RFC 5545. Three results:

  1. **A rule that the desktop cannot read.** `ParseUntil` accepts `yyyyMMdd'T'HHmmss` and `yyyyMMdd` only. Thus, `UNTIL=20271231T000000Z`, which is the UTC form of the RFC, throws a `FormatException`. The throw is in the constructor of `ManagedHolidayViewModel`. A subscription of `HolidaySettingsManagerViewModel` calls it on the main thread, with no catch. The handler for exceptions that nothing handles then probably ends the application ([ARCHITECTURE](ARCHITECTURE.md#14-the-recovery-copy)). `zpp` compiles the same file.
  2. **A rule that changes.** `ParseDay` gives Monday for a day that it does not know, and thus `BYDAY=-1FR` becomes `BYDAY=MO`. `ToRule` ignores the parts that it does not know. After an add, a remove or an edit of one holiday, `UpdateHolidaySettingsToCore` writes each holiday again, from the rule that the reader made. The schedule then changes with no message.
  3. **The edit dialog.** The review reported that the dialog keeps only the first value of `BYMONTHDAY`, `BYMONTH` and `BYSETPOS`.

  A program, the zpp skill or a workbook can give such a rule. Nobody ran a file with one.

  Keep the text of the rule as the model. Show a rule that the dialog cannot make as text that the user cannot change there. Catch a rule that the reader cannot read, and report it.

- [ ] **Stop a drag in the Gantt chart when the window loses the pointer capture** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  No handler under `src` listens to `PointerCaptureLost`. In `GanttChartManagerView`, only `Gantt_PointerReleased` ends the drag that changes a duration, and it calls `SetActivityDuration`. If the window loses the capture during the drag, for example with Alt+Tab, no release arrives.

  The review reported the result. The bar then follows the pointer with no button down, and the next release of a button writes a duration into the plan. Nobody did this in the application.

  Handle `PointerCaptureLost`, and stop the drag with no change to the plan. The drag of a node and the pan of a graph have the same form ([the TODO file of the library](../src/Zametek.Graphs.Avalonia/TODO.md)).

- [ ] **Do six smaller things for the desktop application** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  1. **The rule of the holiday dialog.** `HolidayEditViewModel` builds its rule in four subscriptions that deliver on the task pool. `HolidaySettingsManagerViewModel` reads the rule when the dialog closes. If the pool is busy, for example during a simulation, the rule can be the one from before the last change. Build the rule with no `ObserveOn`.
  2. **A failure at the start.** In `App.OnFrameworkInitializationCompleted`, the set-up of the log is before the `try`. The catch writes to the log and to the standard error stream, which a `WinExe` program does not show. Then it ends the application with exit code 1. Thus, a settings folder that the application cannot make gives no message and no log. Show a message with the cause.
  3. **The build of a chart that fails.** Five subscriptions build a chart or the output with `Subscribe(async _ => await ...)`. Each build catches its exception and shows a dialog. Thus, a plan whose chart fails shows one dialog after each compilation, and the log has no line. Write the exception to the log, and show the state in the panel.
  4. **Two instances and the settings.** Each setter of `SettingService` writes all of the settings from memory, and nothing reads the file again. Thus, the last write of two instances removes the changes of the other instance, for example a recent file. The recovery copies already allow for two instances.
  5. **Save Image with no size.** The review reported that the save of a chart image writes no file and shows no message when `ImageBounds` has no value (`GanttChartManagerViewModel`, `EarnedValueChartManagerViewModel`).
  6. **The row of a sorted grid.** The review reported that `DataGridPersistScrollBehavior` counts the rows of `ItemsSource` to find the first row on the screen. With a sort in the grid, the order is different, and a change of the tab then shows a different row. The behavior does this count at each layout pass.

- [ ] **Wait for a signal, and not for a time, in the tests** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  Some tests wait for a fixed time and then make an assertion. On a slow computer, such a test fails with no defect, or it passes with no check. The harness of the server says that a test can stop for more than one minute (`RunningServer`). This occurs when the other test projects run at the same time. Six groups:

  1. **The server is full.** Ten sites wait 250 ms after a request starts, and then they expect 503 (`s_Settle` in `JobServerTests`, `JobClientTests` and `OpenApiContractTests`, and a delay in `ProjectEndpointsJobStatusTests`). `RunningServer.ApiRequests` already counts the requests that are past the limiter. Wait for that number.
  2. **A command.** Nine sites poll for the result of a command, for as long as 60 s. Two tests wait 200 ms or 300 ms and then say that nothing happened (`ScenarioChartManagerViewModelTests`, `SimulationChartManagerViewModelTests`). Each command is a `ReactiveCommand`, and `ProjectScenarioManagerLoadFailureTests` already awaits one. Await the command.
  3. **A thread that must wait.** Four assertions wait 250 ms or 300 ms and then say that a different thread is not complete (`CoreViewModelOffLockTests`, `TrackerRevisionTests`, `ProjectRecoveryTests`, `PlanGraphTests`). They also pass when that thread did not start. First wait for a signal that the thread started.
  4. **The clock.** `JobClientLimitsTests` waits 30 s in each run. `ProgramCancellationTests` cancels after 3 s, and it does not make sure that the simulation started. Give `JobClient` its times as parameters, and cancel when the job reports progress.
  5. **The UI tests.** `ApplicationDriver.SettleAsync` waits until `IsBusy` is false for 150 ms. `IsBusy` is a signal for the UI only ([ARCHITECTURE](ARCHITECTURE.md#8-isbusy-versus-isbulkupdating)). Use the settled signal of section 9.
  6. **A free port.** `JobServerStartTests.FreePort` finds a free port and closes it, and the server binds the port later. A different process can take the port between the two. Let the server bind port 0, and read the port from its log.

  The review read these sites. Nobody saw one of these tests fail.

- [ ] **Read the fixtures of the converter tests as the application reads a file** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  `ConverterFixture.ReadJsonFile` reads each fixture with `JObject.Parse`, and it writes the tree to a string again. `JObject.Parse` reads a text that looks like a date as a moment, in the time zone of the computer. `ProjectFileOpen` stopped this for the application (`DateParseHandling.None`), and its comment says why. Thus, the tests of `ConverterTests` give the converters dates that the application does not give them.

  The fixtures have dates with an offset, for example `2015-01-05T09:00:00+01:00`. The assertions compare moments. Thus, a step of the upgrade that changes the offset or the day of a date passes. Since 0.11.0-beta.1, a date of a plan is the day that it shows, in its own offset.

  Read the text of the file as it is, from the folder of the test assembly. For each version, compare the date and the offset of one date or more. `ConverterTests` also has assertions in comments, for the times of the creation and for the step from 0.5.0 to 0.6.0. Put them back, or remove them.

- [ ] **Show a test that does not run on a system as skipped** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  Some tests are for one operating system. On a different system, such a test returns at its start, and its result is "passed". `FileStreamHelperWriteTests` has nine such returns, and its header says so. The tests of the copy in `Zametek.Graphs.Avalonia.Tests` and `UnixSocketFileHelperTests` do the same.

  CI runs on Linux only. Thus, the Windows cases of the save by way of a temporary file are green in CI, and they never ran there. The results do not show this. The entry "Build and test on Windows in CI, and collect the coverage" asks for the job on Windows. This entry is about the result of a test.

  Add two attributes that derive from `FactAttribute` and set `Skip` on the wrong system. xunit v2 permits this.

- [ ] **Throw at each step of a load in the tests of a load that fails** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  ARCHITECTURE section 7 rule 16 says that a load that fails changes nothing, and that a load can fail at each of its steps. This rule protects against a loss of data. The tests throw at three places: at two changes of `ArrowGraphEdgeRoutingMode` (`RoutingModeFault`), and at a null list of the dependent activities. For "nothing changed", they compare three values of each scenario: the number of the activities, the number of the resources and the routing mode (`ProjectScenarioManagerLoadFailureTests`).

  Thus, a failure at a different raise of a load has no test. A way back that gives the correct number of activities with wrong durations passes.

  Add a theory that counts the raises of a load that works. For each number, it then throws at that raise, and it compares all of the saved scenario with the original. `ProjectRecoveryTests` already compares projects in this way. The review read the tests. Nobody changed the application to see if a test finds the defect.

- [ ] **Run the tests in more than one time zone** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  CI runs in UTC, and the hook runs in the time zone of the maintainer. The review counted 11 test files of `Zametek.ViewModel.ProjectPlan.Tests` that read `TimeZoneInfo.Local`, and the fixtures use the clock of the system. Earlier defects depended on the zone. For example, a load failed on a computer east of UTC (`DateTimeCalculatorTests` tells of it).

  `DateTimeCalculatorTests` has tests with zones of its own. But the core, the imports and the saves run only in the zone of the computer.

  On Linux, the variable `TZ` changes `TimeZoneInfo.Local`. Run the test project two more times in CI. Use a zone far to the east for one run, for example `Pacific/Kiritimati`. Use a zone far to the west for the other run, for example `America/Los_Angeles`.

- [ ] **Do three smaller things for the tests** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  1. **A server that stops.** `JobServer` gives a stopped server the time limit of a job to complete its jobs (`HostOptions.ShutdownTimeout`). No test stops a server that has a job in progress.
  2. **An upload with no length.** Each test of the answer 413 sends a body with `Content-Length`. No test sends a body in chunks that is too large. The review reported that such a body possibly gets 400.
  3. **Copies of the harness.** The review counted 22 private `Harness` classes. Seven files construct `MainViewModel` with its 11 arguments, and three files make a certificate that signs itself. The copies are no longer the same: the harness of `MainViewModelStartTests` does not dispose the main view model. Make one harness for the main view model and one set of helpers for the server tests, in small steps.

- [ ] **Give the release workflow fewer permissions, a draft release and checksums** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  No earlier review read `release.yml`. This review found five things:

  1. **The permissions.** The workflow gives `contents: write` to each job. The seven build jobs restore the packages of other parties, and they run the build tasks of those packages with this token. Only the job `create-release` needs the permission. Give the workflow `contents: read`, and give `contents: write` to that one job.
  2. **A release with some of its files.** `create-release` makes the release public, and then it uploads the files one at a time. If an upload fails, the public release has only some of the files. Make a draft, upload the files, and then make the release public.
  3. **No test.** The workflow runs no test, and it does not make sure that CI passed for the commit of the tag. A tag on any commit makes a release.
  4. **The name of the tag in the scripts.** The scripts contain `${{ github.ref_name }}`. The pattern of the trigger (`v[0-9]+.[0-9]+.[0-9]+-*`) permits a tag with characters that a shell reads as a command. `enforce-master-source.yml` gives such a value to its script through the environment, and its comment says why. The check of the tag against the SemVer pattern is in the last job only. Give the name through the environment, and do the check first. Only a person who can push a tag can use this, and thus the risk is low.
  5. **Nothing to verify a download.** A release has no file of checksums and no build provenance. The programs for Windows and the MSI have no signature. The programs for macOS have only the signature that the SDK makes. Add a file of SHA-256 checksums and an attestation of the build. A signature costs money, and the maintainer decides.

- [ ] **Give the version of the application in one place** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  No project file sets `Version`. Thus, each assembly and each program of a release has the version 1.0.0.0 in its file properties. The version that the application shows is the resource `Label_AppVersion` in `Labels.resx`.

  For each release, a person changes the version by hand in about seven places. These are the resource, the two MSIX manifests, `Package.wxs`, the name of the MSI in the `.wixproj` file, and the examples in `docs/API.md` and `docs/openapi.yaml`. Nothing makes sure that the places agree.

  Set `Version` one time in `Directory.Build.props`. Let the application read it from the assembly (`AssemblyInformationalVersionAttribute`), and give it to WiX as a constant. A test can then compare the manifests and the documents with it. The numeric fields of the MSIX and the MSI stay `X.Y.Z.0`.

- [ ] **Fix the package sources of a build, and refuse a package build that has the local library** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  The repository has no `nuget.config` file. Thus, a restore uses each package source of the computer. On 2026-11-21, a restore on the computer of the maintainer used a local folder as a source, together with nuget.org. The MSI and the MSIX packages come from that computer. A package in a local source that has the name of a real package can thus go into a release.

  Add a `nuget.config` file that clears the sources, names nuget.org, and maps each package to it (`packageSourceMapping`).

  `Directory.Build.targets` imports `Zametek.Maths.Graphs.local.targets` when the file exists. The build then uses the source code of the library from the folder next to the repository ([Build from source](BUILDING.md)). `build-msix.ps1` and the check of the inputs of the installer (`CheckTheInstallerInputs`) do not look for this file. Thus, a package can contain a library that is not the released one. Stop the two package builds when the file exists.

- [ ] **Make sure that the MSI does not repair itself for a second user** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  The MSI installs for the computer. Its component `DesktopShortcut` has a registry value below `HKCU` as its key path. The start menu has an advertised shortcut (`Advertise="yes"`). For a user who did not install the product, the key path is not there. Windows Installer then usually starts a repair when that user starts the advertised shortcut. It asks for the MSI file if it cannot find the file.

  The review read `DesktopComponents.wxs`. Nobody installed the MSI on a computer with two users. Do this check together with the entry "Install the MSI of the final 0.11.0 over a 0.11.0 pre-release". If the repair starts, put the shortcut of the desktop into a component for the computer, or remove `Advertise`.

- [ ] **Put the licenses of the other parties into each package** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  A release contains many libraries of other parties in one file. Examples are MPXJ, the Java runtime image of IKVM (which has source code of OpenJDK), Avalonia, ScottPlot, ClosedXML and MSAGL, and the fonts Inter and Cascadia Mono. The repository has `LICENSE` for its own source code, and one `OFL.txt` next to Cascadia Mono. The dialog "About" shows the version and the copyright only.

  Most licenses ask that the text of the license goes with each copy of the library. The review believes that MPXJ has the LGPL, which asks for more. The review did not read each license, and this entry is not legal advice.

  Read the license of each library. Make a file `THIRD-PARTY-NOTICES`, put it into each archive and each installer, and show it from the dialog "About".

- [ ] **Do five smaller things for the build and the packages** *(2026-11-23, from the review of `src`, `test` and `pkg`)*

  1. **The MSIX manifests.** Both manifests name the device family `Windows.Universal`, which is not correct for a desktop application with full trust. `MaxVersionTested` is 10.0.14393.0, and the packaging projects give 10.0.19041.0 as the minimum. The `.wapproj` files also keep configurations for 32-bit ARM, which .NET 10 does not have, and package references in a comment.
  2. **`build-msix.ps1`.** The script calls `vswhere` with `-prerelease`, and thus a preview of Visual Studio can build a release package. With `-Sign`, the script does not make sure that the bundle has a signature.
  3. **The dependencies of Spectral.** The step for Spectral in `ci.yml` pins the tool, but `npx` gets the newest dependencies of the tool at each run. Since 0.11.0-beta.3, `ci.yml` has a `concurrency` group: a new push to a pull request stops the run of the push before it.
  4. **Reports about security.** The repository has no `SECURITY.md` file, and `zpp serve` is a server.
  5. **The line endings.** `.gitattributes` asks for LF in the repository, and `.editorconfig` asks for CRLF. On 2026-11-21, 143 files were CRLF in the index, and 1,161 were LF. A tool that follows `.gitattributes` changes each line of a CRLF file. Decide the rule. Then make all the files agree in one commit, and name that commit in `.git-blame-ignore-revs`.

- [ ] **Read only the kept result of a formula in the import of a workbook** *(2026-11-20, from the review of 0.11.0-beta.2)*

  `XlsxWorkbookReader.Convert` reads `cell.Value`. For a formula that has no kept result in the file, ClosedXML 0.105.1 then calculates the formula. It also calculates each other formula of the workbook that has no kept result, on each sheet. One formula in a cell under a title starts this work. The other formulas can be in a sheet that the import does not read.

  The three bounds of the reader (the size of the parts, the rows and the cells) do not see this work. All of it is in one read of a cell. Thus, the stop between two rows and the time limit of a job do not end it. The import of the desktop application reads a workbook in the same way.

  A run with the Release build of `zpp` measured this. The workbook was `docs/assets/sample.xlsx` with a formula in `General!A2`, and with a new sheet of formulas `SUM(C1:CV20000*1)`. The file had 94 KB. The import took 1.4 s with no formula in the new sheet, 3.6 s with 20 formulas and 7.0 s with 60. That is about 0.1 s for each formula. With no formula in `General!A2`, the 20 formulas added no time.

  Each of these imports ended with one problem, because the formula in `General!A2` gives a number and not a date. Nobody ran a formula over a full sheet, which has about 50 times the cells, or a workbook with thousands of formulas.

  Read only the kept result (`CachedValue`), and report a formula that has none as a problem of its cell. `XlsxScenarioImportContractTests` says today that the import calculates such a formula. Thus, this changes the contract of the import, and the maintainer decides. A second choice is to refuse a workbook that has a formula with no kept result.

- [ ] **Do not let a slow download hold a place of a job** *(2026-11-20, from the review of 0.11.0-beta.2)*

  `ProjectEndpoints.CompileAsync` and `ListScenariosAsync` write the answer inside the endpoint (`answer.Result.ExecuteAsync`). The limiter of the jobs frees the place of a job only after the endpoint returns. Thus, a client that reads a large answer slowly holds the place after the job ended. Only the minimum data rate of Kestrel for an answer bounds this time.

  This is the case of the slow upload in the other direction, and 0.11.0-beta.2 bounds only the upload. The time limit that `zpp --server` calculates from the limits of the server (`JobClient.GetRequestTimeout`) does not count this time. Nobody ran this. The source code of this part did not change in 0.11.0-beta.2.

  Free the place when the job ends, before the write of the answer. A second choice is a time limit for the write.

- [ ] **Do the checks of the dates for an import, and for the target date in the application** *(2026-11-20, from the review of 0.11.0-beta.2)*

  `PlanCheck` refuses a project file whose start is outside its range of dates. It also refuses a constraint date or a target date that is too far from the start. Since 0.11.0-beta.2, an import gets the check of the limits before the load (`PlanCheck.FindLimitViolations`). It gets none of the checks of the dates. A run showed one result. A workbook with the project start 9999-12-31 ends `zpp` with exit code 1 and the text of a .NET exception.

  Nobody tried the server with such a workbook. Nobody tried a date that makes the calendar count for a long time.

  The desktop application does not call `PlanCheck`. `SimulationTargetHelper.FinishTimeByEndOf` adds one day to the target date, and the getter of `TargetFinishTime` calculates the target at each read. Thus, a target date at the end of the range of dates throws in the getter and in each save. The setter reads the getter first, so the user cannot change the target. Only a program writes a target date today, and thus the risk is low. Nobody ran this.

  Give the dates of an import the checks that the dates of a project file get. Decide if the application does these checks at the load of a file too.

- [ ] **Do three smaller things from the review of 0.11.0-beta.2** *(2026-11-20, from the review of 0.11.0-beta.2, changed 2026-11-22)*

  1. **The name of the temporary file.** `FileStreamHelper.Write` always uses `<name>.tmp` next to the target, and the copy in Zametek.Graphs.Avalonia does the same. It writes over a file with that name, and it deletes the file at the end. A test pins this for a temporary file that an earlier write left. A file of the user with that name gets the same result. Decide if the name must be one that only the application makes. *(2026-11-23)* A second case for the same decision: two writes of one file at the same time, for example from two instances. The second write cannot open the temporary file, and `Write` then writes into the target directly. The review reported the result on Linux and macOS: the first write then puts its file in the place of the target. The file then does not have the content of the second write. A name for each write removes this case too.
  2. **A value in `zpp-serve.json` that is not a number.** The check for a name that is not a limit reads only the names of the first level. The review reported that `{"MaxJobs": {"value": 2}}` thus passes, and that the server starts with the default. Nobody ran this.
  3. **Writes to the disk on the UI thread.** `FileStreamHelper.Replace` flushes to the disk, and it can wait for 2 s for a file that a program holds. `SettingService` calls it on the thread of each setter. `RecoveryStore.WriteCopyAsync` calls it under its lock, on the UI thread for a copy after an open or after an exception. `FileStreamHelper.SaveAsync` does the same write on a pool thread. Nobody measured a delay.

- [ ] **Decide what a save does with a file that has a second name** *(2026-11-18, from the save by way of a temporary file)*

  Since 0.11.0-beta.2, a save writes a temporary file and puts it in the place of the target ([ARCHITECTURE](ARCHITECTURE.md#11-the-engine-the-pattern-at-its-extreme)). A file can have two names in one file system (a hard link). After a save to one name, the other name keeps the old content. Before, a save changed both, because it wrote in place.

  The application does not see that a file has a second name: .NET gives no count of the names of a file. A check needs a call of the system on each platform. Decide if such a file gets a save in place.

  The save also gives the file to the user who saves it, on Linux and macOS: the new file is a file of that user. A save in place kept the owner of the file. Nobody made such a case to see the result.

- [ ] **Find where an exception of a `DispatcherTimer` goes** *(2026-11-18, from the check of the handler for exceptions that nothing handles)*

  Since 0.11.0-beta.2, `UnhandledErrorHandler` takes an exception of the UI thread, and one that leaves a command or a subscription of ReactiveUI. It keeps a recovery copy, tells the user, and ends the application ([ARCHITECTURE](ARCHITECTURE.md#14-the-recovery-copy)).

  In a check with the built application, the `Tick` handler of a `DispatcherTimer` threw an exception. The application went on. `Dispatcher.UIThread.UnhandledException` was not raised, and the log had no line of the exception. An action that `Dispatcher.UIThread.Post` runs does reach the handler. The application has one such timer (`FadeInBehavior`).

  Find where Avalonia 12.1.3 puts the exception of a timer, and give it to the handler. Do the same check for a timer of the thread pool.

- [ ] **Tell the users about the recovery copies in the wiki** *(2026-11-18, from the recovery copy of the desktop)*

  Since 0.11.0-beta.2, the desktop keeps a recovery copy of a plan with changes, every 5 minutes and before it ends after an exception. The next start asks about a copy that a run left. The menu "Settings" has the item "Keep a Recovery Copy". The copies are in the folder `recovery`, next to the settings and the logs (`.zametek/projectplan.net` in the folder of the user). The wiki does not say this.

  The time between two copies is 5 minutes, and no setting changes it. Decide if a user needs to change it.

- [ ] **Run Reset Layout and Save Layout on the UI thread** *(2026-11-14, from the review of `src`)*

  `ResetLayoutAsync` and `SaveLayoutAsync` of `MainViewModel` call `ResetLayout` and `SaveLayout` through `Task.Run`. On the pool thread, `ResetLayout` holds `m_Lock` of `MainViewModel`. Under the lock, it closes the dock layout, sets `Layout`, starts the new layout and calls `DataGridLayoutManager.ResetDataGridModels()`. Each reset action is `DataGridPersistLayoutBehavior.ResetDataGridModel`, which calls `Dispatcher.UIThread.Invoke` and waits. The setters of `ShowDates` and `UseClassicDates`, and `CloseLayout`, take the same lock on the UI thread. If the UI thread waits for the lock at that moment, the two threads wait for each other, and the application stops.

  The two methods also use Dock objects off the UI thread. `ResetActions` is a plain list, and the UI thread changes it when a grid attaches or detaches. Call the two methods on the UI thread, without `Task.Run`. Then the lock is not necessary around the Dock calls. The Debug assertion of the entry "Add a Debug assertion for the locks that the application holds when it announces a change" finds this site. Nobody reproduced the deadlock.

- [ ] **Do not read `OrderableActivities` on the thread of a compile** *(2026-11-14, from the review of `src`)*

  `PlanGraph.SetDisplayOrders()` reads `OrderableActivities` by index, and it sets the `DisplayOrder` of each activity. Its caller holds the lock of the core. The thread is the one that takes the snapshot for a compile, or the one that builds a save.

  But DynamicData binds `OrderableActivities` on the UI thread (`ObserveOn(RxSchedulers.MainThreadScheduler)`, then `Bind`). The lock of the core does not guard this bind. An add, a remove or a drag of a row during the loop can give an `ArgumentOutOfRangeException` or a wrong order. Nobody reproduced this.

  Let the UI thread publish the order as an array of ids each time the bound collection changes. Let `SetDisplayOrders` read the array. `ActivitiesManagerViewModel` also reads the collection by index under its own lock. Find on which thread it does this.

  **A third caller** *(2026-11-20, from the review of 0.11.0-beta.2, changed 2026-11-21)*. Since 0.11.0-beta.2, the recovery copy reads the order of the view on a pool thread, every 5 minutes while the plan has changes. It uses `PlanGraph.ReadDisplayOrders`, which writes no `DisplayOrder`. A read that the view stops, because the view became shorter, gives no places, and the copy then has the places of the last compile. A read during a drag can still give a wrong order in the copy. When the UI thread publishes the order as an array, let `ReadDisplayOrders` read the array too.

- [ ] **Raise the properties of `DateTimeCalculator` after the release of its lock** *(2026-11-14, from the review of `src`)*

  The setters of `NonWorkingDayMode`, `DisplayMode`, `ProjectStart`, `NonWorkingDaysStart` and `NonWorkingDaysFinish` call `RaiseAndSetIfChanged` inside `lock (m_Lock)`. ARCHITECTURE §7 rule 6 forbids this shape, and the list of the entry about the surgery for rule 6 does not have these sites. The compile, the chart builds and the UI thread take the same lock in `AddDays` and `CountDays`. An observer of one of these properties that waits for a lock of another of those threads makes a cycle.

  The review found no such cycle. Set the field under the lock, and raise after the release. The Debug assertion finds these sites too.

- [ ] **Arm the tracked metrics without a check of `IsBusy`** *(2026-11-14, from the review of `src`)*

  Two subscriptions of `ProjectScenarioManagerViewModel` deliver on the task pool: `m_AreScenariosDisplayedSub` and `m_MetricsSub`. Each one sets `IsReadyToReviseTrackedMetrics` only when `IsBusy` is false. `IsBusy` is a counted scope for the UI, and nothing arms the revision again when the scope ends. If a delivery comes while the manager is busy, the application drops it. The scenario chart can then show the metrics from before the change, until another site arms the revision. Nobody reproduced this.

  The cascades of the core use `IsBulkUpdating` and `MuteWhile` for this purpose. Find what the check protects. Then replace it with these two, or arm again at the end of the busy scope.

- [ ] **Do two smaller things for input that is not trusted** *(2026-11-14, from the review of `src`)*

  1. **The dock layout.** `MainViewModel.RestoreLayout` gives `DockLayout.json` to the `DockSerializer` of `Dock.Serializer.Newtonsoft`. The review reported that this serializer reads type names from the file, with no binder. Nobody confirmed this in the source code of the package. The file is in the settings folder of the user, and thus the risk is low. If the report is correct, do a check of the type names against the types of Dock before the read.
  2. **The MS Project reader.** `MicrosoftProjectFileImporter` uses `UniversalProjectReader`, which finds the format from the contents of the file. It reads many formats that the import dialog does not offer. Use a reader for the one format, if MPXJ.Net has one. If it has none, do a check of the first bytes of the file before the read.

- [ ] **Change a day number to a date for a chart without a count from the project start** *(2026-11-14, from the review of `src`)*

  `DateTimeCalculator.AddNonWorkingDays` goes one day at a time from its start date, and it counts the working days under the lock of the calculator. `ChartHelper.CalculateChartStartTimeXValue` calls `AddDays` from the project start for each point, when the chart shows dates. `ResourceChartManagerViewModel` calls it for each resource on each day, and two times in the Block style. Thus, the steps for each resource grow with the square of the days. For example, 500 resources and 300 days give about 45 million steps for one build of the chart.

  Without dates, the chart does no count. This is a candidate for the work that the entry "Bound the work after the compile" asks for. Nobody measured it.

  Make the dates of day 0 to the last day one time for each build, with one count, and read them by index. The calculator can also keep such a table, and clear it with the cache of non-working days.

- [ ] **Raise each compiled property of an activity one time for each publish** *(2026-11-14, from the review of `src`)*

  `ManagedActivityViewModel.SetCompiledValues` sets `EarliestStartTime`, `LatestFinishTime` and `FreeSlack`. Each setter raises without a comparison, with 11, 11 and 6 calls of `RaisePropertyChanged`. That is 28 calls for each activity at each publish of a compilation, also when no value changed. Each of the four dependency strings gets three calls. For 985 activities, one publish makes about 27,600 calls, and the grid reads each bound cell again.

  Compare the values first, collect the names, and raise each name one time after the three writes. Measure the publish on `docs/assets/erp-migration.zpp` before and after the change.

- [ ] **Do not make a model of each activity for each booked day at each revision of the trackers** *(2026-11-14, from the review of `src`)*

  `ResourceTrackerSetViewModel` makes one `ResourceActivitySelectorViewModel` for each day that a resource has a tracker. Each selector has its own subscription to `TrackersRevision` of the core. At each revision, `ReviseTrackers()` of each selector makes a `ResourceActivityTrackerModel` for each activity of the plan. Then it compares them with its list. `PublishLists` keeps the arrays of a list that did not change, but the selector makes the models first. For example, 985 activities and 2,000 such days give about two million objects for each revision.

  Let one owner make the list of the ids and the names one time for each revision, and let each selector read it. Then one subscription is enough. Nobody measured this.

- [ ] **Decide if a chart or a graph that is not on the screen must build after each compilation** *(2026-11-14, from the review of `src`)*

  Each chart manager builds its plot when `CompilationOutputRevision` changes (for example `m_BuildGanttChartPlotModelSub`). Each graph manager asks for a new layout when `ArrowGraph` or `VertexGraph` changes (`m_RebuildSub`). No subscription asks if its tab shows. Thus, the MSAGL layout of the two graphs and the plots of the charts run after each edit. They also run when the user sees only a grid.

  A build on demand needs a mark that says that the output is stale. It also needs a build when the tab becomes active, and before each export. `zpp` and the server build all that a job asks for, so they do not change. Measure the builds on `docs/assets/erp-migration.zpp` first. Nobody measured them.

- [ ] **Measure the smaller costs that the review reported** *(2026-11-14, from the review of `src`)*

  The review reported these costs from the source code. Nobody read these sites a second time, and nobody measured them. Measure before a change.

  1. `PlanGraph.RawActivities` returns `m_Activities.Items`, and DynamicData 9.4.33 makes a new array for each read. A loop that reads the property many times copies the list each time.
  2. The graph outputs clone the activities two times (`CompilationOutputService`, `GraphCompilationService`). The arrow graph then compiles them again. The vertex graph calculates their critical path again. Since 0.11.0-beta.2, it does not schedule the resources a second time. Refer to the entry about the two compiles of a job.
  3. With rectilinear routing, a drag of a node resolves each edge two times for each move of the pointer (`InteractiveGraphViewModel`, `GraphClashResolver`). *(2026-11-23)* A possible cause: `GraphNodeDragBehavior.OnPointerMoved` sets `node.X` and then `node.Y`, and each of them raises. One method that sets the two values and raises one time can make the work half.
  4. The save of a workbook holds some copies of it in memory (`XlsxWorkbookWriter`). The server holds each output of a job in memory until the answer (`OutputResponse`). *(2026-11-23)* An example for the measurement: a chart can have the format `Bmp`, and the default maximum size of a chart is 5000 by 5000. Such an image has about 100 MB with no compression. A job can ask for more than one such chart.
  5. The SVG export of a chart takes one lock for the whole process, on each operating system (`ScottPlotImageExporter`). Thus, the jobs of `zpp serve` that export SVG charts wait for each other.

- [ ] **Make a compiler warning stop the build** *(2026-11-14, from the review of `src`)*

  The hook and CI do `dotnet format style` and `dotnet format analyzers` with `--verify-no-changes`, and then a plain `dotnet build`. No project sets `TreatWarningsAsErrors`, `AnalysisLevel` or `EnforceCodeStyleInBuild`, and `Directory.Build.props` does not set them either. Thus, a warning of the compiler, for example a nullable warning, does not stop a commit. On 2026-11-03, f52cd6cb added a CS8600 to a test, and 2d37e6fa removed it.

  The comment in `ci.yml` says that `/WarnAsError` is off because of NU1903 for `System.Security.Cryptography.Xml` 8.0.2. On 2026-11-14, `dotnet list package --vulnerable --include-transitive` found no vulnerable package in a project, and the restore now takes version 10.0.11 of that package. Thus, the reason is probably gone.

  Add `TreatWarningsAsErrors` to `Directory.Build.props`, or pass `-warnaserror` in the hook and in CI. List the warnings that must stay warnings. They are the IKVM warnings of the projects that reference MPXJ, and the one SDK warning of the Browser head (`NativeFileReference`). Build Debug and Release first, and read what each build gives.

- [ ] **Keep the versions of the packages in one file, and let Dependabot watch NuGet** *(2026-11-14, from the review of `src`)*

  Each project file gives the versions of its packages. Avalonia and ReactiveUI.Avalonia are in six project files each. A bump must change each file, and nothing stops two projects with two versions. No lock file exists, and thus a restore can take another transitive version on another day. `dependabot.yml` watches only `github-actions`.

  Add `Directory.Packages.props` (central package management). Then decide about lock files (`RestorePackagesWithLockFile`) for the programs. Add the `nuget` ecosystem to Dependabot, with `ignore` rules for the packages that the repository holds on purpose. These are Dock (after 12.1.0.6, it needs ReactiveUI 26), SkiaSharp 4 and Svg.Skia from 5.2 (they break Linux), `Microsoft.Extensions.*` 11 and xunit v3.

  On 2026-11-14, the packages that follow were behind, and the review knows no reason to hold them. Each line gives the version in use, and then the newest version. Do a check of each one before a bump.

  - Avalonia 12.1.3 (12.1.6) and ReactiveUI.Avalonia 12.1.2 (12.1.6).
  - Irihi.Ursa 2.2.0 (2.3.1) and Xaml.Behaviors.Avalonia 12.0.7 (12.1.5).
  - DynamicData 9.4.33 (9.5.0) and Splat.Autofac 20.2.0 (21.0.0).
  - MPXJ.Net 16.9.0 (16.11.0) and Ical.Net 5.2.3 (5.2.4).
  - JsonSchema.Net 9.4.0 (9.7.0) and MessageBox.Avalonia 12.0.0 (12.1.0).
  - Svg.Controls.Skia.Avalonia 12.0.0.13 (12.0.0.17).

  *(2026-11-23)* This list had Markdown.Avalonia.SyntaxHigh 11.0.3 (12.0.2). Nothing used that package, and 0.11.0-beta.3 removes the reference.

- [ ] **Build and test on Windows in CI, and collect the coverage** *(2026-11-14, from the review of `src`)*

  `ci.yml` has one job, on `ubuntu-24.04`. The desktop ships for Windows, and the hook on the computer of the maintainer is the only gate on Windows. Thus, CI does not test the things that differ there. Examples are the MS Project import through IKVM, the sharing of open files, the fonts and the paths with a backslash. The test projects reference `coverlet.collector`, but no step collects coverage. Thus, nobody knows which lines the tests do not reach.

  Add a job on Windows. The entry "Package `zpp serve`" already asks for Windows, Linux and macOS, for its golden files. Collect the coverage in one job, and publish the report as an artifact. Do not make a threshold a gate at first.

- [ ] **Map the paths of the XAML files in the symbols of a release** *(2026-11-14, found after the change to `ContinuousIntegrationBuild`)*

  A `Release` build starts each path of a C# file in the symbols with `/_/` (2e3f20cb). The XAML compiler of Avalonia writes the paths of the `.axaml` files into the symbols itself. It makes each path from the folder of the project, and it does not use `PathMap`. Thus, the symbols of a release keep the full path of each `.axaml` file on the build machine. On 2026-11-14, these were 34 files in four DLLs of the desktop application, and one file in `zpp`. Such a path shows only in a frame of compiled XAML in a stack trace.

  Avalonia 12.1.3 has no setting for this. The target `CompileAvaloniaXaml` gives `$(MSBuildProjectDirectory)` to the task as `ProjectDirectory`, and the task has no parameter for `PathMap` or `SourceRoot`. Ask Avalonia to map these paths, or look for a newer version that does. Do not change `ProjectDirectory` in a copy of the target before you know what else the task does with it. Each new version of Avalonia can also break such a copy.

  To do a check of the paths, read the documents of the embedded PDB of `Zametek.View.ProjectPlan.dll` with `System.Reflection.Metadata`. After the change, each document must start with `/_/`.

- [ ] **Suggestion: make `Zametek.ViewModel.ProjectPlan` smaller** *(2026-11-14, from the review of `src`)*

  On 2026-11-23, the C# files of the project had about 47,800 lines. That is about 55 % of the lines of the C# files under `src`, without the resources and the Browser head (about 86,200 lines). It holds the view models. It also holds the file formats, the Excel and MS Project imports, the services for the schedule and the metrics, and the chart exports. Four files have more than 1,600 lines: `ProjectScenarioManagerViewModel.cs` (2,764), `CoreViewModel.cs` (2,649), `MainViewModel.cs` (1,829) and `GanttChartManagerViewModel.cs` (1,815).

  Three steps are possible, and each is independent:

  1. Move the MS Project importer to an assembly of its own. Point 6 of the entry "Package `zpp serve`" asks for this too.
  2. Move the source code that needs no view model (probably `Xlsx`, `Files` and `Services`) to a library of its own. Its tests then need no view model.
  3. Continue the decomposition of the core with `ProjectScenarioManagerViewModel`.

  ARCHITECTURE §13 says where a file goes. Change it together with a move.

- [ ] **Suggestion: make the collections of the models read-only** *(2026-11-14, from the review of `src`)*

  `Zametek.Common.ProjectPlan` has 79 records. They have 74 public members that are a `List<T>` or another collection that a holder can change. The application gives these records to other threads as snapshots, but nothing stops a change. The equality of such a record compares its lists by reference, and the entry about rule 7 found a guard that this made dead. Until 0.11.0-beta.2, the build of the Gantt chart changed a set of a published activity, and ARCHITECTURE §7 rule 10 tells of a torn `HashSet`.

  Types such as `IReadOnlyList<T>` or `ImmutableArray<T>` make the snapshot rule a rule of the compiler. Mapperly and Newtonsoft.Json must still read and write the models. Thus, try one model first, when it changes for another reason.

- [ ] **Give the controls names for a screen reader** *(2026-11-14, from the review of `src`)*

  No `.axaml` file under `src` sets `AutomationProperties`. Thus, a button that has only an icon has no name for a screen reader, and the charts and the graphs have none. Start with the buttons that have only an icon, and take `AutomationProperties.Name` from the resources. The review reported one such button in `MainView.axaml`. The names also give a UI test a stable way to find a control.

- [ ] **Bound the work after the compile** *(2026-11-13, from the stress test of 0.11.0-beta.1)*

  Since 0.11.0-beta.1, the effort series no longer reads each activity for each resource on each day. The series of 1500 activities and 500 resources took 34 seconds and 38 GB of allocations, and it now takes 1.3 seconds and 278 MB. A job of that plan now takes 9 seconds, and not 60. The rest of this entry is from before the change. Measure it again, and then decide what to bound. A job still builds each output of the cascade two times (refer to the entry about the two compiles of a job).

  The test compiles one network of 1500 activities with 10, 50, 100, 200 and 500 developers, on a server with no other jobs. The jobs take 5.4, 7.8, 13.1, 21.6 and 51.5 seconds. Each job adds 107, 107, 108, 161 and 740 MB to the memory of the server. The compile limit does not stop this work. With a `compileTimeout` of PT5S, the plan with 200 developers gets 200 after 25 seconds. With PT1S, it gets `compilation-timed-out` after 3.9 seconds.

  At the limits of the compiler (2000 activities and 1000 resources), one job takes 214 seconds, and the server uses 2.9 GB at the peak. With the default job limit of 120 seconds, the job fails after it holds the memory for two minutes. Thus, four such jobs at one time can use 12 GB. Find the work that grows with the activities and the resources. The allocation streams of each resource are one possible cause: they have one value for each day. Then bound the work: make it smaller, put it under the compile limit, or give the server a limit of memory.

  The Linux test of the same day measured the job at the limits after the change. It took 15 seconds on Windows, and 17.6 seconds on Linux (WSL, 2 processors). But the server used 4.5 GB at the peak on Windows (3.5 GB on Linux), and it kept 3.4 GB after the job.

  In the stress test, a long run of 440 jobs gave its memory back. Thus, this is probably the garbage collector, which keeps its heap. By default, the server runs as many jobs at one time as the computer has processors. Thus, on a computer with 2 processors and 8 GB, two such jobs fill the memory. Measure the job with `DOTNET_GCConserveMemory` or with a limit of the heap, together with point 5 of the entry "Package `zpp serve`".

- [ ] **Stop the cache of non-working days from taking a gap as known** *(2026-11-13, found during the fix of a constraint date on a non-working day)*

  `AppendNonWorkingDays` in `DateTimeCalculator` searches the non-working days of a range. Then it makes the known span reach the range. When the range does not touch the span, the days between them become known, but the method did not search them. Their non-working days then count as working days. The first fix of a constraint date on a non-working day checked the date before it counted from the project start. Then a minimum earliest start of 220 became 306.

  The counts of the days start at the project start, and the shades of the Gantt chart go from one day to the next. Thus, nothing in the application gets a gap now, except maybe a shade before the project start. Make the span grow only next to itself: search the days from the span to the new range. Do not search the whole range to a date that is far away, for example in the year 9999. That can take a long time.

  **A count to an earlier day** *(2026-11-23, from the review of `src`, `test` and `pkg`)*. `AddNonWorkingDays` first searches `days + 30` days from its start date. For a negative number of days, that range is after the start date and not before it. In the loop, the method makes the known span longer only when the day is at or after `NonWorkingDaysFinish`. Thus, a count to a day before the known span takes each day there as a working day. The review reported that the callers give -1 only, and that they stay at or after the project start today. Search the days before `NonWorkingDaysStart` too.

- [ ] **When the schedule counts hours, store the times of a plan without an offset** *(2026-11-13, a decision of the maintainer)*

  Since 0.11.0-beta.1, a date of a plan is the day that it shows, in its own offset (a70c2e1c). The schedule counts whole days, so the time of the day and the offset have no effect. The file keeps a `DateTimeOffset` for each date, and the code must not read such a value as a moment. A plain date (`DateOnly`) was another option, but it removes the time of the day, which hours need.

  If the schedule counts hours one day, keep the dates and the times, and remove the offset. Store each time of the plan as the time on the clock of the plan, for example `2027-01-04T09:00:00`. Do this in one change of the version of the file, as for the entry about `Duration`. A working day of 09:00 to 17:30 then stays 09:00 to 17:30 in each time zone. A change of the clock does not move it either.

  The version of the file tells the migration that the earlier times have no meaning, so it keeps only their dates. Hours that a person works on a day do not need this change, because a tracker counts them for a day. A log of work as moments, with a start and a stop from a clock, needs a time zone of the plan. The zone puts each moment on a day and an hour of the plan.

- [ ] **Build with the .NET 11 SDK** *(2026-11-13, found when CI failed)*

  `global.json` keeps the build on .NET 10, with the newest .NET 10 SDK of the computer. On 2026-11-13, CI failed in the build of `Zametek.ViewModel.ProjectPlan`. The task `MavenReferenceItemResolve` of IKVM.Maven.Sdk 1.12.1 failed with `MSB4217` ("Task host node exited prematurely") and `MSB4027` (a disposed `MetadataLoadContext`). IKVM.Maven.Sdk runs its tasks in a task host (`TaskHostFactory`).

  The image of the runner, version 20261111, added the .NET 11 SDK (11.0.100), and nothing pinned the SDK. Before the move to .NET 11, find a version of IKVM.Maven.Sdk that works with the MSBuild of .NET 11, or report the problem to ikvmnet. Then change `global.json`.

- [ ] **Install the MSI of the final 0.11.0 over a 0.11.0 pre-release** *(2026-11-12, do before the final 0.11.0)*

  Each MSI of 0.11.0 has the version 0.11.0.0: the alpha, the beta and the final release. Since 0.11.0-beta.1, `<MajorUpgrade>` in `pkg/Zametek.ProjectPlan.MsiPackager/Package.wxs` has `AllowSameVersionUpgrades="yes"`. Thus, the MSI removes an installed product of the same version. The Upgrade table of an MSI that was built from beta.1 shows it. Its row that removes the earlier versions has the attributes 513, which include the maximum of 0.11.0.0. The MSI of the beta had 1.

  On 2026-11-12, the maintainer removed the MSI files of v0.11.0-alpha and v0.11.0-beta from their releases, and the next pre-releases have no MSI. But a user who installed one of these MSI files has 0.11.0.0. Before the final release, install its MSI over the beta, and make sure that the installed apps show one entry.

- [ ] **Suggestion: give the headcount for each day in the data output** *(2026-11-12, a suggestion from phase 1 of the zpp skill)*

  `outputs.data` writes `-resources.csv`, with one row for each resource on each day. It has no total for each day. The Excel export has this total, in the column `Total` of the sheet "Resource Chart (Activity)". Add the headcount for each day to the data output. For example, add a file `-headcount.csv` with the columns `Time` and `Headcount`.

  The CSV has the data now: on each day, count the rows where `Allocated` is `true`. On `docs/assets/sample.zpp`, this count is the same as the column `Total` on each of the 210 days, and its maximum is the `PeakHeadcount` of 10. But the sheet "Resource Chart (Activity)" counts the column `Allocated`, not the column `Activity`. A count of `Activity` gives a different curve, with a maximum of 6. Thus, the file is a convenience, and it also prevents a count of the wrong column.

- [ ] **Suggestion: let the scheduler prefer task continuity** *(2026-11-12, a suggestion from phase 1 of the zpp skill)*

  The priority list gives an activity to the first free resource in the allocation order. Righting Software also asks for task continuity. When activity A depends on activity B, the developer of B also does A. That developer knows the context, and has a stake in B.

  In the worked example of the zpp skill, the developer of Claims Store stops on day 50. Then `zpp` gives ClaimsAccess, which depends on Claims Store, to the developer of Security on day 55. The book keeps the developer of Claims Store, who then waits for 5 days. Thus, the schedule of `zpp` costs 5 person-days less, and the schedule of the book keeps the continuity.

  An option of the scenario can make the scheduler prefer a free resource that did a predecessor of the activity. It must be an option, because it can add idle days.

  The priority list is in the library Zametek.Maths.Graphs (`PriorityListResourceScheduler`, and `scheduling/scheduler.rs` in Rust). Thus, the change starts in the library. Today, `TargetResources` can keep an activity on one resource, but the user must set it on each activity.

- [ ] **Suggestion: give the days on critical activities for each resource** *(2026-11-12, a suggestion from phase 2 of the zpp skill)*

  Righting Software compresses a project with better resources on the critical path only. To select the person to replace with a senior, it counts the critical activities and the critical days of each person. The schedule CSV of `outputs.data` gives `AllocatedToResources` and `TotalSlack` for each activity. Thus, a client must add the durations to get the critical days of a resource. The zpp skill does not add numbers that `zpp` can calculate, so it only lists the critical activities of each resource.

  Add the number of critical activities and the sum of their durations for each resource. For example, add a file `-resources-summary.csv` with the columns `ResourceId`, `Resource`, `CriticalActivities` and `CriticalDays`. Say in the documentation which activities are critical: a total slack of zero, or the critical band of `ActivitySeverities`.

  On 2026-11-14, the maintainer moved this suggestion to a release after 0.11.0.

- [ ] **Suggestion: let a fit of the scenario chart ignore some tracked scenarios** *(2026-11-12, a suggestion from phase 2 of the zpp skill)*

  The scenario chart fits a curve to all the tracked scenarios. The time–cost–risk curve of Righting Software uses one family of plans: the normal plan, its compressed plans and its decompressed plans. Some options are not on that curve, for example a compressed plan with float. In the worked example of the zpp skill, one such option changes the r² of the cubic risk fit from 0.91 to 0.71. Thus, the zpp skill tracks only one family, and the other options are not on the chart.

  Let a person or a client keep a tracked scenario on the chart as a point, but not in the fits. For example, add a flag to the node of the scenario, or fit only the scenarios of one folder.

  On 2026-11-14, the maintainer moved this suggestion to a release after 0.11.0.

- [ ] **Suggestion: keep the late iterations in the statistics of a simulation** *(2026-11-14, a suggestion from phase 3 of the zpp skill)*

  A simulation leaves its failed iterations out of the mean, the percentiles and the chance of the target. The maintainer chose this rule for Zametek.Maths.Graphs 4.1.0 on 2026-11-11, because a compile with the durations of such an iteration gives an error. But an iteration that misses a maximum finish is a late one. Thus, without these iterations, the percentiles are too early, and the chance of the target is too high.

  The zpp skill measured the effect on its claims example, with 5,000 iterations and the seed 1. PaymentsAccess must finish by day 80, and 552 iterations miss this day. Then the chance of a finish by day 172 is 96.6%, and P80 is 165. Without the maximum finish, the chance is 95.1%, and P80 is 166.

  When End must finish by day 166, 977 iterations fail. Each iteration that is left finishes by day 166, and thus the chance of a finish by day 168 is 100%. Without that maximum finish, the chance is 86.9%.

  Since 0.11.0-beta.2, a warning says this next to the statistics. The table "Statistics" of the application and of `zpp` has the row "Warning". `zpp` also writes a line to its standard error stream, with the number of these iterations. The answer of the API has the counts as before (`invalidConstraintsIterations`), and API.md tells what they mean.

  Three more changes are possible:

  - The chance of the target over all the iterations. A failed iteration then counts as a miss of the target. In the example with End, the chance is then 80.5% (4,023 of 5,000).

  - A second run of the plan without its maximum finishes, with the same seed. The durations of an iteration come only from the seed, the order of the activities and their estimates. Thus, each iteration has the same durations in the two runs. The second run gives the statistics, and the first run gives the misses. This doubles the time of a simulation.

  - An option of the simulation in Zametek.Maths.Graphs (4.2.0, in C# and in Rust). It schedules an iteration that misses a maximum finish again, without that maximum. Its late finish goes into the statistics, and it still counts as a miss.

  The last two changes make a maximum finish a deadline in a simulation. In a compile, a maximum finish pins the activity, and a plan that cannot meet it has an error. For a deadline of the whole project, the target of the simulation already gives the chance without this problem. Decide which change to make.

- [ ] **Let the date of the target in the dock "Simulation" write the target date** *(2026-11-15, left from the target date of the simulation)*

  Since 0.11.0-beta.2, `SimulationSettings` can give the target as a date (`TargetFinishDateTime`). The project is done by the end of that date: "done by June 30" is "done by the end of June 30". `zpp`, the server and the application use the date, and a save keeps it. [The project file](PROJECT-FILE.md#times-and-constraints) gives the rule.

  The dock "Simulation" does not write the date. Its date gives a number of days, as before, and a number from the user removes the target date of a file. Thus, a target that a user gives as a date in the dock does not follow a change of the calendar.

  Let the date of the dock write `TargetFinishDateTime`. The date of the dock is the date that the application shows for a finish. Without classic dates, that is the first working day after the target date. The Excel export and import have only the number of days. Decide if they need the date.

- [ ] **Put Inter into the chart SVG files** *(2026-11-12, from the smoke test of v0.11.0-beta)*

  The chart SVG files (Gantt, resource, earned value, scenario and simulation) do not contain the font Inter. Windows 11 does not install Inter. Until 2026-11-12, the SVG named only `Inter`, and thus Microsoft Edge showed the text in its default serif font. Now each text names fallback fonts after Inter (`ChartFonts.SvgFontFamilies`), and Edge shows the text in Segoe UI.

  But the SVG gives the position of each letter from the measurements of Inter, for example `x="0, 8.897727"`. Thus, with a fallback font, the spaces between the letters are not regular. For example, Edge shows "Requirements & Architecture" as "Requirements &Architecture".

  To show the text exactly as the PNG does, put Inter into the SVG with `@font-face`. Each file then becomes larger by the size of the font, unless the export keeps only the letters that the chart uses. Skia can change text to paths in an SVG, but SkiaSharp does not give access to this option (checked in 3.119.4 and 4.148.0).

- ~~**Suggestion: tell how far a plan misses its deadline**~~ DECLINED on 2026-11-12. *(2026-11-11, a suggestion from the design of the zpp skill)*

  A frequent question is "can we finish by day T with these people?". The suggestion was a maximum LFT on the end milestone, with negative total slack as in Microsoft Project. On 2026-11-11, the maintainer decided against it. A maximum LFT pins an activity, and constraints of that kind make the compile too complex. A deadline is only a guideline.

  Add the deadline to the job: an option such as `--deadline` (`deadline` in the API). The plan does not keep it, and the compile does not change. The answer gives the shortfall: the finish of the plan minus the deadline, or 0. A simulation also gives the share of the iterations that finish by the deadline, as the target of a simulation does now.

  On 2026-11-12, the maintainer declined the suggestion, because it adds little to the target of the simulation. The target gives the share of the iterations that finish by it. A client can calculate the shortfall from `projectFinishDays`. A deadline lets a request ask about a date without a change to the plan. It also changes a date to working days in the calendar of the plan. These two gains do not justify the change.

- [ ] **Do not save the calculated `Duration` in the next version of the file format** *(2026-11-11)*

  The estimates are the input: `MostLikelyDuration`, and `OptimisticDuration` and `PessimisticDuration` when the activity has them. `Duration` is the duration that the compiler schedules. It is (O + 4 × ML + P + 3) / 6, which is ML when the activity has no spread. When you edit `Duration` in the application, it sets ML.

  A plan file keeps `Duration` as well as the estimates. When the file has `MostLikelyDuration`, the load calculates `Duration` again and ignores the value in the file. Thus, a program that changes only `Duration` changes nothing ([The project file](PROJECT-FILE.md) says so).

  In the next version of the file format, save only the estimates. Read `Duration` from the earlier versions. Use it as the most likely duration when `MostLikelyDuration` has no value, as the load does now. Do this only when the format changes for another reason, because each new version needs a mapper step. Until then, [The project file](PROJECT-FILE.md) tells a program to set `MostLikelyDuration`, or to leave it out and set `Duration`.

- [ ] **Suggestion: find the decision points of the time–cost–risk curve** *(2026-11-11, a suggestion from the design of the zpp skill)*

  Righting Software gets decision points from a fitted direct cost and a fitted risk. They are the maximum risk, the two risk crossover points and the inflection point of the risk. The inflection point is the target for a decompression. The method also finds the minimum direct cost and the minimum total cost. It also asks what total cost a duration needs, to find the death zone.

  From its own fits, `zpp` can calculate these points and give them as data. The data of the scenario chart (`scenarioChart` in the API) already gives the coefficients of each fit with all their digits. Keep at least six significant digits in each coefficient, because a cubic fit is sensitive to rounding.

  On 2026-11-11, the maintainer moved this suggestion to a release after 0.11.0. The method uses fits of its own against the duration. The direct cost is a quadratic, the indirect cost is a line, the total cost is their sum, and the risk is a cubic. Two questions are still open. Which metric of risk does the cubic fit (for example, an option with the activity risk as the default)? Where do the points go (the answer, the console, a CSV file or the chart of the application)?

- [ ] **Suggestion: compare the tracked scenarios with a baseline** *(2026-11-11, a suggestion from the design of the zpp skill)*

  A decision meeting compares options with the normal plan. It asks how much shorter, how much more expensive and how much riskier each option is. The scenario chart plots the values, but not the differences. An output can list the tracked scenarios with their metrics and their differences from a baseline scenario that the user marks. The compression of an option is then its difference in duration as a percentage.

  On 2026-11-11, the maintainer moved this suggestion to a release after 0.11.0. A question that is still open: how a client marks the baseline. An option of the request, such as `--baseline <scenario>`, does not change the file format. A mark in the plan needs a new version of the format.

- [ ] **Suggestion: a productivity factor for each resource** *(2026-11-11, a suggestion from the design of the zpp skill)*

  Righting Software compresses a plan with better resources. A senior developer finishes an activity sooner, but costs more. zpp cannot show this today. A factor on the resource, for example 0.8, can scale the duration of each activity that the resource does. With the operator `OR`, the duration then depends on the resource that the scheduler selects, and the priority list must allow for this. The simulation and the Rust port must give the same result. Until then, a client gives the activities of the senior shorter estimates and targets them at that resource.

- [ ] **Remove System.Reactive when DynamicData and Dock have no dependency on Rx** *(2026-08-02, last checked 2026-11-20)*

  ReactiveUI 24 no longer uses System.Reactive. But removal has no value while other packages still depend on it. Thus, the assembly ships in all cases.

  **Dock.** Dock now ships two lanes. The Dock documentation recommends the suffixed System.Reactive lane (`Dock.Model.ReactiveUI.Reactive`, with `ReactiveUI.Reactive`). It calls the unsuffixed family the optional ReactiveUI.Primitives lane (`docfx/articles/dock-reactiveui.md` in the Dock repository).

  The unsuffixed lane keeps `System.Reactive`. Version 12.1.5 of `Dock.Model.ReactiveUI` declares `System.Reactive` 7.0.0, and so does the suffixed twin. On Dock master, the props file `build/ReactiveUI.Primitives.props` of the unsuffixed project references `ReactiveUI` and `System.Reactive` together. Thus, the Dock gate can stay closed for good. The earlier expectation, that the next unsuffixed release drops the reference, does not hold.

  **DynamicData.** `DynamicData` is the remaining hard blocker in released packages. Version 9.5.0 (2026-10-13) declares `System.Reactive` 6.1.0, and no suffixed twin exists. PR [reactivemarbles/DynamicData#1116](https://github.com/reactivemarbles/DynamicData/pull/1116) migrates DynamicData to ReactiveUI.Primitives. It is open, and it has conflicts with main.

  On 2026-10-28, a maintainer wrote in this PR that Primitives support will be opt-in. The default `DynamicData` package stays on `System.Reactive`. Primitives support ships as a separate package that consumers choose to reference. Thus, the default package does not become free of Rx.

  On 2026-11-20, version 9.5.0 was still the newest version, and the PR had no later comment. nuget.org had no package of the DynamicData maintainers for Primitives.

  **Watch.** Watch for the separate DynamicData package for Primitives, and for a Dock release whose unsuffixed package drops `System.Reactive`. If both packages keep the dependency, the assembly ships in all cases, and you cannot complete this entry. Then decide whether to delete the entry.

  **Caution for version bumps.** This repository is deliberately in the unsuffixed Primitives lane. Do not adopt a package with the suffix `.Reactive`. The two families have distinct type identities, and you must not mix them.

  **After both gates open.** Examine the migration of the own Rx use of this repository to `ReactiveUI.Primitives`. The migration must close these gaps:

  - `FromEventPattern` does not exist. Two uses are the collection-changed bridges in the effort tracking manager.
  - `Subject` and `BehaviorSubject` do not exist. Primitives has different "Signal" abstractions.
  - `Observable.Create` does not exist. `MuteWhile` uses it.

  **Starting points.** Comments above the `System.Reactive` reference in `Zametek.Graphs.Avalonia.csproj` and the Dock and DynamicData block in `Zametek.ViewModel.ProjectPlan.csproj` mark the places. The bridge `ObservableExtensions.ObserveOn(ISequencer)` is the seam where the unwinding starts.

- [ ] **Remove `SyncedMultiComboBox` when Ursa follows `SelectedItems`** *(2026-10-31)*

  In Ursa 2.2.0, an item of `MultiComboBox` adds its data to `SelectedItems` when it attaches to the logical tree, if its own `IsSelected` is true. The control also stops listening to `SelectedItems` when it unloads, and it does not listen again. The two chart filters use [`SyncedMultiComboBox`](../src/Zametek.View.ProjectPlan/Controls/SyncedMultiComboBox.cs), and its item corrects `IsSelected` first.

  The activity grid, the resource grid and the two edit dialogs still use the plain control. Their items belong to one row or one dialog, and thus a selection cannot go to another scenario. But a selection that changes without its dropdown can possibly come back in the same way. No test shows this yet.

  **Watch.** Check each new version of Ursa. If the control is correct, remove the subclass. [`ChartFilterTests`](../test/Zametek.View.ProjectPlan.Tests/Controls/ChartFilterTests.cs) then show if the plain control is enough.

- [ ] **Spin out Zametek.Graphs.Avalonia into its own repository** *(2026-08-02)*

  The library is already framework-decoupled, and it has its own README. The checklist is in [src/Zametek.Graphs.Avalonia/TODO.md](../src/Zametek.Graphs.Avalonia/TODO.md), so that it stays with the folder.

- [ ] **Formal drag-and-drop from charts** *(2026-08-02, consolidated from a TODO in the source code)*

  `ScottPlotUserControl.CheckPointerDrag` detects the threshold between a click and a drag. But it only tracks the state. The inline comment shows where `DragDrop.DoDragDropAsync` starts a real drag-and-drop operation (for example to drag a chart image into another application).

- [ ] **Reduce UI-thread reads of locked view-model getters** *(2026-08-04)*

  A dotnet-trace profile of a burst of edits showed several seconds of Monitor contention. Bindings read locked getters again while the background compile cascade held the locks. Most of the contention disappeared with the fix for `IsBusy`. If input hitches appear again under heavy background activity, prefer lock-free snapshots (volatile fields or immutable models) for hot bound properties.

- [ ] **Consider a report to Dock about the float and re-dock behavior** *(2026-08-02, optional)*

  Under Dock 12.1, a drop can return a floating tool to the layout. Then Dock materializes the destination while the visual tree of the closed float window is still assembled. Version 12.0.0.2 did not show this. The crash was finally the own pattern of the application: a shared control as `Content`. The fix is by construction in `ScottPlotUserControl`.

  A minimal reproduction is possibly worth a report to Dock. Any dockable that binds a shared control instance as `Content` shows the problem. Then the Dock maintainers can examine the order of the materialization across windows.

- [ ] **Surgery for rule 6: no notifications and no calls to foreign components under a lock** *(2026-08-17)*

  Two deadlocks, which dumps prove, come from cycles in the lock order. Both dumps are from 2026-08-17, during scenario loads. The cycles involve `CoreViewModel.m_Lock`, `ProjectScenarioDisplaySettingsViewModel.m_Lock`, `GanttActivitySelectorViewModel.m_Lock` and the internal `ExpressionChainSink` gates of ReactiveUI.

  The point fixes are already in place:

  - The sweep for lock-free getters (ARCHITECTURE §7 rule 9).
  - The move of the core callbacks of the display settings (`SetIsProjectScenarioUpdated` and `RequestCompilation`) out of all the lock blocks of the setters.
  - Ref-counted guards for revision in the Gantt and earned-value selectors. A shared bool let the `finally` of one reviser clear the guard of the other reviser during the revision, across threads.

  The REMAINING sites that call out under a lock are not live bugs, because every remaining edge is one-way with the point fixes in place. A fix for them is hardening. These sites form the deferred surgery. The list that follows has the items in descending order of value:

  1. `CoreViewModel.ProcessProjectScenario` and `ResetProjectScenario` hold `m_Lock` across the whole fan-out of a load. The fan-out raises the settings setters, raises `TrackersRevision`, and calls `DisplaySettingsViewModel.SetValues`. The `Scheduler.CurrentThread` subscriptions of `TrackersRevision` run the selector revisions synchronously ON the raising thread. Thus, they take selector locks under the core lock. This is the Core→Selector edge in the first dump. Since 2026-11-02, the selector of the Gantt chart only reads the plan there, and it takes its lock on the UI thread (§7 rule 17). The days of the resources still take their locks there. The call to `SetValues` is the Core→DS edge in the second dump.

     The surgery splits each method into two phases. The state phase runs under the lock. The fan-out phase runs after the release. The state phase collects the pending raises and signals, and the fan-out phase emits them outside the lock. §7 rule 7 makes this safe. Handlers read the live state again, because payloads are stale snapshots. The window of `BeginBulkUpdate` and `EndBulkUpdate` wraps both phases, and thus the semantics of the suppression survive.
  2. `ProjectScenarioDisplaySettingsViewModel.SetValues` assigns about 30 raising setters under the DS lock. Thus, every synchronous observer (the sink gates of ReactiveUI) runs under that lock. The surgery sets the fields and collects the names of the changed properties under the lock. Then it raises all of them after the release. This also removes the re-entrant locking of the setters.
  3. The five selector view models change `ObservableCollection`s inside their locked `Set*` methods. Thus, the `CollectionChanged` handlers run under the selector lock. These handlers raise notifications. In the Gantt and earned-value selectors, they also write through to the DS and the core. The surgery has two options. One option detaches the handler around the locked changes and does one manual refresh and raise after the release. The other option computes the change set under the lock and applies it outside.
  4. The plot-model setters of the chart managers (the earned-value, Gantt and resource charts) and `ResourceTrackerSetViewModel.RefreshIndex` raise inside their locks. These are trivial splits: set under the lock, and raise after the release.
  5. Document the global DAG of lock acquisition in ARCHITECTURE §7: Manager→Core→DisplaySettings and Core→Selector, with the node and tracker view models as leaves. Verify that the core never calls back into the scenario manager under its lock. Then future calls across view models have an order to follow.

  **Related problem.** The investigation found one more problem in the same area. The DS setters mark the scenario as modified, also when `RaiseAndSetIfChanged` writes nothing.

  See also the entry of 2026-08-04 above. The sweep for getters already delivered its suggestion of lock-free snapshots for hot bound properties, for the getters of the selectors and the trackers.

  **Direction to explore: delete locks and do not order them** *(2026-08-17)*

  The reactive cascade is not the problem, and it does not need a rewrite. In all three dumps, the dependency graph behaved correctly. The failures were in the locking around it. But the current design is safe only by discipline and not by construction. Three properties combine badly:

  - Each view model has one lock, and calls across view models are free in both directions.
  - Synchronous notification (`PropertyChanged` and `CollectionChanged`) re-enters arbitrary foreign components while it holds locks. The application does not own these locks and cannot inspect them. Examples are the `ExpressionChainSink` gates of ReactiveUI, DynamicData and Dock.
  - The `Scheduler.CurrentThread` subscriptions run on the thread that raised. Thus, no static analysis can tell which thread runs a given piece of work.

  Thus, the lock graph is also not fully knowable. A deadlock stays one refactor away, however carefully the application follows rules 6 and 9.

  The target end state has no locks in the view models. A view model is affine to the UI thread. Background work (parse, compile, metrics) operates on immutable snapshots, as the compiler already does when it clones the graph. It returns one result, and the UI thread applies it. With no locks, there is no lock order to get wrong. Rule 9 then becomes unnecessary and not only enforced.

  A pragmatic option gives 80% of the value, if full affinity is too invasive on the load path. It has exactly ONE lock at the boundary between the core and the model, and no other lock. With rules 6 and 9, this removes by construction every cycle among the own locks of the application.

  The path is incremental, and the team did most of it already. The sweep for getters removed the first class of deadlock for good. The moves in the setters removed the live case of the second class. When rule 6 holds everywhere, most remaining locks in view models will guard only brief writes to fields that happen on one thread. Then you can DELETE these locks and do not need to order them. This is better than a documented lock DAG that someone must maintain for ever, and it makes item 5 above unnecessary.

  Add enforcement by machine, and do not rely on vigilance. `System.Threading.Lock` exposes `IsHeldByCurrentThread`. Thus, assertions only for Debug at the boundaries of raises and callbacks change a latent deadlock into a loud test failure. Without them, you learn about the deadlock from a dump weeks later. This work is now a separate entry below. It has value on its own, and it does not depend on any of the surgery above.

  **The third dump.** The third dump (`zametek-deadlock-2.dmp`, 2026-08-17) is not a deadlock. No thread waited for any lock, and the UI thread was idle. A worker of a scenario load spun inside `PriorityListResourceScheduler.CalculateResourceSchedules`, and nothing was ever scheduled onto any of the 14 resource builders. The investigation of this dump is complete.

  The ClrMD analysis of 2026-08-18 found the root cause of the spin: a torn `HashSet` clone poisoned the `Contains` gate of the scheduler. ARCHITECTURE §7 rule 10 tells the full story, and it shows the race on the ProjectPlan side that tore the clone.

  The scheduler now detects the dead state upstream. Zametek.Maths.Graphs 3.2.0 reports compilation error C0020 and does not spin, and `Compile` takes a cancellation token. The compile watchdog of the application (`CompilationTimeoutMilliseconds`, 2026-08-19) limits all that the scheduler cannot prove dead. The last structural item of this paragraph landed on 2026-10-25: the compile and the cascade now run outside the lock, against a snapshot (§7 rule 14).

  **Size, and a recommendation not to do the surgery as written** *(2026-08-21)*

  The measurement is against the source code as it is now. The surgery spans these parts:

  - `CoreViewModel`: 2,453 lines and 66 `lock (m_Lock)` sites. The fan-out of the load is about ninety lines under one lock. These lines contain two `SetValues` calls, four assignments of settings that each raise and fan out, and the call of `ReviseTrackers()`. The `Scheduler.CurrentThread` subscriptions of this call run selector revisions synchronously on the raising thread.
  - `ProjectScenarioDisplaySettingsViewModel`: 735 lines and 33 lock sites. Its `SetValues` makes sixty property assignments, and each one uses a locked raising setter.
  - The five selector view models: 1,455 lines in total.
  - A handful of trivial splits in the chart managers.
  - The documentation item.

  This is the largest open entry in this file. The risk is higher than the number of lines shows. The change affects WHEN notifications fire in a cascade that already produced three dumps. Verification needs the diff of the CLI worktree and checks of the GUI. The compile work taught that naive concurrency tests have no teeth.

  Two reasons argue against the surgery in this form. First, there is no live bug. The point fixes are in place, and every remaining edge is one-way. Thus, the surgery improves safety and does not fix a bug.

  Second, this entry argues against itself. The direction to explore above makes items 1 to 4 unnecessary and not only done. It also makes item 5 unnecessary: with no locks, there is no DAG of acquisition to document. If the surgery for the lock order comes first, the cost is full for something that the better end state discards.

  Better value was nearby. Phase 2b (run the compile and the cascade outside `m_Lock`) landed on 2026-10-25, and it was larger than this entry expected. The lock did hidden jobs, and counters now do them (§7 rule 14). The pattern that it proves is the one of the direction above: copy under the lock, build without it, and publish with checks.

  **The browser head changes the economics** *(2026-08-21)*

  Single-threaded WebAssembly runs everything on the UI thread in all cases. Thus, "UI-thread affine, no locks, background work on immutable snapshots" is not a costly migration there. The platform already imposes it.

  If the web app becomes worth shipping, this direction is no longer speculative. It becomes the natural shape of the application. Then you must delete this entry. Do not do the work that it lists.

- [ ] **Speed up the calculation of the tracking series** *(2026-10-25)*

  The cascade of a large plan takes about 650 ms on the sample plan with 985 activities (`docs/assets/erp-migration.zpp`). The compile takes 100 to 170 ms. `CalculateTrackingSeriesSet` in `ResourceSchedulingService` takes 310 to 370 ms, and thus it is about half of the cascade. The arrow graph takes 50 to 90 ms, the vertex graph takes 160 to 190 ms, and all the metrics together take 30 to 70 ms.

  *(2026-11-20)* The vertex graph now takes about 30 ms on this plan (26 to 29 ms in 15 runs of a Debug build, where the build as it was took 142 to 158 ms). Since 0.11.0-beta.2, it calculates the critical path of the compiled activities, and it does not schedule the resources a second time. Thus, the cascade is about 130 ms shorter than the 650 ms above. Nobody measured the full cascade again.

  The cascade no longer holds `m_Lock` (ARCHITECTURE §7 rule 14), and thus the time does not block the UI. But it delays the charts after each edit, and a bulk update waits for it. Profile the calculation and look for work that repeats for each activity. A faster tracking series shortens the wait for every chart.

  **A possible cause** *(2026-11-14, from the review of `src`, read in the source code and not measured)*. For the plan series, the method makes the list `progressTimeline`, with one dictionary for each day to the end of the plan. For each activity, it then adds a `TrackingPointModel` to the dictionary of each day. This includes the days before the activity starts and the days after it finishes. A second loop reads each entry of each day two times. For example, 985 activities and 450 days give about 443,000 objects and dictionary entries, also for a plan with no trackers.

  The loops for the progress and the effort read each day again. One pass in the order of the days is enough for the plan series. It adds the activities that start on a day to a running sum, and it removes those that finish. Profile first, because nobody measured which loop takes the time.

- [ ] **Bring the browser head up to the desktop application** *(2026-08-21)*

  `Zametek.ProjectPlan.Browser` now presents the whole application. The dock layout, the data grids, the interactive graphs and the ScottPlot charts all render under WebAssembly. The behavior behind them remains. Seven items remain:

  1. ~~**Split `MainView`**~~ DONE on 2026-08-21. It is now a `UserControl` shell that both heads share. `Zametek.ProjectPlan.Desktop.MainWindow` is the thin `Window` wrapper. It carries the title, the icon and the handlers for the confirmation of closing. `File | Exit` became the event `MainView.ExitRequested`, and the host decides how to honor it. `MainView.CanExit` hides the item where there is no host window.
  2. **Make the file dialogs hand back streams.** The file layer below the dialogs already works in streams *(2026-09-26, phase 1 of the plan for the headless service)*. All of these types read or write a `Stream` that the caller owns, in an explicit format:

     - `IProjectFileOpen` and `IProjectFileSave`.
     - `IProjectScenarioFileImport` and `IProjectScenarioFileExport`.
     - `IXlsxScenarioFileImporter` and `IXlsxScenarioFileExporter`.
     - `IMicrosoftProjectFileImporter`.
     - The `Write*ChartImageAsync` methods of the chart managers.
     - `IInteractiveGraph.WriteImageAsync` of the graph library.

     The desktop and `zpp` open the files at the edge (`FileStreamHelper`, with `FileFormatHelper` reading the format from a file name).

     The dialogs remain. `IDialogService.ShowOpenFileDialogAsync` and `ShowSaveFileDialogAsync`, and `IGraphHost.PickSaveFileAsync` of the graph library, still return a local path. A browser has no local path. It has only opaque `IStorageFile` handles that the application must stream.

     Thus, the dialogs must return a handle. The handle gives a name from which to read the format, and a way to open it for reading or writing. `MainViewModel` and the Save-As paths of the charts and graphs then open the handle and not a path. Until then, the two file methods of `BrowserDialogService` throw, on purpose. A return of null looks the same as a cancellation by the user.
  3. **Persist the settings.** `BrowserSettingService` holds everything for the life of the page. Back it with localStorage or IndexedDB, so that the dock layout, the layouts of the grids, the recents and the preferences survive a reload. `ISettingService` itself has the shape of a path (`SettingsFilename`, `DockLayoutFilename`, `ProjectDirectory`, `RecentProjectFilePaths`). Thus, this is partly a question of the contract and not only of the implementation. The list of recents cannot hold paths in a browser. A handle that the user gave access to is not a name that the application can open again.
  4. **Disable floating dockables.** `DockFactory` sets `CanFloat = true` on 17 dockables. Floating creates host windows, and a browser has none. Nobody tested this so far. The obvious test is to drag a tool out of the layout.
  5. **Confirm the diagnostic channel.** The head configures Serilog to the console, with errors routed to stderr. Nobody verified that this output arrives. During bring-up, neither Serilog output nor a plain `Console.WriteLine` appeared in the browser console. But this observation is not trustworthy. At that time, the tool that read the console showed only `[error]` entries. A direct `console.log` from the page was also invisible until the UI began to compose, and then it appeared normally.

     Treat the channel as UNVERIFIED. Do not treat it as broken. The first step is to open the developer console against a running browser head and see if the startup line is there. The result matters either way. A web app with logging that goes nowhere reports a failure as a blank canvas and nothing else.
  6. **Prune what a browser cannot use.** Remove `Process.Start` in `UriHelper` (use the own navigation of the browser). Remove the file-writing thread of `PerfTelemetry`. Hide the MS Project import from the UI, so that it does not reach `UnavailableMicrosoftProjectFileImporter`.

- [ ] **Decide how to host and deploy the web app** *(2026-08-21)*

  `dotnet publish` makes a static `AppBundle`, but nothing publishes it anywhere. [Build from source](BUILDING.md#the-web-app) already documents two requirements. The first is a secure context (HTTPS or localhost) for the browser APIs that the file pickers need. The second is a `Service-Worker-Allowed: /` header on `_framework/sw.js`, or that script served from the root of the site. Avalonia registers its service worker at the root scope. Without the header, Firefox and Safari lose the fallback for save-file.

  Decide these questions together:

  - Does the release workflow publish the bundle as an artifact?
  - Does GitHub Pages serve the bundle? GitHub Pages cannot send custom headers, and thus the placement of `sw.js` matters. Or does a host that can send headers serve it?
  - Is it useful to configure Brotli precompression? The payload includes the whole runtime.

- [ ] **Reconsider single-threaded WebAssembly when the browser head does real work** *(2026-08-21)*

  The browser head runs single-threaded. This lets any static host serve it. Thus, every `Task.Run` in the layer of the view models runs on the UI thread. There are many, and `RunCompile` is one. The compile of a large plan will freeze the tab.

  The compile watchdog (`CompilationTimeoutMilliseconds`) probably cannot fire at all. Its timer callback has no thread to run on while a synchronous compile holds the only thread. Nobody measured either effect. Measure with a real plan before you decide.

  The alternative is `WasmEnableThreads`. It keeps `Task.Run` truly off the UI thread, and it restores the watchdog. But the host must send COOP/COEP headers, which rules out plain GitHub Pages. The Avalonia configuration is also less well tested by other users.

  The direction "delete locks rather than order them" in the entry for rule 6 above becomes more attractive if the browser stays single-threaded. A layer of view models with affinity to the UI thread has nothing to contend over.

- [ ] **Build the packages as a step of each release** *(2026-11-15, from the audit of the packaging path)*

  The packaging projects are outside the filter `Zametek.ProjectPlan.slnf`, and thus no gate builds them. A fault in that path can stay there in silence. For example, `-Clean` in `build-msix.ps1` deleted nothing from the day that someone wrote it, and it printed "Cleaned obj/bin". The WiX components once took their files from a folder that the publish no longer wrote to.

  On 2026-11-15, an audit read `build-msix.ps1`, the two `.wapproj` files and the WiX project for more faults of this kind. It found two in the script, and both now stop the script. The first was a build that wrote no bundle, or only the bundle of an earlier run. The second was a `bin` folder that `-Clean` could not delete. The script also forces the NuGet restore now (`RestoreForce`). Before, a restore that NuGet did not do made the build fail with `NETSDK1047`.

  After these changes, a run of the script built the two bundles in 7 minutes, directly after an ordinary build of the solution. No package in the bundles had a `.pdb` file.

  A gate in the hook or in CI is not practical. The build needs the MSBuild of Visual Studio with the Windows App Packaging SDK, and it takes minutes. Thus, build the two bundles and the MSI before the tag of each release that has them. The WiX project stops a build for a wrong platform, or with a publish that is older than the code (the target `CheckTheInstallerInputs`). A bundle has no signature unless the script gets `-Sign`.

  **The MSI in the release workflow** *(2026-11-23, from the review of `src`, `test` and `pkg`)*. The WiX project uses `WixToolset.Sdk`, and `dotnet build` builds it with no Visual Studio ([Build from source](BUILDING.md)). Thus, the Windows job of `release.yml` can publish the two programs for x64 and then build the MSI. The release then has an MSI from the same commit as its archives, and a fault in the WiX project stops the release. That is a step of a release, and not a gate of each commit. Nobody tried if a runner of GitHub builds a `.wapproj` project.

- [ ] **Find why a publish of the Browser head waits in the workload restore** *(2026-11-15, from the audit of the packaging path)*

  On 2026-11-15, `dotnet publish -c Release` of `Zametek.ProjectPlan.Browser` wrote "Determining projects to restore...", and then nothing for 11 minutes. Its child process was `dotnet workload restore`, which the target `WasmWorkloadRestore` in `Directory.Build.targets` starts before each restore of the Browser head. The workload was on the computer already. With `WASM_WORKLOAD=0`, which skips the target, the same publish took 4 minutes.

  The hook does not meet this, because it builds with `--no-restore`. The comment of the target says that the installation of a workload needs administrator rights. Thus, the restore possibly waits for a question that a shell with no window cannot show. Find what it waits for. Then give the target a time limit, or let it start the restore only when the workload is not there.

  `make publish-browser` has the same wait. The release workflow does not publish the Browser head.

- [ ] **Add a Debug assertion for the locks that the application holds when it announces a change** *(2026-08-21, extracted from the rule-6 entry as the piece that is worth doing on its own)*

  The two deadlocks of 2026-08-17 were both the own half of an ABBA. One thread raised `PropertyChanged` into an expression-chain sink of ReactiveUI while it held the `m_Lock` of a view model. Another thread was inside the gate of that sink and read again a getter that wanted the same lock.

  Rules 6 and 9 forbid exactly this, and the point fixes removed the live cases. But nothing enforces the rules: no test, no analyzer and no gate. Thus, the real risk is not in the edges that exist now, which are one-way. The risk is a future edit that quietly creates a cycle again.

  A deadlock freezes the application with no exception, no log line and no crash. The only diagnosis is a process dump that someone reads with ClrMD. This has already happened three times.

  `System.Threading.Lock` exposes `IsHeldByCurrentThread`. Thus, an assertion with `[Conditional("DEBUG")]` at the boundary of the announcement changes this whole failure mode to a test failure that names the offending line. The assertion sees only the own locks of the application. It never sees the locks of ReactiveUI, DynamicData or Dock. This is the intent. The application controls its own half, and the rules govern that half.

  The seam is `ViewModelBase`. It is an empty class that derives from `ReactiveObject`, and it sits between ReactiveUI and every view model. Confirm if ReactiveUI offers a suitable override there. If it does not, use a small helper that the raise sites call.

  Size the scope first. The layer of the view models has 31 `Lock` fields. Thus, the assertion needs a cheap way to ask "does this thread hold any of them?". There are two options. One option is a registry of the locks in use. The other option is an assertion in each view model for only its own lock and the shared leaf lock `m_ActivityDataLock`.

  The payoff is immediate and not theoretical. The pre-commit hook builds and tests in Debug, and the test suites drive real scenario loads and compiles through the whole cascade. Thus, a violation shows in the gates and not in a dump weeks later.

  The assertion encodes rules that exist already and does not invent new rules. Rule 11 holds the leaf lock around the writes of an activity on purpose, and never around its announcements. This is exactly the shape that the assertion checks. The work has value whether or not the surgery for rule 6 above ever happens. If the team takes the direction "delete locks rather than order them" instead, these assertions prove that each lock is now removable.

- [ ] **Audit every deferred subscription for §7 rule 7: handlers must read the live state again and not act on the payload** *(2026-09-13, prompted by the wipe of the inter-activity phases, which the team fixed on the same day)*

  Rule 7 says that a value delivered through a subscription is a snapshot of the moment of the raise. It is not a snapshot of the moment of the arrival. Thus, a handler that acts on its payload can act on stale state.

  This is not a theory. A load of a project whose resources carry inter-activity phases silently emptied the phases. The cause was `ResourceSettingsManagerViewModel`. It observed `CoreViewModel.WorkStreamSettings` and pushed the delivered snapshot into each resource.

  During a load, the worker thread writes both settings four times: the reset clears each one, and then the scenario supplies each one. This happens before the UI thread drains either `ObserveOn` queue. The queue for the resource settings is first in the schedule, and thus it drains in full. This correctly rebuilds every resource from the live state.

  Then the queue for the work streams delivers its first snapshot, which is now stale and empty. It reconciled the phases of each resource against no work streams and discarded them. A run of the cascade diagnostics showed all four writes on the worker within 7 ms. The first delivery arrived 470 ms later. The delivery that did the damage had `isStalePayload=True payloadWorkStreams=0 liveWorkStreams=1`.

  The fix for this case is structural. It deletes the subscription. `WorkStreamSettingsManagerViewModel.UpdateWorkStreamSettingsToCore` now pushes into the resources synchronously. This mirrors the push that `CoreViewModel` already makes into the activities. The audit covers the other cases. Four settings managers (graph, holiday, resource and work stream) share the identical shape `Subscribe(rs => { if (m_Current != rs) ProcessSettings(rs); })`, and all four consume the payload.

  These four are much less dangerous than the case that did the damage. `ProcessSettings` rebuilds its whole collection from the payload. Thus, a stale delivery that a real delivery follows still converges on the right answer. The wipe of the phases was different, because the discarded state lived only in the resource view models. The later delivery had no way to restore it.

  But the four are not free. Each stale delivery disposes and rebuilds every managed view model in that collection. Any state of a view model that the payload cannot rebuild goes with them.

  The team did the sweep on 2026-09-13 and closed most of the findings. The text that follows gives the remainder and the findings that are worth keeping. The layer of the view models has 55 deferred subscriptions. Of these, 24 bound their payload.

  **Subscriptions that now read the live state again:**

  - `CoreViewModel.m_CompileOnSettingsUpdateSub`. It took `m_Lock` and read `IsBusy` live, but it trusted the delivered value for the other half of the same condition. Thus, two edits in quick succession compiled twice, and the redundant compile marked the scenario as modified.
  - In all four settings managers, the `ProcessSettings` subscription and the `AreSettingsUpdated` subscription. The second one is included because both `ProcessSettings` and `UpdateXToCore` clear that flag. Thus, a queued `true` often outlived its own clearing. It was then possible for the flag to push view models straight back into the core after a load replaced them.

  **A correction.** An earlier draft of this entry claimed that the `m_Current != rs` guards are dead weight. This is wrong. `UpdateXToCore` assigns one instance to both `m_Current` and the core property. When that raise comes back, the guard compares the same reference and correctly stops the manager.

  The guard prevents a rebuild of the grid of the manager in response to an edit that the manager made itself. Such a rebuild discards the selection and the edits in progress. The guard is dead only in the own comparison of `UpdateXToCore`. There the new instance can never equal the stored instance, because the record equality becomes a reference equality on the `List<T>` members.

  The seeding of the graph layout in `ArrowGraphManagerViewModel` and `VertexGraphManagerViewModel` is also converted. It now reads the current layout of the core. It recognizes its own echo when it compares the layout with the instance that it pushed. This replaces an `m_SuppressNextSeed` bool.

  The next delivery to arrive consumed that bool, and not the echo that the application set it for. A drag landed while the layout change of a load was still in the queue. Then the delivery of the load ate the flag, and the seeding skipped the arrangement that the user had just opened.

  **Two problems outside this rule.** The team fixed both at the same time. Both were in the title pipeline of `MainViewModel`:

  - The pipeline assigned `ProjectTitle` on the taskpool, although it binds to `Window.Title`. Thus, a background thread raised a change notification for a UI binding. This survived on Win32 because `SetWindowText` posts and does not block. The browser head has no taskpool thread at all.
  - The `WhenAnyValue` selector cleared `IsReadyToReviseTitle` from inside itself. Thus, a projection changed one of its two own inputs and re-entered its own chain. It ended only because ReactiveUI collapses the second raise with the same value. The readiness enums exist to work around that behavior.

  **The date subscriptions of the activities.** The team also fixed the date subscriptions of `ManagedActivityViewModel`, under rule 10 and not under this rule. An earlier draft of this entry made two errors about them. It described one subscription where there are two. It also called `ProjectStart` an anchor for the display of dates and not an input of the compiler.

  This understates the effect. The setter runs `SetMinimumEarliestStartTimes` and `SetMaximumLatestFinishTimes`. They write `DependentActivity.MinimumEarliestStartTime` and `MaximumLatestFinishTime`, and the compiler reads both. `m_ProjectStartSub` carried the project start. `m_DateTimeCalculatorCalculatorModeSub` carried the holiday settings and the mode for non-working days. Together they are the calendar along which the application counts those two integers.

  Both subscriptions observed on `Scheduler.CurrentThread`. This scheduler runs inline only while nothing else holds the trampoline of the thread. A write from inside another scheduled delivery is the ordinary case when the cascade runs. It queued the subscriptions behind that delivery. It was then possible for the compile that the same setter armed to start with counts that the application still measured along the previous calendar.

  Three tests make each change from inside `Scheduler.CurrentThread.Schedule` and read the activity before that action returns. Against the source code without the fix, all three tests gave 3 where 2 was correct.

  Both subscriptions are now deleted. All three changes now push in synchronously, as `SetResourceSettings` and `SetWorkStreamSettings` do:

  - `CoreViewModel.ProjectStart` calls `SetProjectStart` on every activity.
  - `CoreViewModel.HolidaySettings` calls `UpdateEarliestStartAndLatestFinishDateTimes`.
  - `ProjectScenarioDisplaySettingsViewModel.NonWorkingDayMode` does the same through a new callback of the core. It is the only writer of the mode of the calculator. The callback takes no lock, on purpose. That setter can still be inside the lock of the display settings, because `SetValues` holds it across the whole batch and `Lock` is re-entrant. A call to `m_Lock` there creates again the edge of lock order from the display settings to the core. This edge deadlocked against `ProcessProjectScenario`.

  The third subscription is on the display mode of the calculator. It stays in place on purpose. It only raises display properties again and changes nothing. Thus, neither rule applies to it.

  **The audit found no problem in everything else:**

  - The subscription for uncompiled activities is the reference implementation, and a comment says so.
  - The subscriptions that followed the readiness flag of the trackers now follow revision counters (§9). The two selectors of activities use the value of `TrackersRevision` only as a trigger, and they read the plan during the raise. The selector of the Gantt chart posts work to the UI thread. On purpose, that work applies the plan of the last revision and not a live read. A load can hold `m_Lock` with an empty plan when the UI thread runs the work (§7 rule 17). The tracker sets and the timesheet follow `GraphCompilation` and `TrackerIndex`, and they read the live state.
  - The title subscription of `MainViewModel` binds the product of its own `Select` and not a snapshot of the state.
  - The internal subscription of `MuteWhile` is the gate at emission time itself.
  - The subscriptions for edge routing of the graphs have no `ObserveOn`, and thus rule 7 never applies to them.

  Pair this audit with the Debug lock assertions above. Both change an invisible rule of order to something that a machine can check. The analyzer is a separate entry below. It is a piece of work in its own right and not a tail on this entry.

  With the date subscriptions of the activities complete, no action remains under this heading. The text remains as the written record of what the sweep found and why each decision went the way it did. It stays here until the analyzer entry can carry it.

- [ ] **Write a Roslyn analyzer for §7 rule 7: STANDALONE WORK, NOT A TAIL ON THE AUDIT** *(2026-09-13)*

  This entry is separate from the sweep above on purpose. Do it as a focused piece of work, and not at the end of an unrelated change. It is a new project in this repository, and a new kind of artifact for it. An analyzer has its own packaging, its own test harness and its own failure modes around false positives. It needs an uninterrupted effort. If you start it as a follow-on to a bug fix, the result is a half-built analyzer that someone disabled.

  The analyzer encodes the rule that the audit found broken again and again. A value delivered through a subscription is a snapshot from the moment of the raise. Thus, a handler that binds its payload and uses it as data can act on stale state. The analyzer must flag a `Subscribe` that reads its lambda parameter on a pipeline that contains an `ObserveOn`. An attribute or a comment marker must suppress the flag. It applies where the delivered value is a product and not a snapshot of the state.

  Two cases must not get a flag. The first case is the title subscription of `MainViewModel`, whose payload is the output of its own `Select`. The second case is the subscriptions for readiness flags. They gate on an enum that triggers on an edge, and its setter raises without a condition.

  A live read there swallows a revision that somebody asked for. Thus, the analyzer must tell a level from an edge, or it pushes people in the wrong direction. This distinction is the hard part of the design. Settle it before you write any source code.

  The reason to do the work is this. The audit found nine violations across five files, in source code that people reviewed many times. The two tests that guard the wipe of the inter-activity phases (`WorkStreamSettingsDeliveryTests`) pin that one case and nothing else. Only an analyzer catches the next case. Pair it with the entry for the Debug lock assertions above. The motivation and the payoff are the same, and one analyzer project can host both checks.

- [ ] **Decide if graph exports with no user interface must show the saved arrangement** *(2026-09-25, raised during the spike of phase 0 for the headless service, and kept on the fixed layout for now)*

  `zpp` exports the arrow and vertex graphs through the fixed-layout path: `WriteFixedLayoutArrowGraphImageAsync` and `WriteFixedLayoutVertexGraphImageAsync`, then `InteractiveGraphViewModel.WriteFixedLayoutImageFormatAsync`, then `MsaglGraphLayoutEngine.RenderSvg` with the static presets `GraphConfigurations.Arrow` and `GraphConfigurations.Vertex`. This path lays out the graph afresh. It ignores the node positions that the project scenario saves. Thus, a graph that the user arranged in the desktop application exports differently from the Save-As of the desktop. The Save-As renders the interactive canvas (`InteractiveGraphRenderer` over `GraphNodes` and `GraphEdges`, or the real templates in raster mode).

  `zpp serve` (2026-10-02) has the same behavior, and so does `zpp --server`. Their parity tests pin their graphs to the graphs of `zpp`. Thus, a switch changes all three at once.

  A switch needs two changes. First, the interactive render must stop its run inside `Dispatcher.UIThread.Invoke`. This is the same hazard as the `Refresh` call that leaks a parked thread for each job, where there is no user interface. Second, a node without a saved position still needs a deterministic fallback, and the same MSAGL layout gives it.

  *(2026-11-23, from the review of `src`, `test` and `pkg`)* The first change is complete. `InteractiveGraphViewModel` now calls the render through `IGraphDispatcher`, and a host with no user interface gives `InlineGraphDispatcher`, which calls it directly. One thing remains. The font cache of `InteractiveGraphRenderer` is a static dictionary for one thread, and two jobs can then use it at the same time. [The TODO file of the library](../src/Zametek.Graphs.Avalonia/TODO.md) has this item.

  A third need is already met. It is to draw with the bundled font through the own font cache of `InteractiveGraphRenderer`, which does not use the typeface providers of Svg.Skia. It came with the bundled fonts on 2026-09-25. A run of the CLI already computes the interactive layout, because the `Refresh` subscription fires. Then the run discards the layout.

- [ ] **Revisit ReadyToRun for the command line tool `zpp`** *(2026-09-25, measured during the spike of phase 0 for the headless service, and left for now)*

  `release.yml` publishes `zpp` as a single file and self-contained, without ReadyToRun. The measurement is on win-x64 with `two-scenarios.zpp`. It is the median of 7 cold runs, after the one-time self-extraction of each variant. With ReadyToRun, the load, the compile and the metrics went from 842 ms to 458 ms. With every export, the time went from 1,841 ms to 874 ms.

  The cost is size. The single file grows from 234 MB to 462 MB. About 100 MB of the growth is R2R output for the IKVM and Java assemblies, which only the MS Project import uses. There is also a `zpp.r2r.dll` of 265 MB. The self-extraction at the first run also takes longer: 5.8 s and not 4.5 s.

  Since `zpp serve` (2026-10-02), the single file also carries ASP.NET Core. This brings its size to 264 MB without ReadyToRun. A script that runs many plans can now send them to one warm server (`zpp --server`). This weakens the case to pay the size of ReadyToRun for the start-up.

  But a run that goes to a warm server still has the own start-up of `zpp`. It takes about a quarter of a second on win-x64 (Release, not a single file). ReadyToRun can possibly shorten this time too. Nobody measured it.

  Try these actions before you decide. Use `PublishReadyToRunExclude` for the IKVM and MPXJ assemblies. Find what produces `zpp.r2r.dll` (it looks like a composite image) and if you can avoid it. Nobody measured the start-up of the desktop application.

- [ ] **Package `zpp serve`** *(2026-10-02, phase 6 of the plan for the headless service)*

  Make a Docker image, a systemd unit, a launchd plist and a Windows Service, with CI to match. The image is the canonical deployment. The data outputs and the graph SVGs are already byte-identical across platforms. But the platform measures or rasterizes the text of other outputs: the charts, in any format, and the raster graphs.

  These outputs are byte-identical only on the same operating system, the same CPU architecture and the same Skia build. The spike of phase 0 found a difference between Windows and Linux. The same bundled font has a measurement that differs by a fraction of a pixel. The anti-aliasing of the glyphs also differs.

  The packaging must get these six points right:

  1. **The shutdown timeout.** A stopped server gives the jobs in progress up to the job time limit to finish. The default limit is 120 s. Docker gives a container only 10 s. systemd gives a unit 90 s by default (`TimeoutStopSec`). Raise each timeout to at least the job limit, or the shutdown kills jobs part way.
  2. **The culture and the time zone.** The image must set them explicitly (`--culture`, `TZ`) and must not inherit them. The default is the culture and zone of the host. The decision of 2026-10-01 made this default, so that a local `zpp serve` agrees with `zpp`.
  3. **The integrations for hosting.** The integrations for systemd and Windows services do nothing in a process that neither of them started. Thus, one binary can carry both.
  4. **The probes.** `/health/live` is for liveness. `/health/ready` is for readiness, and it comes a few seconds after the start-up, when the warm-up is complete.
  5. **The garbage collector.** `zpp` runs the workstation GC. Measure the server GC (`DOTNET_gcServer=1` in the image or in the environment of the service) with a long test of 1,000 jobs before you choose.
  6. **The size of the image.** It carries MPXJ and the Java runtime image of IKVM, although the server refuses MS Project imports. `Zametek.ViewModel.ProjectPlan` references MPXJ.Net. If the importer moves to an assembly of its own, the image can leave them out.

  The packaging must also give the single file a folder. At its first start, the single file unpacks approximately 250 MB into `~/.net/zpp/<hash>`. A service without a home folder cannot write there, for example a systemd unit with `ProtectHome` or `DynamicUser`. Then `zpp` stops with the exit code 159. Set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` in the image and in each unit (refer to [The single file](COMMAND-LINE.md#the-single-file)).

  CI must build and test on Windows, Linux and macOS. It needs exact golden files for the data outputs and the graph SVGs on all platforms. It needs exact golden files for the other outputs on the platform of the image. On other platforms, it needs a perceptual tolerance.

- [ ] **Decide which omissions of `zpp serve` to add** *(2026-10-02, deferred when the team planned phase 4)*

  The design left out each of these items on purpose, and nobody needed any of them yet:

  1. **A mode for stdin and stdout.** This mode takes jobs as JSON lines for scripts, with no port and no key. `zpp --server` over loopback or a Unix domain socket made it redundant (2026-10-02).
  2. **Paths of the own server.** With this option, the server reads plans from directories under a root with an allow-list, and writes outputs to them. Bytes in and bytes out behave the same, whether the caller is local or remote. In Docker, the paths of the host are not the paths of the container.
  3. **MS Project import.** The server refuses it with 415 today, and `zpp --server` refuses to send it and points to `--local`. The import runs MPXJ on the Java runtime of IKVM, and nobody measured its cost at first use. The parsing of untrusted `.mpp` files is a reason to give it the least privilege that a container gives.
  4. **A culture or a time zone for each request.** `DateTimeCalculator` reads its date formats one time for each process, into static fields, from the culture that is current at that time. Thus, it must first take its culture from the scope of the job. A time zone for each request requires everything that reads `TimeZoneInfo.Local` to read the zone of the job.
  5. **Caching of answers.** A job with `now` gets the same answer to the same request on the same server (the same version, culture and time zone). The job id and the ids that the job mints (of an import, or of the upgrade of an old file) are the exceptions. Thus, the server can cache the answer under a hash of the request, the version, the culture and the time zone.

- [ ] **Stop a job from compiling its plan two times** *(2026-10-02, noticed in phase 4, and worth more now that `zpp serve` takes the start-up out of the cost of a job)*

  The load of a plan already compiles it and builds its outputs. `CoreViewModel.ProcessProjectScenario` and `ProcessProjectScenarioImport` both end with `RunCompile()` and `RunBuildCascade()`, whatever `AutoCompile` says. Then `JobRunner.RunAsync` sets the base theme and runs `RunCompile()` and all seven Build\* steps again. The arrow graph compiles the plan once more, and the vertex graph calculates its critical path once more. Thus, every job does all the compiling and building of the load a second time. The sequence came over from `Program.RunAsync` of zpp without a change.

  *(2026-11-20)* Since 0.11.0-beta.2, the vertex graph does not schedule the resources a second time. A second schedule was not always the same as the first, and the graph then showed other times than the list of the activities. `GraphCompilationService.BuildVertexGraph` has the reason.

  Before you remove the second pass, find what it sees that the first pass did not. The application sets the base theme between them. Thus, the removal can need only a change to set the theme before the load.

  Also measure the cost. A warm job without exports takes about 25 ms on the server (`two-scenarios.zpp`, win-x64). Nobody measured how much of this time the second pass takes.

  Verify the change in two ways. Run `zpp --now` on every sample plan with every output, before and after, and compare the files byte for byte. Also run the parity tests.

- [ ] **Run the parallel loop of the simulation on threads of its own** *(2026-11-06, after the measurement of `zpp serve`)*

  The simulator of the library runs its iterations in `Parallel.For`, on the thread pool. The loop keeps each thread that it takes until its end, and other work of the process waits for a thread. `zpp serve` now keeps more threads in the pool (`JobServer.KeepThreadsForRequests`), and so its requests do not wait ([The plan](SIMULATION-PLAN.md#the-server-keeps-threads-for-its-requests) has the measurement). The desktop application does not keep more threads, and a simulation there takes a thread for each processor. Nobody measured if its compilations and its charts wait while a simulation runs.

  A `TaskScheduler` with threads of its own would keep the loop away from the pool. But `Parallel.For` uses `ParallelOptions.TaskScheduler`, or the scheduler of the thread pool when that is null. It does not use the scheduler of the task that calls it (tested on .NET 10, 2026-11-06). The library sets no scheduler. Thus, the change needs an option in the library, for example in `SimulationSettings`, and a release of the library.

- [ ] **Group the critical paths of a simulation in a way that helps for a plan that resources decide** *(2026-10-16)*

  A plan with resources has many critical paths in a simulation, because the resources do the work in a different order in each iteration. For the sample plan, 3,000 iterations give about 475 distinct paths. The six heaviest have 7 % to 8 % of the weight each, and the group "Other paths" has about 54 %. Thus, the chart shows the main chains, but about half of the weight is in one group.

  Until version 4.0.1 of the library, "Other paths" had 92 %, and the eight paths of the chart were all one chain of activities. A defect of the library caused this ([The plan](SIMULATION-PLAN.md#the-application-uses-version-401-of-the-library) tells how).

  Choices: group the paths by the activities that decide the finish (the last activity before the finish that is not shared by all the paths), group them by the first activity that differs, or show the criticality index of the activities as a bar chart next to the histogram. The criticality table already says which activities matter most. On the sample plan (2026-11-05), each of the first two choices gives two groups only. The first gives 39 Manual Polishing and 40 System Testing, with about half each. The second gives 7 Dev Training with 96 % and 10 CM and setup with 4 %.

- [ ] **Count the criticality of the activities with the bits of the library** *(2026-10-16)*

  `SimulationAnalyzer` calls `SimulationResult.IsCritical(iteration, activity)` once for each activity and each iteration. For 1,000,000 iterations of a plan of 2,000 activities, that is two thousand million calls. A method of `SimulationResult` that counts the set bits of one activity over the iterations would take a few seconds less. Measure before the change.

- [ ] **Run the schedule simulation in small batches in the web host** *(2026-10-16)*

  The web host has one thread. The panel runs the simulation with `Task.Run`, and the simulator uses `Parallel.For`. On the web host, both run on the one thread, and the UI freezes until the run ends. A run of 5,000 iterations of a plan like the sample plan takes about 5 seconds on one thread.

  Choices: give the simulator a way to run a number of chunks and return (the chunk is already the unit of the work, with its own seed), and let the panel call it from a timer so that the UI gets the thread between batches. Or hide the panel on the web host until this is done. The result must stay the same, because a chunk does not depend on the thread that runs it.

- [ ] **Show the problems of an import in a window of their own** *(2026-10-17)*

  The desktop shows the first ten problems of a workbook in an error, and it offers to save a CSV report of all of them (`ImportProblemsPresenter`). A window with a grid (sheet, cell, column, row, value, message), with the buttons "Save report" and "Copy", is easier to use when a workbook has hundreds of problems. `ScenarioImportException.Issues` already holds all that the window needs.

- [ ] **Write the report of the import problems from `zpp`** *(2026-10-17)*

  `zpp` writes one line for each problem to the standard error stream, and a server lists the first 100. An option like `--import-report <file>` can write the same CSV report as the desktop (`ImportIssueText.WriteReport`). The server can return it as an output of the job.

- [ ] **Report the problems of an MS Project import in the same way** *(2026-10-17)*

  The import of an Excel workbook collects its problems and cancels (`ScenarioImportException`, `ImportProblemsPresenter`). The import of an MS Project file (`MicrosoftProjectFileImporter`) does not. A file that MPXJ cannot read, or a value that the importer cannot use, fails with the message of the library. The open project stays as it is, but the user sees only this message. The importer can collect its problems as `ImportIssue` records and throw the same exception. Then the desktop, `zpp` and the server show them in the same way. `ImportIssue` has a sheet, a row and a column, and an MS Project file has none of these. Decide first what the place of a problem is: the kind of item (task, resource or calendar), its unique id and the name of the field.

  **What the import drops with no message** *(2026-11-23, from the review of `src`, `test` and `pkg`)*. `MicrosoftProjectFileImporter` keeps only the id of each predecessor. Thus, the import loses the lag of a link and its type, for example start-to-start. It reads one type of constraint, `StartNoEarlierThan`, and it ignores each other type. A plan with such links or constraints thus gets a different schedule after the import, and nothing tells the user. Report each item that the import drops as a warning of the import.

- [ ] **Say where a bad date is in a workbook that counts from 1904** *(2026-10-17)*

  ClosedXML converts a number that has the format of a date while it loads a workbook that counts its dates from 1904. Old Excel for the Mac makes such workbooks. A number that no calendar has then makes the whole file unreadable. The message is "Not a legal OleAut date." In a normal workbook, the reader finds the cell and reports `notADate`.

  One fix is to find the cell in the file when the load fails with this message, and to report it. Another fix is to read the cells of such a workbook without the date conversion of the library.

- [ ] **Let a job make only some of the CSV files of a simulation** *(2026-10-18, from the review of the known gaps)*

  The output `simulation` of a job makes the chart of the simulation and all its CSV files: the statistics, the histogram, the paths, the activities and, for a run, the iterations. At the end of phase 4, a request could ask for each file with an output of its own (`simulationChart`, `simulationStatistics`, `simulationHistogram`, `simulationPaths`, `simulationActivities` and `simulationIterations`). Step 5 of phase 5 (`4e98d8fe`) replaced them with the one output `simulation`. The answer still lists each file as an output of its own, but a request cannot ask for a part. A client that needs only the statistics gets the other files too. `zpp` never had an option for only some of the files.

  The file of the iterations has one row for each iteration, and a run can have 1,000,000 iterations. Thus the files that a client does not need cost most for this file.

  Choices: add the single outputs again, next to `simulation`. Or let `simulation` carry a list of the parts to make (for example `"parts": ["chart", "statistics"]`). The command line needs an option in both cases (for example `--simulation-files statistics,histogram`). Decide first what a request means when it gives `simulation` and a single output together.

  **Where.** `SimulationCsvFile` is a flags enum, and so the engine can already make a set of files. `JobRunner` has the table of `SimulationCsvFile` and `JobOutput`. The change is also in `JobRequest`, in `CompileOptionsHelper` and `Program` of the command line, and in `docs/API.md`, `docs/openapi.yaml` and `docs/COMMAND-LINE.md`. The contract tests of the OpenAPI description change with the API. The workbook of the simulation (`simulationWorkbook`) is an output of its own and is independent of `simulation`, so that a request can already choose between the chart with its five files and one workbook. The same choice for the single files remains.

- [ ] **Make the schedule simulation faster** *(2026-10-18, from the review of the known gaps)*

  Phase 0 found where the time goes, and the maintainer decided that phase 2 builds none of the speed work. [The plan](SIMULATION-PLAN.md#performance-work-to-explore-later) lists four possibilities, with their rough effort and their effect on the accuracy:

  - A mode that keeps the resource order of the compiled plan. It takes 1 to 2 days and is 430 to 1,400 times faster. The results change for a plan that the resources decide.
  - A faster incremental update in the critical-path engine. It takes about a week and is up to 2 to 3 times faster. The results do not change.
  - A flat kernel for the priority list and the scheduler. It takes 2 to 4 weeks for each language. Nobody measured the gain.
  - An approximate priority list. The team does not recommend it, because the loss is not predictable.

  The efforts are estimates, and nobody measured them.

  Start with a measurement when speed becomes a problem. Each exact change must be made in the Rust port too, because the same seed must give the same iterations in both languages.
