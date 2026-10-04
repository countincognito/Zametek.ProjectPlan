# Client-server quick start

This walks through running zpp as a server on Windows, over https with a certificate made for the purpose, and sending zpp's runs to it from a second PowerShell window - with everything on the command line and nothing in the environment. It ends with the other way to reach a server on the same machine, a Unix domain socket, which needs no certificate, and with the server's HTTP API, which zpp is one client of, called with `curl`. For what each option does, see [Running zpp as a server](COMMAND-LINE.md#running-zpp-as-a-server); for the API, the [API reference](API.md).

You need Windows PowerShell; the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), to build zpp (see [Building from source](BUILDING.md)); and [Git for Windows](https://git-scm.com/download/win), whose `openssl` makes the certificate. After any run, `$LASTEXITCODE` shows the code zpp exited with (see [Exit codes](COMMAND-LINE.md#exit-codes)).

## The sample files

- [two-scenarios.zpp](assets/two-scenarios.zpp) - a small project with two scenarios, Alpha and Beta.
- [broken-dependency.zpp](assets/broken-dependency.zpp) - a project that does not compile: an activity depends on one that is not there.
- [sample.zpp](assets/sample.zpp) - a larger project, with resources and costs: the plan `zpp serve` warms up on.
- [sample.xlsx](assets/sample.xlsx) - `sample.zpp` exported to Excel, to import.

## Set up, once

In a PowerShell window, from the repository root, build zpp. It may print warnings from IKVM, which converts the MS Project reader; they are harmless.

```powershell
dotnet build src\Zametek.ProjectPlan.CommandLine\Zametek.ProjectPlan.CommandLine.csproj -c Release
```

Make a folder for the walkthrough, and copy the sample files into it - or download them into it from the links above:

```powershell
mkdir $HOME\zpp-try
```

```powershell
Copy-Item docs\assets\* $HOME\zpp-try
```

Then make the certificate, in that folder. `openssl` prints rows of dots and pluses while it makes the key.

```powershell
cd $HOME\zpp-try
```

```powershell
& "C:\Program Files\Git\usr\bin\openssl.exe" req -x509 -newkey rsa:2048 -sha256 -days 365 -nodes -keyout zpp-localhost.key -out zpp-localhost.crt -subj "/CN=localhost" -addext "subjectAltName=DNS:localhost,IP:127.0.0.1,IP:::1" -addext "basicConstraints=critical,CA:FALSE" -addext "keyUsage=critical,digitalSignature,keyEncipherment" -addext "extendedKeyUsage=serverAuth"
```

That writes two files:

- `zpp-localhost.crt` - the certificate: for a year, for `localhost`, `127.0.0.1` and `::1`, and for a server only. It is not a certificate authority, so it cannot vouch for any other certificate.
- `zpp-localhost.key` - its private key, unencrypted, so that the server needs no password for it. While you trust the certificate, anyone with this file could pose as `localhost` to your programs: keep it in this folder, and delete it when you are done.

## Window 1: the server

Open a PowerShell window and, from the repository root, give zpp a short name - a PowerShell alias, not an environment variable, which lasts until the window closes - and go to the folder. With a zpp.exe from elsewhere, such as `make publish-cli`, give its path instead.

```powershell
Set-Alias zpp "$PWD\src\Zametek.ProjectPlan.CommandLine\bin\Release\net10.0\zpp.exe"; cd $HOME\zpp-try
```

Start the server:

```powershell
zpp serve --listen https://localhost:9770 --certificate zpp-localhost.crt --certificate-key zpp-localhost.key
```

It says where it listens, and after a few seconds that it is ready:

```text
Now listening on: https://localhost:9770
Application started. Press Ctrl+C to shut down.
Warmed up in 2767 ms: ready for jobs
```

Leave it running: each job it runs adds a line to its log. On `localhost` it needs no API key.

## Window 2: the client, refused for now

Open a second PowerShell window and do the same, from the repository root:

```powershell
Set-Alias zpp "$PWD\src\Zametek.ProjectPlan.CommandLine\bin\Release\net10.0\zpp.exe"; cd $HOME\zpp-try
```

Send a run to the server:

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770
```

It fails with exit code 5, because nothing on this machine trusts the certificate yet:

```text
zpp could not reach the server at https://localhost:9770: The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot
```

## Trust the certificate, for your user only

In either window - no administrator rights needed:

```powershell
Import-Certificate -FilePath .\zpp-localhost.crt -CertStoreLocation Cert:\CurrentUser\Root
```

Windows shows a security warning - that you are about to install a certificate claiming to represent `localhost` - which may open behind the window: choose **Yes**. If `Import-Certificate` will not take the file, `certutil -user -addstore Root zpp-localhost.crt` does the same, with the same warning.

To check that it took - it prints `True`, where before it printed `False`, with a warning that the chain ends in `CERT_TRUST_IS_UNTRUSTED_ROOT`:

```powershell
Test-Certificate -Cert (Get-PfxCertificate .\zpp-localhost.crt) -Policy SSL -DNSName localhost
```

zpp checks the server's certificate against what Windows trusts for the user running it, so run the client as the user who trusted it.

## Window 2: runs on the server

Each run adds a line to the server's log, which begins with the id of the request - the id that an error of zpp's gives, to find the request by - such as `4bf92f3577b34da6a3ce929d0e0e4736 POST /v1/projects/compile: 200 ok, exit code 0, after 27 ms`.

The metrics, as zpp prints them here, with exit code 0:

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770
```

Every output, on the server and here, compared. `--now` fixes the time that the saved project and the workbook record, so that two runs write the same bytes.

```powershell
mkdir out-server, out-here
```

```powershell
zpp -i sample.zpp --now 2026-10-03T09:00:00+01:00 -o out-server\saved.zpp -x out-server\exported.xlsx --gantt-directory out-server --gantt-format png --gantt-size 1600:900 --arrow-directory out-server --arrow-format svg --vertex-directory out-server --vertex-format pdf --resource-directory out-server --resource-format png --resource-size 1600:900 --ev-directory out-server --ev-format svg --ev-size 1200:800 --scenario-chart-directory out-server --scenario-chart-format png --scenario-chart-size 1200:800 --server https://localhost:9770 > server.txt
```

```powershell
zpp -i sample.zpp --now 2026-10-03T09:00:00+01:00 -o out-here\saved.zpp -x out-here\exported.xlsx --gantt-directory out-here --gantt-format png --gantt-size 1600:900 --arrow-directory out-here --arrow-format svg --vertex-directory out-here --vertex-format pdf --resource-directory out-here --resource-format png --resource-size 1600:900 --ev-directory out-here --ev-format svg --ev-size 1200:800 --scenario-chart-directory out-here --scenario-chart-format png --scenario-chart-size 1200:800 > here.txt
```

```powershell
Get-ChildItem out-here | ForEach-Object { "{0,-26} {1}" -f $_.Name, ((Get-FileHash $_.FullName).Hash -eq (Get-FileHash "out-server\$($_.Name)").Hash) }
```

That lists the eight files, each `True`. The text both printed is the same too, which this shows by printing nothing:

```powershell
Compare-Object (Get-Content here.txt) (Get-Content server.txt)
```

The scenarios: the list, the second as JSON, and one that is not there (exit code 1):

```powershell
zpp -i two-scenarios.zpp --list-scenarios --server https://localhost:9770
```

```powershell
zpp -i two-scenarios.zpp -s Beta --metrics-format json --server https://localhost:9770
```

```powershell
zpp -i two-scenarios.zpp -s Gamma --server https://localhost:9770
```

A plan that does not compile - its errors in red, and exit code 3:

```powershell
zpp -i broken-dependency.zpp --server https://localhost:9770
```

A workbook import:

```powershell
zpp -m sample.xlsx --metrics-format table --server https://localhost:9770
```

`-v`, which says where the run went: `Running two-scenarios.zpp on https://localhost:9770`, then `Job … ran on https://localhost:9770: exit code 0`.

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770 -v
```

What the server refuses ends the run with exit code 5. `--compile-timeout 0`, which here switches the limit off, is refused - `The server at https://localhost:9770 did not run the job: The request has a problem: #/options/compileTimeout must be from PT0.001S to PT1M (trace id 0ca7b82bbf8007c789cc71a13f5b7fea)`, which names the option as the API's request does and the request by its id - and so is plain http to the https server, with an error about the connection whose words vary:

```powershell
zpp -i two-scenarios.zpp --compile-timeout 0 --server https://localhost:9770
```

```powershell
zpp -i two-scenarios.zpp --server http://localhost:9770
```

## An API key, if you want one

On `localhost` the server needs no key, but it takes one if it is given one - and a server that other machines can reach must have one, of at least 32 characters. Make a random 256-bit key with Git's `openssl`, which writes it in base64 as 44 characters, into a key file:

```powershell
& "C:\Program Files\Git\usr\bin\openssl.exe" rand -base64 32 | Set-Content -Path api-key.txt -Encoding ascii
```

In window 1, stop the server with Ctrl+C, and start it again with the key:

```powershell
zpp serve --listen https://localhost:9770 --certificate zpp-localhost.crt --certificate-key zpp-localhost.key --api-key-file api-key.txt
```

In window 2, a run without the key now ends with exit code 5 - `The server at https://localhost:9770 needs its API key: set ZPP_API_KEY, or use --api-key-file.` - and one with it runs. A file with the wrong key in it gets exit code 5 too: `The server at https://localhost:9770 did not accept the API key.`

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770
```

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770 --api-key-file api-key.txt
```

## Stop, and clean up

Stop the server with Ctrl+C in window 1: it says `Application is shutting down...`, and exits with code 0. A run sent to it now ends with exit code 5: `No connection could be made because the target machine actively refused it.`

Stop trusting the certificate - Windows may ask you to confirm - after which `Test-Certificate` fails again:

```powershell
Remove-Item -Path "Cert:\CurrentUser\Root\$((Get-PfxCertificate .\zpp-localhost.crt).Thumbprint)"
```

Delete the folder, and the key with it:

```powershell
cd $HOME; Remove-Item -Recurse zpp-try
```

## On a Unix domain socket

A server can be reached without a certificate from the same machine, over a Unix domain socket: a file that programs connect to, in place of a network address. zpp serve leaves the socket to the user who started the server - on Windows as on Linux - so that nobody else can connect to it, and there is no certificate to trust and no key to keep. The address, for `--listen` and for `--server`, is `unix:` and the path of the socket.

This needs zpp built, and the sample files in a folder, as in [Set up, once](#set-up-once) - without the certificate. In window 1, with the alias and in the folder as in [Window 1: the server](#window-1-the-server), listen on a socket in the folder. A relative path is taken from the folder the command runs in:

```powershell
zpp serve --listen unix:zpp.sock
```

The log says where it listens in the web server's words for a socket, `http://unix:` and the path - which is not the address to give zpp:

```text
Now listening on: http://unix:C:\Users\you\zpp-try\zpp.sock
Application started. Press Ctrl+C to shut down.
Warmed up in 2767 ms: ready for jobs
```

In window 2, in the same folder, send it a run - this one, or any of the runs above, with `--server unix:zpp.sock` in place of the https address:

```powershell
zpp -i two-scenarios.zpp --server unix:zpp.sock
```

Only you can connect to the socket. `icacls` shows why: it lists you alone, with full access, `(F)`, and nothing inherited from the folder - an entry marked `(I)`, which a file made in the folder would carry:

```powershell
icacls zpp.sock
```

```text
zpp.sock MYPC\you:(F)

Successfully processed 1 files; Failed processing 0 files
```

Ctrl+C in window 1 stops the server, which removes the socket. For what zpp serve does about a socket that a killed server left behind, and about the other things in its way, see [On a Unix domain socket](COMMAND-LINE.md#on-a-unix-domain-socket).

On Linux the same commands work in a bash terminal. Build zpp as above, with `/` in place of `\`, after installing the packages [Running on Linux or WSL](BUILDING.md#running-on-linux-or-wsl) lists. Then, from the repository root, give zpp a short name in each terminal, and go to a folder with the sample files:

```bash
alias zpp="$PWD/src/Zametek.ProjectPlan.CommandLine/bin/Release/net10.0/zpp"
mkdir -p ~/zpp-try && cp docs/assets/* ~/zpp-try && cd ~/zpp-try
```

The socket is the same file there, and `ls -l zpp.sock` shows `srw-------`: read and write for you alone.

## The API, with curl

zpp is one client of the server's HTTP API, and anything that speaks HTTP is another. This sends projects to a server with `curl.exe`, which Windows 10 and later have - in Windows PowerShell `curl` is another command, so write `curl.exe`. The [API reference](API.md) has every option, answer and problem, and [openapi.yaml](openapi.yaml) describes the API for programs.

It needs zpp built and the sample files in a folder, as in [Set up, once](#set-up-once) - without the certificate. In window 1, with the alias and in the folder as in [Window 1: the server](#window-1-the-server), start a server on `http://localhost:9770`, which needs no certificate and, on this machine, no key:

```powershell
zpp serve
```

In window 2, in the same folder, ask what the server is - its version, the culture and time zone it writes in, and its limits:

```powershell
curl.exe -s http://localhost:9770/v1/info
```

```text
{"version":"0.10.1","culture":"en-GB","timeZone":"Europe/London","limits":{"maxJobs":4,"maxQueue":8,"maxUploadMegabytes":50,"maxChartWidth":5000,"maxChartHeight":5000,"jobTimeout":"PT2M","maxCompileTimeout":"PT1M"}}
```

The server serves its own description too - the `openapi.yaml` of [the API reference](API.md), as it was when the server was built - for any tool that reads OpenAPI. It needs no key:

```powershell
curl.exe -s -o openapi.yaml http://localhost:9770/v1/openapi
```

```powershell
Get-Content openapi.yaml -TotalCount 3
```

```text
openapi: 3.1.0
info:
  title: zpp serve API
```

Compile a project, which a request sends as a file in a part named `project`. The answer is JSON: the project's metrics, here shortened, and the outputs it was asked for, which are none yet:

```powershell
curl.exe -s -F project=@two-scenarios.zpp http://localhost:9770/v1/projects/compile
```

```text
{"metrics":{"activityRisk":1,"activityRiskWithStandardDeviationCorrection":1,"criticalityRisk":1,...,"networkDuration":5,"networkDurationManMonths":0.1643835616438356,"projectFinishDays":5,"projectFinishDate":"2024-01-06",...},"outputs":[]}
```

A request asks for outputs in its options, which are JSON in a part named `options`: zpp's own options, with its names and defaults, less the paths. Put them in a file, which saves quoting JSON for PowerShell:

```powershell
Set-Content -Path options.json -Encoding ascii -Value '{"outputs":{"project":{},"ganttChart":{"format":"png","width":1600,"height":900},"arrowGraph":{"format":"svg"}}}'
```

To get the files themselves, ask for a zip, which holds each output as zpp names it, and `result.json`, which is the JSON answer less the files' contents:

```powershell
curl.exe -s -F project=@two-scenarios.zpp -F "options=@options.json;type=application/json" -H "Accept: application/zip" -o plan.zip http://localhost:9770/v1/projects/compile
```

```powershell
Expand-Archive -Path plan.zip -DestinationPath plan-out; Get-ChildItem plan-out | Select-Object Name, Length
```

```text
Name                    Length
----                    ------
result.json               1028
two-scenarios-arrow.svg   1499
two-scenarios-gantt.png  16998
two-scenarios.zpp        14475
```

The sizes vary with the machine, the project's file most. The scenarios of a project, as `--list-scenarios` lists them:

```powershell
curl.exe -s -F project=@two-scenarios.zpp http://localhost:9770/v1/projects/scenarios
```

```text
{"scenarios":[{"path":"Alpha","id":"8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5","isTracked":true,"isCurrent":true},{"path":"Beta","id":"17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4","isTracked":false,"isCurrent":false}]}
```

What the server cannot answer is a problem, in `application/problem+json`, with the status that says why: a project that does not compile is `422`, with each of its errors listed. `--fail-with-body` makes that a failed command - curl's exit code 22, after it prints the problem - which makes it a gate for a build:

```powershell
curl.exe -s --fail-with-body -F project=@broken-dependency.zpp http://localhost:9770/v1/projects/compile; $LASTEXITCODE
```

```text
{"type":"https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#compilation-failed","title":"The project did not compile","status":422,"detail":"The project has a compilation error.","traceId":"559274683d3853c6df668cfbb50aab4f","errors":[{"pointer":"#/project","code":"P0010","detail":"Invalid activity dependencies:\n999 is invalid but referenced by: 1"}]}
22
```

The `traceId` is the request's id, which every response carries as `Request-Id` and every line of the server's log that belongs to the request begins with.

With an API key - stop the server with Ctrl+C in window 1, make a key, and start it again with it - a request has to carry the key as `Authorization: Bearer <key>`, which is refused without it, with `401`. Make the key, as in [An API key, if you want one](#an-api-key-if-you-want-one), and start the server with it:

```powershell
& "C:\Program Files\Git\usr\bin\openssl.exe" rand -base64 32 | Set-Content -Path api-key.txt -Encoding ascii
```

```powershell
zpp serve --api-key-file api-key.txt
```

```powershell
curl.exe -si http://localhost:9770/v1/info
```

```text
HTTP/1.1 401 Unauthorized
Content-Type: application/problem+json
Date: Sun, 04 Oct 2026 15:51:01 GMT
Cache-Control: no-store
Transfer-Encoding: chunked
WWW-Authenticate: Bearer
Request-Id: 3cfe290945229bb4c5a8a10aca118d87
X-Content-Type-Options: nosniff

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.2","title":"Unauthorized","status":401,"detail":"The request needs the server's API key, as Authorization: Bearer <key>.","traceId":"3cfe290945229bb4c5a8a10aca118d87"}
```

A key on a command line can be read by the other users of the machine, so give `curl` the header from a file, which holds the one line `Authorization: Bearer <key>`:

```powershell
Set-Content -Path key-header.txt -Value "Authorization: Bearer $((Get-Content api-key.txt).Trim())" -Encoding ascii
```

```powershell
curl.exe -s -H "@key-header.txt" http://localhost:9770/v1/info
```

The answer is the same as it was without a key. The one thing that needs no key is the description, `http://localhost:9770/v1/openapi`. The server's log shows what it refused, with the method and the path and never the key - `GET /v1/info: refused, no API key` - and the id of the request, which the `401` carried.

Stop the server with Ctrl+C in window 1, and delete the folder, and the key with it, when you are done.

On Linux the same commands work in a bash terminal, with `curl` and `~/zpp-try`: the options may be given as `-F 'options={"outputs":{"ganttChart":{"format":"png","width":1600,"height":900}}}'`, in single quotes, and a server on a Unix domain socket is reached with `curl --unix-socket /tmp/zpp.sock ... http://localhost/v1/projects/compile`.
