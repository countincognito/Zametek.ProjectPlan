# Client-server quick start

This walks through running zpp as a server on Windows, over https with a certificate made for the purpose, and sending zpp's runs to it from a second PowerShell window - with everything on the command line and nothing in the environment. For what each option does, and for the server's HTTP API, see [Running zpp as a server](COMMAND-LINE.md#running-zpp-as-a-server).

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

Each run adds a line to the server's log, such as `Job … (/v1/jobs): exit code 0 after 27 ms`.

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

What the server refuses ends the run with exit code 5. `--compile-timeout 0`, which here switches the limit off, is refused - `'compileTimeout' must be from 1 to 60000 milliseconds.` - and so is plain http to the https server, with an error about the connection whose words vary:

```powershell
zpp -i two-scenarios.zpp --compile-timeout 0 --server https://localhost:9770
```

```powershell
zpp -i two-scenarios.zpp --server http://localhost:9770
```

## An API key, if you want one

On `localhost` the server needs no key, but it takes one if it is given one - and a server that other machines can reach must have one. Make a key file:

```powershell
Set-Content -Path api-key.txt -Value "zpp-try-$(New-Guid)"
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

## On Linux

On Linux, the server can listen on a Unix domain socket instead, which needs no certificate: only the user who started the server can connect to it. Build zpp as above, with `/` in place of `\`, after installing the packages [Running on Linux or WSL](BUILDING.md#running-on-linux-or-wsl) lists. Then, from the repository root, give it a short name in each terminal:

```bash
alias zpp="$PWD/src/Zametek.ProjectPlan.CommandLine/bin/Release/net10.0/zpp"
```

In one terminal, start the server:

```bash
zpp serve --listen unix:/tmp/zpp.sock
```

In another, from the folder with the sample files, send it a run - any of the runs above, with `--server unix:/tmp/zpp.sock`:

```bash
zpp -i two-scenarios.zpp --server unix:/tmp/zpp.sock
```

Ctrl+C stops the server, which removes the socket.
