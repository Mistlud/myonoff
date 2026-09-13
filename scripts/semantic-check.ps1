[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot '.validation'
$cscPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$dotnetPath = 'C:\Program Files\dotnet\dotnet.exe'

if (-not (Test-Path -LiteralPath $cscPath -PathType Leaf)) {
    throw 'Visual Studio 2022 Roslyn csc.exe is unavailable.'
}
if (-not (Test-Path -LiteralPath $dotnetPath -PathType Leaf)) {
    throw '.NET runtime host is unavailable.'
}

function Get-LatestRuntimeDirectory([string]$frameworkName) {
    $frameworkRoot = Join-Path 'C:\Program Files\dotnet\shared' $frameworkName
    $directory = Get-ChildItem -LiteralPath $frameworkRoot -Directory |
        Where-Object { $_.Name -like '8.*' } |
        Sort-Object { [version]$_.Name } -Descending |
        Select-Object -First 1
    if (-not $directory) {
        throw ".NET 8 runtime framework is unavailable: $frameworkName"
    }
    return $directory.FullName
}

function Get-ManagedReferences([string[]]$directories) {
    $assemblies = foreach ($directory in $directories) {
        Get-ChildItem -LiteralPath $directory -File -Filter '*.dll' | ForEach-Object {
            try {
                $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($_.FullName)
                [PSCustomObject]@{ Name = $assemblyName.Name; Path = $_.FullName }
            }
            catch {
                # Native runtime libraries are intentionally not compiler references.
            }
        }
    }
    return $assemblies | Group-Object Name | ForEach-Object { $_.Group[0].Path }
}

function Invoke-RoslynCompile(
    [string]$name,
    [string]$target,
    [string]$outputPath,
    [string[]]$sourcePaths,
    [string[]]$referencePaths
) {
    $responsePath = Join-Path $outputRoot "$name.rsp"
    $arguments = @(
        '/nostdlib+',
        '/langversion:preview',
        '/nullable:enable',
        '/warnaserror+',
        '/deterministic+',
        "/target:$target",
        "/out:$outputPath"
    )
    $arguments += $referencePaths | ForEach-Object { "/reference:`"$_`"" }
    $arguments += $sourcePaths | ForEach-Object { "`"$_`"" }
    Set-Content -LiteralPath $responsePath -Value $arguments -Encoding UTF8

    & $cscPath /noconfig "@$responsePath"
    if ($LASTEXITCODE -ne 0) {
        throw "Roslyn semantic compilation failed: $name"
    }
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$coreRuntime = Get-LatestRuntimeDirectory 'Microsoft.NETCore.App'
$desktopRuntime = Get-LatestRuntimeDirectory 'Microsoft.WindowsDesktop.App'
$coreReferences = Get-ManagedReferences @($coreRuntime)
$desktopReferences = Get-ManagedReferences @($desktopRuntime, $coreRuntime)

$protocolOutput = Join-Path $outputRoot 'MyOnOff.Protocol.dll'
$protocolSources = @(
    (Join-Path $PSScriptRoot 'validation\ProtocolGlobalUsings.cs')
) + (Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\MyOnOff.Protocol') -File -Filter '*.cs').FullName
Invoke-RoslynCompile 'protocol' 'library' $protocolOutput $protocolSources $coreReferences

$aspNetRoot = 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App'
$hostSemanticallyChecked = $false
if (Test-Path -LiteralPath $aspNetRoot -PathType Container) {
    $aspNetRuntime = Get-LatestRuntimeDirectory 'Microsoft.AspNetCore.App'
    $hostReferences = Get-ManagedReferences @($coreRuntime, $aspNetRuntime)
    $hostOutput = Join-Path $outputRoot 'MyOnOff.HostAgent.dll'
    $hostSources = @(
        (Join-Path $PSScriptRoot 'validation\HostAgentGlobalUsings.cs')
    ) + (Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\MyOnOff.HostAgent') -File -Filter '*.cs').FullName
    Invoke-RoslynCompile 'host-agent' 'exe' $hostOutput $hostSources ($hostReferences + $protocolOutput)
    $hostSemanticallyChecked = $true
}
else {
    Write-Warning 'ASP.NET Core runtime assemblies are absent; Host Agent remains syntax-checked only.'
}

$desktopOutput = Join-Path $outputRoot 'MyOnOff.DesktopController.dll'
$desktopSources = @(
    (Join-Path $PSScriptRoot 'validation\DesktopGlobalUsings.cs'),
    (Join-Path $PSScriptRoot 'validation\WpfGeneratedStubs.cs')
) + (Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\MyOnOff.DesktopController') -File -Filter '*.cs').FullName
Invoke-RoslynCompile 'desktop-controller' 'library' $desktopOutput $desktopSources ($desktopReferences + $protocolOutput)

$selfTestOutput = Join-Path $outputRoot 'ProtocolSelfTest.dll'
$selfTestSources = @(
    (Join-Path $PSScriptRoot 'validation\ProtocolGlobalUsings.cs'),
    (Join-Path $PSScriptRoot 'validation\ProtocolSelfTest.cs')
)
Invoke-RoslynCompile 'protocol-self-test' 'exe' $selfTestOutput $selfTestSources ($coreReferences + $protocolOutput)

$runtimeConfig = @{
    runtimeOptions = @{
        tfm = 'net8.0'
        framework = @{
            name = 'Microsoft.NETCore.App'
            version = (Split-Path -Leaf $coreRuntime)
        }
    }
} | ConvertTo-Json -Depth 4
Set-Content -LiteralPath (Join-Path $outputRoot 'ProtocolSelfTest.runtimeconfig.json') -Value $runtimeConfig -Encoding UTF8

& $dotnetPath $selfTestOutput
if ($LASTEXITCODE -ne 0) {
    throw 'Protocol self-test execution failed.'
}

if ($hostSemanticallyChecked) {
    Write-Host 'Semantic checks passed: Protocol, Host Agent and WPF C# compilation plus protocol execution.'
}
else {
    Write-Host 'Semantic checks passed: Protocol and WPF C# compilation plus protocol execution. Host Agent skipped.'
}
