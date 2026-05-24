#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('App', 'CommandLine', 'Both')]
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
    # @() forces array context — without it, a single-value split returns a scalar
    # string and $bundleList[0] indexes a character instead of an element.
    $bundleList = @($BundlePlatforms -split '\|' | ForEach-Object { $_.Trim() })
    $Platform = if ($bundleList -contains 'x64') { 'x64' }
                elseif ($bundleList -contains 'x86') { 'x86' }
                else { $bundleList[0] }
    if ($Platform -match '^arm') { $Platform = $Platform.ToUpper() }
}

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$wapprojs = @{
    'App'         = Join-Path $repoRoot 'pkg\Zametek.ProjectPlan.WapPackager\Zametek.ProjectPlan.WapPackager.wapproj'
    'CommandLine' = Join-Path $repoRoot 'pkg\Zametek.ProjectPlan.CommandLine.WapPackager\Zametek.ProjectPlan.CommandLine.WapPackager.wapproj'
}

$selected = if ($Target -eq 'Both') { @('App', 'CommandLine') } else { @($Target) }

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found at $vswhere. Install Visual Studio 2022 or newer."
}
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild -or -not (Test-Path $msbuild)) {
    throw 'Could not locate MSBuild.exe via vswhere.'
}
Write-Host "MSBuild: $msbuild" -ForegroundColor DarkGray

# The wapproj's inner publish builds the EntryPoint csproj per-RID. The csproj's
# project.assets.json must contain targets for every RID in the bundle, otherwise
# NETSDK1047 fires. Setting RuntimeIdentifiers via env var makes restore populate
# all RID targets in one pass without modifying any csproj.
$env:RuntimeIdentifiers = 'win-x64;win-x86;win-arm64'
try {
    foreach ($name in $selected) {
        $proj = $wapprojs[$name]
        if (-not (Test-Path $proj)) { throw "Project not found: $proj" }

        Write-Host "`n=== $name : $(Split-Path $proj -Leaf) ===" -ForegroundColor Cyan

        if ($Clean) {
            $projDir = Split-Path $proj -Parent
            $entryRef = ([xml](Get-Content $proj)).Project.PropertyGroup.EntryPointProjectUniqueName | Where-Object { $_ }
            $entryDir = Split-Path (Join-Path $projDir $entryRef) -Parent
            foreach ($d in @($projDir, $entryDir)) {
                Get-ChildItem -Path $d -Include obj, bin -Directory -ErrorAction SilentlyContinue |
                    ForEach-Object { Remove-Item -Recurse -Force $_.FullName -ErrorAction SilentlyContinue }
            }
            Write-Host "Cleaned obj/bin for $name" -ForegroundColor DarkGray
        }

        $signProp = if ($Sign) { 'True' } else { 'False' }

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
