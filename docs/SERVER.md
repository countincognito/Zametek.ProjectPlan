# Client-server quick start

This guide shows how to operate `zpp` as a server on Windows. The server uses HTTPS with a certificate that you make for this purpose. You send the runs of `zpp` to the server from a second PowerShell window. You give everything on the command line, and you set nothing in the environment.

The guide also shows a different method to connect to a server on the same computer: a Unix domain socket. This method needs no certificate. At the end, the guide shows how to use the HTTP API of the server with `curl`. `zpp` is one client of this API.

For information about each option, refer to [zpp as a server](COMMAND-LINE.md#zpp-as-a-server). For information about the API, refer to the [API reference](API.md).

You must have:

- Windows PowerShell
- The [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), to build `zpp` (refer to [Build from source](BUILDING.md))
- [Git for Windows](https://git-scm.com/download/win), because its `openssl` makes the certificate.

After each run, the variable `$LASTEXITCODE` shows the exit code of `zpp` (refer to [Exit codes](COMMAND-LINE.md#exit-codes)).

## The sample files

This guide uses four sample files:

- [two-scenarios.zpp](assets/two-scenarios.zpp) is a small plan with two scenarios, Alpha and Beta.
- [broken-dependency.zpp](assets/broken-dependency.zpp) is a plan that does not compile. One activity depends on a second activity that does not exist.
- [sample.zpp](assets/sample.zpp) is a larger plan, with resources and costs. `zpp serve` uses this plan for its warm-up.
- [sample.xlsx](assets/sample.xlsx) is the plan `sample.zpp`, exported to Excel. You use it for the import.

## One-time preparation

In a PowerShell window, build `zpp` from the root of the repository:

```powershell
dotnet build src\Zametek.ProjectPlan.CommandLine\Zametek.ProjectPlan.CommandLine.csproj -c Release
```

The build can show warnings from IKVM, which converts the MS Project reader. These warnings are harmless.

Make a folder for this guide:

```powershell
mkdir $HOME\zpp-try
```

Copy the sample files into the folder. You can also download them from the links above.

```powershell
Copy-Item docs\assets\* $HOME\zpp-try
```

Go to the folder:

```powershell
cd $HOME\zpp-try
```

Make the certificate. While `openssl` makes the key, it prints rows of dots and pluses:

```powershell
& "C:\Program Files\Git\usr\bin\openssl.exe" req -x509 -newkey rsa:2048 -sha256 -days 365 -nodes -keyout zpp-localhost.key -out zpp-localhost.crt -subj "/CN=localhost" -addext "subjectAltName=DNS:localhost,IP:127.0.0.1,IP:::1" -addext "basicConstraints=critical,CA:FALSE" -addext "keyUsage=critical,digitalSignature,keyEncipherment" -addext "extendedKeyUsage=serverAuth"
```

The command writes two files:

- `zpp-localhost.crt` is the certificate. It is valid for one year, for `localhost`, `127.0.0.1` and `::1`, and for a server only. It is not a certificate authority, and thus it cannot vouch for another certificate.
- `zpp-localhost.key` is the private key of the certificate. The key is unencrypted, and thus the server needs no password for it. While you trust the certificate, anyone with this file can pose as `localhost` to your programs. Keep the file in this folder, and delete it at the end of this guide.

## Window 1: the server

Open a PowerShell window. From the root of the repository, enter this command. It gives `zpp` a short name and goes to the folder. The short name is a PowerShell alias and not an environment variable, and it stays until you close the window. If you have a `zpp.exe` from a different location, for example from `make publish-cli`, give its path in the alias.

```powershell
Set-Alias zpp "$PWD\src\Zametek.ProjectPlan.CommandLine\bin\Release\net10.0\zpp.exe"; cd $HOME\zpp-try
```

Start the server:

```powershell
zpp serve --listen https://localhost:9770 --certificate zpp-localhost.crt --certificate-key zpp-localhost.key
```

The server shows where it listens. After some seconds, it shows that it is ready:

```text
Now listening on: https://localhost:9770
Application started. Press Ctrl+C to shut down.
Warmed up in 2767 ms: ready for jobs
```

Do not stop the server. Each job that the server processes adds a line to its log. On `localhost`, the server needs no API key.

## Window 2: the client, refused for now

Open a second PowerShell window. From the root of the repository, enter the same command as in window 1:

```powershell
Set-Alias zpp "$PWD\src\Zametek.ProjectPlan.CommandLine\bin\Release\net10.0\zpp.exe"; cd $HOME\zpp-try
```

Send a run to the server:

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770
```

The run fails with exit code 5, because nothing on this computer trusts the certificate yet. The message is:

```text
zpp could not reach the server at https://localhost:9770: The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot
```

## Trust the certificate, for your user only

In window 1 or window 2, enter this command. It does not need administrator rights:

```powershell
Import-Certificate -FilePath .\zpp-localhost.crt -CertStoreLocation Cert:\CurrentUser\Root
```

Windows shows a security warning. The warning says that you are about to install a certificate that claims to represent `localhost`. The warning can open behind the window. Select **Yes**. If `Import-Certificate` does not accept the file, `certutil -user -addstore Root zpp-localhost.crt` does the same, with the same warning.

To make sure that Windows trusts the certificate now, enter this command. It prints `True`. Before you trusted the certificate, it printed `False`, with a warning that the chain ends in `CERT_TRUST_IS_UNTRUSTED_ROOT`:

```powershell
Test-Certificate -Cert (Get-PfxCertificate .\zpp-localhost.crt) -Policy SSL -DNSName localhost
```

`zpp` compares the certificate of the server with the certificates that Windows trusts for the user of `zpp`. Thus, run the client as the user who trusted the certificate.

## Window 2: runs on the server

Each run adds a line to the log of the server. The line begins with the id of the request. An error message of `zpp` gives the same id, and you can use it to find the request. For example: `4bf92f3577b34da6a3ce929d0e0e4736 POST /v1/projects/compile: 200 ok, exit code 0, after 27 ms`.

Send a run. `zpp` prints the metrics here, and the exit code is 0:

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770
```

Now compare each output of the server with the same output of a run on this computer. The option `--now` sets the time that the saved plan and the workbook record. Thus, two runs write the same bytes. Make a folder for the outputs of each run:

```powershell
mkdir out-server, out-here
```

Send a run to the server. The outputs go to `out-server`:

```powershell
zpp -i sample.zpp --now 2026-10-03T09:00:00+01:00 -o out-server\saved.zpp -x out-server\exported.xlsx --gantt-directory out-server --gantt-format png --gantt-size 1600:900 --arrow-directory out-server --arrow-format svg --vertex-directory out-server --vertex-format pdf --resource-directory out-server --resource-format png --resource-size 1600:900 --ev-directory out-server --ev-format svg --ev-size 1200:800 --scenario-chart-directory out-server --scenario-chart-format png --scenario-chart-size 1200:800 --server https://localhost:9770 > server.txt
```

Do the same run on this computer. The outputs go to `out-here`:

```powershell
zpp -i sample.zpp --now 2026-10-03T09:00:00+01:00 -o out-here\saved.zpp -x out-here\exported.xlsx --gantt-directory out-here --gantt-format png --gantt-size 1600:900 --arrow-directory out-here --arrow-format svg --vertex-directory out-here --vertex-format pdf --resource-directory out-here --resource-format png --resource-size 1600:900 --ev-directory out-here --ev-format svg --ev-size 1200:800 --scenario-chart-directory out-here --scenario-chart-format png --scenario-chart-size 1200:800 > here.txt
```

Compare each file in `out-here` with the file in `out-server`:

```powershell
Get-ChildItem out-here | ForEach-Object { "{0,-26} {1}" -f $_.Name, ((Get-FileHash $_.FullName).Hash -eq (Get-FileHash "out-server\$($_.Name)").Hash) }
```

The command lists the eight files, and each file has the value `True`. The text that both runs printed is also the same. The next command shows this when it prints nothing:

```powershell
Compare-Object (Get-Content here.txt) (Get-Content server.txt)
```

List the scenarios of a plan:

```powershell
zpp -i two-scenarios.zpp --list-scenarios --server https://localhost:9770
```

Print the metrics of the second scenario as JSON:

```powershell
zpp -i two-scenarios.zpp -s Beta --metrics-format json --server https://localhost:9770
```

Request a scenario that does not exist. The exit code is 1:

```powershell
zpp -i two-scenarios.zpp -s Gamma --server https://localhost:9770
```

Send a plan that does not compile. `zpp` shows its errors in red, and the exit code is 3:

```powershell
zpp -i broken-dependency.zpp --server https://localhost:9770
```

Import a workbook:

```powershell
zpp -m sample.xlsx --metrics-format table --server https://localhost:9770
```

Use the option `-v` to see where the run went. `zpp` prints `Running two-scenarios.zpp on https://localhost:9770` and then `Job … ran on https://localhost:9770: exit code 0`:

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770 -v
```

When the server refuses a run, the run ends with exit code 5. These two commands show this. The first command uses `--compile-timeout 0`, which here disables the limit. The server refuses it:

```powershell
zpp -i two-scenarios.zpp --compile-timeout 0 --server https://localhost:9770
```

The message is: `The server at https://localhost:9770 did not run the job: The request has a problem: #/options/compileTimeout must be from PT0.001S to PT1M (trace id 0ca7b82bbf8007c789cc71a13f5b7fea)`. The message names the option as the request of the API names it, and it gives the id of the request.

The server also refuses plain HTTP, because it uses HTTPS. The error is about the connection, and its words vary:

```powershell
zpp -i two-scenarios.zpp --server http://localhost:9770
```

## An API key, if you want one

On `localhost`, the server needs no key, but it accepts one. A server that other computers can connect to must have a key of a minimum of 32 characters. Use the `openssl` of Git to make a random 256-bit key. It writes the key in base64 as 44 characters. Write the key to a key file:

```powershell
& "C:\Program Files\Git\usr\bin\openssl.exe" rand -base64 32 | Set-Content -Path api-key.txt -Encoding ascii
```

In window 1, stop the server with Ctrl+C. Then start it again with the key:

```powershell
zpp serve --listen https://localhost:9770 --certificate zpp-localhost.crt --certificate-key zpp-localhost.key --api-key-file api-key.txt
```

In window 2, send a run without the key. The run ends with exit code 5, and the message is: `The server at https://localhost:9770 needs its API key: set ZPP_API_KEY, or use --api-key-file.`

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770
```

Send a run with the key. The run operates correctly:

```powershell
zpp -i two-scenarios.zpp --server https://localhost:9770 --api-key-file api-key.txt
```

A key file with an incorrect key also gives exit code 5. The message is: `The server at https://localhost:9770 did not accept the API key.`

## Stop the server and remove the files

In window 1, stop the server with Ctrl+C. The server prints `Application is shutting down...` and exits with code 0. A run that you send to the server after this step ends with exit code 5. The message is: `No connection could be made because the target machine actively refused it.`

Remove the certificate from the trusted certificates. Windows can ask for your approval. After this step, `Test-Certificate` fails again:

```powershell
Remove-Item -Path "Cert:\CurrentUser\Root\$((Get-PfxCertificate .\zpp-localhost.crt).Thumbprint)"
```

Delete the folder. This also deletes the key:

```powershell
cd $HOME; Remove-Item -Recurse zpp-try
```

## On a Unix domain socket

You can connect to a server on the same computer without a certificate. Use a Unix domain socket for this. A socket is a file that programs connect to, in place of a network address.

`zpp serve` gives the socket to the user who started the server, on Windows and on Linux. Thus, nobody else can connect to the socket. There is no certificate to trust and no key to keep. The address for `--listen` and for `--server` is `unix:` and the path of the socket.

Before you start, build `zpp` and put the sample files in a folder, as in [One-time preparation](#one-time-preparation). You do not need the certificate. In window 1, enter the alias command and go to the folder, as in [Window 1: the server](#window-1-the-server). Then listen on a socket in the folder. A relative path starts at the folder where you enter the command:

```powershell
zpp serve --listen unix:zpp.sock
```

The log shows where the server listens. It uses the words of the web server for a socket: `http://unix:` and the path. This is not the address that you give to `zpp`:

```text
Now listening on: http://unix:C:\Users\you\zpp-try\zpp.sock
Application started. Press Ctrl+C to shut down.
Warmed up in 2767 ms: ready for jobs
```

In window 2, go to the same folder and send a run to the server. Use this run, or one of the runs above with `--server unix:zpp.sock` in place of the HTTPS address:

```powershell
zpp -i two-scenarios.zpp --server unix:zpp.sock
```

Only you can connect to the socket. The command `icacls` shows the reason. It lists you alone, with full access, `(F)`. The list has no entry that the socket inherits from the folder. An inherited entry has the mark `(I)`, and a file that you make in the folder has such an entry:

```powershell
icacls zpp.sock
```

The output is:

```text
zpp.sock MYPC\you:(F)

Successfully processed 1 files; Failed processing 0 files
```

To stop the server, press Ctrl+C in window 1. The server removes the socket. If a program kills the server, the socket can stay behind. Other causes can also stop the server from using the socket. For what `zpp serve` does in these cases, refer to [On a Unix domain socket](COMMAND-LINE.md#on-a-unix-domain-socket).

On Linux, the same commands operate in a bash terminal. Build `zpp` as above, with `/` in place of `\`. Before the build, install the packages that the section [Linux and WSL](BUILDING.md#linux-and-wsl) lists. Then, in each terminal, from the root of the repository, give `zpp` a short name and go to a folder with the sample files:

```bash
alias zpp="$PWD/src/Zametek.ProjectPlan.CommandLine/bin/Release/net10.0/zpp"
mkdir -p ~/zpp-try && cp docs/assets/* ~/zpp-try && cd ~/zpp-try
```

The socket is the same type of file on Linux. The command `ls -l zpp.sock` shows `srw-------`. This means that you alone have permission to read and write the socket.

## The API, with curl

`zpp` is one client of the HTTP API of the server. Each program that speaks HTTP is another client. This section sends plans to a server with `curl.exe`. Windows 10 and later have this program. In Windows PowerShell, `curl` is a different command, and thus you must write `curl.exe`.

The [API reference](API.md) lists each option, answer and problem. The file [openapi.yaml](openapi.yaml) describes the API for programs.

Before you start, build `zpp` and put the sample files in a folder, as in [One-time preparation](#one-time-preparation). You do not need the certificate. In window 1, enter the alias command and go to the folder, as in [Window 1: the server](#window-1-the-server). Then start a server on `http://localhost:9770`. It needs no certificate, and on this computer it needs no key:

```powershell
zpp serve
```

In window 2, in the same folder, ask the server for information about itself. The information has the version of the server, the culture and time zone for its output, and its limits:

```powershell
curl.exe -s http://localhost:9770/v1/info
```

```text
{"version":"0.10.1","culture":"en-GB","timeZone":"Europe/London","limits":{"maxJobs":4,"maxQueue":8,"maxUploadMegabytes":50,"maxChartWidth":5000,"maxChartHeight":5000,"jobTimeout":"PT2M","maxCompileTimeout":"PT1M"}}
```

The server also supplies its own description, for each tool that reads OpenAPI. This description is the file `openapi.yaml` of the [API reference](API.md), in the version from the time when you built the server. A request for it needs no key:

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

To compile a plan, send it as a file in a part named `project`. The answer is JSON. It contains the metrics of the plan, shortened here, and the outputs that the request asked for. This request asked for no outputs:

```powershell
curl.exe -s -F project=@two-scenarios.zpp http://localhost:9770/v1/projects/compile
```

```text
{"metrics":{"activityRisk":1,"activityRiskWithStandardDeviationCorrection":1,"criticalityRisk":1,...,"networkDuration":5,"networkDurationManMonths":0.1643835616438356,"projectFinishDays":5,"projectFinishDate":"2024-01-06",...},"outputs":[]}
```

A request names the outputs in its options. The options are JSON in a part named `options`. They are the options of `zpp`, with the same names and defaults, but without the paths. Put the options in a file. This prevents problems with the quotation marks of JSON in PowerShell:

```powershell
Set-Content -Path options.json -Encoding ascii -Value '{"outputs":{"project":{},"ganttChart":{"format":"png","width":1600,"height":900},"arrowGraph":{"format":"svg"}}}'
```

To get the files, request a zip file. The zip file contains each output with the name that `zpp` gives it. It also contains `result.json`, which is the JSON answer without the contents of the files:

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

The sizes vary between computers. The size of the plan file varies most. To list the scenarios of a plan, as `--list-scenarios` does, enter:

```powershell
curl.exe -s -F project=@two-scenarios.zpp http://localhost:9770/v1/projects/scenarios
```

```text
{"scenarios":[{"path":"Alpha","id":"8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5","isTracked":true,"isCurrent":true},{"path":"Beta","id":"17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4","isTracked":false,"isCurrent":false}]}
```

When the server cannot answer a request, it sends a problem in `application/problem+json`. The status shows the reason. For example, a plan that does not compile has status `422`, and the problem lists each error of the plan. The option `--fail-with-body` makes the command fail. The exit code of `curl` is 22, and `curl` prints the problem first. Thus, you can use the command as a gate in a build:

```powershell
curl.exe -s --fail-with-body -F project=@broken-dependency.zpp http://localhost:9770/v1/projects/compile; $LASTEXITCODE
```

```text
{"type":"https://github.com/countincognito/Zametek.ProjectPlan/blob/develop/docs/API.md#compilation-failed","title":"The project did not compile","status":422,"detail":"The project has a compilation error.","traceId":"559274683d3853c6df668cfbb50aab4f","errors":[{"pointer":"#/project","code":"P0010","detail":"Invalid activity dependencies:\n999 is invalid but referenced by: 1"}]}
22
```

The `traceId` is the id of the request. Each response has this id in the header `Request-Id`. Each line of the log of the server that belongs to the request begins with the same id.

You can also use an API key with the API. Then each request must have the key in the header `Authorization: Bearer <key>`, and the server refuses a request without the key, with status `401`. In window 1, stop the server with Ctrl+C. Make a key as in [An API key, if you want one](#an-api-key-if-you-want-one):

```powershell
& "C:\Program Files\Git\usr\bin\openssl.exe" rand -base64 32 | Set-Content -Path api-key.txt -Encoding ascii
```

Start the server again with the key:

```powershell
zpp serve --api-key-file api-key.txt
```

In window 2, send a request without the key:

```powershell
curl.exe -si http://localhost:9770/v1/info
```

The server refuses the request:

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

Other users of the computer can read a key on a command line. Thus, give `curl` the header from a file. The file has one line: `Authorization: Bearer <key>`:

```powershell
Set-Content -Path key-header.txt -Value "Authorization: Bearer $((Get-Content api-key.txt).Trim())" -Encoding ascii
```

Send a request with the key:

```powershell
curl.exe -s -H "@key-header.txt" http://localhost:9770/v1/info
```

The answer is the same as the answer without a key. Only the description needs no key: `http://localhost:9770/v1/openapi`. The log of the server shows each refused request with the method and the path, such as `GET /v1/info: refused, no API key`. The log does not show the key. It also shows the id of the request, which the response `401` carried.

At the end, stop the server with Ctrl+C in window 1. Delete the folder, and the key with it.

On Linux, the same commands operate in a bash terminal, with `curl` and `~/zpp-try`. You can give the options in single quotes: `-F 'options={"outputs":{"ganttChart":{"format":"png","width":1600,"height":900}}}'`. To reach a server on a Unix domain socket, use `curl --unix-socket /tmp/zpp.sock ... http://localhost/v1/projects/compile`.
