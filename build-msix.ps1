#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Desktop', 'CommandLine', 'Both')]
    [string]$Target = 'Both',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$BundlePlatforms = 'x86|x64|arm64',

    [ValidateSet('SideloadOnly', 'StoreUpload')]
    [string]$BuildMode = 'SideloadOnly',

    [string]$Platform,

    [switch]$Sign,

    [switch]$Clean
)

# Build Platform must be one of the platforms in the bundle list, otherwise APPX3104.
# Prefer x64 if present, else first listed platform. ARM/ARM64 must be upper-cased for MSBuild.
if (-not $Platform) {
    # @() forces array context - without it, a single-value split returns a scalar
    # string and $bundleList[0] indexes a character instead of an element.
    $bundleList = @($BundlePlatforms -split '\|' | ForEach-Object { $_.Trim() })
    $Platform = if ($bundleList -contains 'x64') { 'x64' }
                elseif ($bundleList -contains 'x86') { 'x86' }
                else { $bundleList[0] }
    if ($Platform -match '^arm') { $Platform = $Platform.ToUpper() }
}

$ErrorActionPreference = 'Stop'

# This script lives in the repository root, alongside the makefile, so the root is simply where it sits.
$repoRoot = $PSScriptRoot
$wapprojs = @{
    'Desktop'     = Join-Path $repoRoot 'pkg\Zametek.ProjectPlan.Desktop.WapPackager\Zametek.ProjectPlan.Desktop.WapPackager.wapproj'
    'CommandLine' = Join-Path $repoRoot 'pkg\Zametek.ProjectPlan.CommandLine.WapPackager\Zametek.ProjectPlan.CommandLine.WapPackager.wapproj'
}

$selected = if ($Target -eq 'Both') { @('Desktop', 'CommandLine') } else { @($Target) }

# Deletes the bin directly beneath each given project directory.
#
# Written with an explicit name test rather than 'Get-ChildItem -Include bin,obj -Directory', which is
# what this used to do and which silently matched nothing: -Include filters the leaf of -Path rather than
# the children unless the path itself ends in a wildcard, and even then it does not combine with
# -Directory here. The result was a -Clean switch that printed "Cleaned" and deleted nothing at all.
#
# obj is deliberately left alone, despite what a clean would normally mean. MSBuild evaluates every
# project before running any target, and that evaluation imports obj\<project>.nuget.g.props, which
# restore is what writes; against an empty obj the DesktopBridge targets read the entry point off an
# evaluation that predates the restore and the build dies with MSB4024, unable to find a file the same
# command was about to generate. Splitting restore into its own invocation solves that but then leaves
# the per-RID targets unpopulated, because the RuntimeIdentifiers environment variable that populates
# them does not survive as a plain '/t:Restore', and forcing it as a global property instead makes the
# SDK read the whole list as one identifier (NETSDK1083). bin is where the staleness that actually
# matters lives - the published output and IKVM's native image - so bin is what this clears.
function Remove-BuildOutput {
    param([string[]]$ProjectDirectory)

    foreach ($dir in $ProjectDirectory) {
        if (-not (Test-Path $dir)) { continue }
        Get-ChildItem -Path $dir -Directory -Force |
            Where-Object { $_.Name -eq 'bin' } |
            ForEach-Object { Remove-Item -Recurse -Force $_.FullName -ErrorAction SilentlyContinue }
    }
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found at $vswhere. Install Visual Studio 2022 or newer."
}
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild -or -not (Test-Path $msbuild)) {
    throw 'Could not locate MSBuild.exe via vswhere.'
}
Write-Host "MSBuild: $msbuild" -ForegroundColor DarkGray

# Cleaning happens once, up front, rather than per target inside the build loop: the two packaging
# projects share every library beneath them, so cleaning before each one would delete the output the
# previous target had just produced.
#
# The scope is every project in src, not merely the packaging project and its entry point. The libraries
# are where IKVM stages its native runtime image, and residue there is exactly what lets a "clean" build
# reproduce a previous run's output - which it did, silently, for as long as this switch was inert.
if ($Clean) {
    $cleanDirs = @($selected | ForEach-Object { Split-Path $wapprojs[$_] -Parent })
    $cleanDirs += Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory | Select-Object -ExpandProperty FullName
    Remove-BuildOutput $cleanDirs
    Write-Host "Cleaned bin for $($selected -join ', ') and every project in src" -ForegroundColor DarkGray
}

# The wapproj's inner publish builds the EntryPoint csproj per-RID. Every project in the graph must
# therefore have a project.assets.json carrying a target for each RID in the bundle, or NETSDK1047
# fires - naming a project that looks entirely innocent. Declaring the RIDs here populates them all in
# one restore without modifying any csproj.
#
# It has to be the environment rather than an MSBuild property: as a global property it cannot be
# overridden by the per-RID inner publishes, which need to set a single RuntimeIdentifier of their own,
# and the SDK then reads the entire list as one identifier and rejects it (NETSDK1083).
$env:RuntimeIdentifiers = 'win-x64;win-x86;win-arm64'
try {
    foreach ($name in $selected) {
        $proj = $wapprojs[$name]
        if (-not (Test-Path $proj)) { throw "Project not found: $proj" }

        Write-Host "`n=== $name : $(Split-Path $proj -Leaf) ===" -ForegroundColor Cyan

        $signProp = if ($Sign) { 'True' } else { 'False' }

        # '/restore' rather than a separate '/t:Restore' invocation: the switch runs restore in its own
        # pass and re-evaluates before building, which is what gets the RuntimeIdentifiers environment
        # variable into the assets files. A standalone restore target does not.
        $msbuildArgs = @(
            $proj,
            '/restore',
            '/t:Build',
            "/p:Configuration=$Configuration",
            "/p:Platform=$Platform",
            "/p:AppxBundlePlatforms=$BundlePlatforms",
            '/p:AppxBundle=Always',
            "/p:UapAppxPackageBuildMode=$BuildMode",
            "/p:AppxPackageSigningEnabled=$signProp",
            '/v:minimal',
            '/nologo'
        )
        & $msbuild @msbuildArgs
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $name (exit $LASTEXITCODE)" }

        $appPkgs = Join-Path (Split-Path $proj -Parent) 'AppPackages'
        $bundle = Get-ChildItem -Path $appPkgs -Recurse -Filter '*.msixbundle' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($bundle) {
            Write-Host "Bundle: $($bundle.FullName)" -ForegroundColor Green
        } else {
            Write-Warning "No .msixbundle found under $appPkgs"
        }
    }
}
finally {
    Remove-Item Env:RuntimeIdentifiers -ErrorAction SilentlyContinue
}
