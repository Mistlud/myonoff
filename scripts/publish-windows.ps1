[CmdletBinding()]
param(
    [ValidateSet('All', 'HostAgent', 'DesktopFrameworkDependent', 'DesktopSelfContained')]
    [string]$Target = 'All',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot 'artifacts'

function Invoke-MyOnOffPublish(
    [string]$Name,
    [string]$Project,
    [string]$OutputDirectory,
    [bool]$SelfContained
) {
    $projectPath = Join-Path $repoRoot $Project
    $outputPath = Join-Path $artifactsRoot $OutputDirectory
    $arguments = @(
        'publish',
        $projectPath,
        '-c', $Configuration,
        '-r', 'win-x64',
        '--self-contained', $SelfContained.ToString().ToLowerInvariant(),
        '-o', $outputPath
    )
    if ($NoRestore) {
        $arguments += '--no-restore'
    }

    Write-Host "Publishing $Name to $outputPath"
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Name publish failed with exit code $LASTEXITCODE."
    }

    $executableName = if ($Name -eq 'Host Agent') {
        'MyOnOff.HostAgent.exe'
    }
    else {
        'MyOnOff.DesktopController.exe'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $outputPath $executableName) -PathType Leaf)) {
        throw "$Name publish completed without the expected executable: $executableName"
    }
}

if ($Target -in @('All', 'HostAgent')) {
    Invoke-MyOnOffPublish `
        -Name 'Host Agent' `
        -Project 'src\MyOnOff.HostAgent\MyOnOff.HostAgent.csproj' `
        -OutputDirectory 'host-agent-selfcontained' `
        -SelfContained $true
}

if ($Target -in @('All', 'DesktopFrameworkDependent')) {
    Invoke-MyOnOffPublish `
        -Name 'Desktop Controller' `
        -Project 'src\MyOnOff.DesktopController\MyOnOff.DesktopController.csproj' `
        -OutputDirectory 'desktop-controller' `
        -SelfContained $false
}

if ($Target -in @('All', 'DesktopSelfContained')) {
    Invoke-MyOnOffPublish `
        -Name 'Desktop Controller self-contained' `
        -Project 'src\MyOnOff.DesktopController\MyOnOff.DesktopController.csproj' `
        -OutputDirectory 'desktop-controller-selfcontained' `
        -SelfContained $true
}

Write-Host "Windows publish completed for target: $Target"
