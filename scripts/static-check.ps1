[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -LiteralPath $repoRoot -Recurse -File |
    Where-Object {
        $_.Extension -eq '.json' -and
        $_.FullName -notmatch '[\\/](\.git|bin|obj|build|\.gradle)[\\/]'
    } |
    ForEach-Object {
        $file = $_
        try {
            Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json | Out-Null
        }
        catch {
            $failures.Add("Invalid JSON: $($file.FullName): $($_.Exception.Message)")
        }
    }

Get-ChildItem -LiteralPath $repoRoot -Recurse -File |
    Where-Object {
        ($_.Extension -in @('.csproj', '.xaml', '.xml')) -and
        $_.FullName -notmatch '[\\/](\.git|bin|obj|build|\.gradle)[\\/]'
    } |
    ForEach-Object {
        $file = $_
        try {
            [xml](Get-Content -LiteralPath $file.FullName -Raw) | Out-Null
        }
        catch {
            $failures.Add("Invalid XML: $($file.FullName): $($_.Exception.Message)")
        }
    }

Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.ps1' | ForEach-Object {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    foreach ($parseError in $errors) {
        $failures.Add("Invalid PowerShell: $($_.FullName): $($parseError.Message)")
    }
}

$agentSettings = Get-Content -LiteralPath (Join-Path $repoRoot 'src\MyOnOff.HostAgent\appsettings.json') -Raw |
    ConvertFrom-Json
if (-not [string]::IsNullOrEmpty($agentSettings.Agent.AuthToken)) {
    $failures.Add('appsettings.json must not contain a committed authentication token.')
}

$requiredPaths = @(
    'Plan.md',
    'README.md',
    'MANUAL_VALIDATION.md',
    'docs\API.md',
    'src\MyOnOff.HostAgent\Program.cs',
    'src\MyOnOff.DesktopController\MainWindow.xaml',
    'android\app\src\main\java\com\mistlud\myonoff\MainActivity.kt'
)
foreach ($relativePath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath) -PathType Leaf)) {
        $failures.Add("Missing required file: $relativePath")
    }
}

$requiredSourceContracts = @(
    @{ Path = 'src\MyOnOff.HostAgent\Program.cs'; Text = 'app.MapGet(ApiRoutes.Status' },
    @{ Path = 'src\MyOnOff.HostAgent\Program.cs'; Text = 'app.MapPost(ApiRoutes.Sleep' },
    @{ Path = 'src\MyOnOff.HostAgent\Program.cs'; Text = 'app.MapPost(ApiRoutes.Shutdown' },
    @{ Path = 'src\MyOnOff.HostAgent\ControlAuthentication.cs'; Text = 'CryptographicOperations.FixedTimeEquals' },
    @{ Path = 'src\MyOnOff.Protocol\HostStateClassifier.cs'; Text = 'snapshot.AgentReachable && snapshot.SmbReachable' },
    @{ Path = 'src\MyOnOff.DesktopController\MainWindow.xaml.cs'; Text = '_operations.TryBeginAction()' },
    @{ Path = 'src\MyOnOff.DesktopController\MainWindow.xaml.cs'; Text = '_operations.ShouldApplyRefreshResult' },
    @{ Path = 'android\app\src\main\AndroidManifest.xml'; Text = 'android.permission.ACCESS_LOCAL_NETWORK' },
    @{ Path = 'android\app\src\main\java\com\mistlud\myonoff\HostRepository.kt'; Text = 'DatagramSocket()' },
    @{ Path = 'android\app\src\main\java\com\mistlud\myonoff\HostRepository.kt'; Text = 'setRequestProperty("Authorization"' }
)
foreach ($contract in $requiredSourceContracts) {
    $contractPath = Join-Path $repoRoot $contract.Path
    if (-not (Select-String -LiteralPath $contractPath -SimpleMatch $contract.Text -Quiet)) {
        $failures.Add("Missing source contract '$($contract.Text)' in $($contract.Path)")
    }
}

$solutionHeader = Get-Content -LiteralPath (Join-Path $repoRoot 'MyOnOff.sln') -First 1
if ($solutionHeader -ne 'Microsoft Visual Studio Solution File, Format Version 12.00') {
    $failures.Add('MyOnOff.sln has an invalid first-line header.')
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Static checks passed: JSON, XML/XAML, PowerShell, secret placeholder, solution header, required files and source contracts.'
