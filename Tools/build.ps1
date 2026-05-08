[CmdletBinding()]
param (
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")]
    [string]$Runtime = "",

    [switch]$SelfContained,
    [switch]$NoSingleFile,
    [switch]$ReadyToRun,
    [switch]$Trim,

    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrEmpty($ScriptDir)) {
    $ScriptDir = Get-Location
}
# 脚本位于 Tools/ 子目录下，项目根目录是其父目录
$RootDir = Resolve-Path (Split-Path -Parent $ScriptDir)

if ([string]::IsNullOrEmpty($OutputDir)) {
    if ($Runtime) {
        $OutputDir = Join-Path (Join-Path $RootDir "publish") $Runtime
    } else {
        $OutputDir = Join-Path $RootDir "publish"
    }
}
else {
    if (-not [System.IO.Path]::IsPathRooted($OutputDir)) {
        $OutputDir = Join-Path $RootDir $OutputDir
    }
}

$ProjectPath = Join-Path (Join-Path $RootDir "CharacterKiller.CLI") "CharacterKiller.CLI.csproj"

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

if (Test-Path $OutputDir) {
    Write-Host "Cleaning old output: $OutputDir" -ForegroundColor Yellow
    Remove-Item -Recurse -Force $OutputDir
}

$PublishArgs = @(
    "publish"
    $ProjectPath
    "-c"
    $Configuration
    "--output"
    $OutputDir
)

if ($Runtime) {
    $PublishArgs += "--runtime"
    $PublishArgs += $Runtime
}

if ($SelfContained) {
    $PublishArgs += "--self-contained"
} else {
    $PublishArgs += "--no-self-contained"
}

if (-not $NoSingleFile) {
    $PublishArgs += "/p:PublishSingleFile=true"
}

if ($ReadyToRun) {
    $PublishArgs += "/p:PublishReadyToRun=true"
}

if ($Trim) {
    $PublishArgs += "/p:PublishTrimmed=true"
}

if ($Configuration -eq "Release") {
    $PublishArgs += "/p:DebugType=None"
    $PublishArgs += "/p:DebugSymbols=false"
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building CharacterKiller.CLI" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Project  : $ProjectPath"
Write-Host "Config   : $Configuration"
Write-Host "Runtime  : $(if ($Runtime) { $Runtime } else { "Current platform (framework-dependent)" })"
Write-Host "Self-contained : $(if ($SelfContained) { "Yes" } else { "No" })"
Write-Host "Single file    : $(if (-not $NoSingleFile) { "Yes" } else { "No" })"
Write-Host "ReadyToRun     : $(if ($ReadyToRun) { "Yes" } else { "No" })"
Write-Host "Trim           : $(if ($Trim) { "Yes" } else { "No" })"
Write-Host "Output   : $OutputDir"
Write-Host "========================================" -ForegroundColor Cyan

$StartTime = Get-Date
& dotnet @PublishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}

$Elapsed = (Get-Date) - $StartTime

Write-Host ""
Write-Host "Build succeeded in $($Elapsed.TotalSeconds.ToString("F2"))s" -ForegroundColor Green
Write-Host ""
Write-Host "Artifacts:" -ForegroundColor Green

if (Test-Path $OutputDir) {
    $ExeName = "CharacterKiller.CLI"
    if ($Runtime -match "^win") {
        $ExeName += ".exe"
    }

    Get-ChildItem -Path $OutputDir | ForEach-Object {
        $size = if ($_.PSIsContainer) { "<dir>" } else { "{0:N0} KB" -f ($_.Length / 1KB) }
        Write-Host ("  {0,-30} {1,12}" -f $_.Name, $size)
    }

    $MainExe = Join-Path $OutputDir $ExeName
    if (Test-Path $MainExe) {
        Write-Host ""
        Write-Host "Executable: $MainExe" -ForegroundColor Green
    }
} else {
    Write-Warning "Output directory was not created: $OutputDir"
}
