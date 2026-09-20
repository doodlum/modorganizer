#!/usr/bin/env pwsh
# Windows counterpart of run.sh. Keeps the same contract: a repository-local SDK
# under .tools/dotnet wins over the machine one, Release unless
# MO2_BUILD_CONFIGURATION says otherwise, and every argument is forwarded to the host.
#
# Architecture note: MnemonicDB's RocksDbNative dependency ships no win-arm64
# binary (linux-arm64, linux-x64, osx-arm64, osx-x64 and win-x64 only), so a
# native ARM64 build fails at startup in RocksDbSharp.Native's type initializer.
# On ARM64 Windows we therefore build and run win-x64 under emulation, which
# needs the x64 .NET runtime installed alongside the ARM64 one. Set
# MO2_RUNTIME_IDENTIFIER to override.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$frontendRoot = $PSScriptRoot
$localDotnet = Join-Path $frontendRoot '..\.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localDotnet) {
    $env:DOTNET_ROOT = (Resolve-Path (Join-Path $frontendRoot '..\.tools\dotnet')).Path
    $env:Path = "$env:DOTNET_ROOT;$env:Path"
    $dotnetCmd = (Resolve-Path $localDotnet).Path
} else {
    $dotnetCmd = 'dotnet'
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$configuration = if ($env:MO2_BUILD_CONFIGURATION) { $env:MO2_BUILD_CONFIGURATION } else { 'Release' }
$project = Join-Path $frontendRoot 'MockHost\MockHost.csproj'

$rid = $env:MO2_RUNTIME_IDENTIFIER
if (-not $rid -and [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq 'Arm64') {
    $rid = 'win-x64'
}

if ($rid) {
    # Not dotnet run: that starts the app through the SDK's own host, which is
    # this machine's architecture. On ARM64 it therefore runs an -r win-x64
    # build as ARM64 anyway, and the app dies in RocksDbSharp.Native's type
    # initializer exactly as a native ARM64 build does. Build, then start the
    # apphost the build produced — that exe carries the target architecture and
    # finds the matching runtime installed beside this one.
    $output = & $dotnetCmd build $project --configuration $configuration -r $rid --no-self-contained --nologo -getProperty:RunCommand
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    # -getProperty prints the value, but compiler warnings still share the stream,
    # so the executable is picked out rather than taken as the last line.
    $runCommand = $output | ForEach-Object { $_.Trim() } |
        Where-Object { $_.EndsWith('.exe') -and (Test-Path -LiteralPath $_) } | Select-Object -Last 1
    if (-not $runCommand) { throw 'The build did not report an executable to run.' }
    # Started and waited for: the host is a windowed executable, and calling one
    # returns the moment it starts, so this script would otherwise exit while the
    # application it launched was still coming up.
    #
    # A windowed executable also has no console to write to, so the --check runs
    # print nothing through here. Run those against the executable this reports
    # instead; run.sh has no such split, because ELF carries no subsystem.
    $start = @{ FilePath = $runCommand; NoNewWindow = $true; Wait = $true; PassThru = $true }
    if ($args.Count -gt 0) { $start.ArgumentList = $args }
    exit (Start-Process @start).ExitCode
}

& $dotnetCmd run --configuration $configuration --project $project -- @args
exit $LASTEXITCODE
