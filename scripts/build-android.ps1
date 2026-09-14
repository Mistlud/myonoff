[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$androidRoot = Join-Path $repoRoot 'android'
$gradleWrapper = Join-Path $androidRoot 'gradlew.bat'
$appBuildFile = Join-Path $androidRoot 'app\build.gradle.kts'
$artifactsRoot = Join-Path $repoRoot 'artifacts\android'

if (-not (Test-Path -LiteralPath $gradleWrapper -PathType Leaf)) {
    throw "Committed Gradle Wrapper was not found: $gradleWrapper"
}

if (-not (Test-Path -LiteralPath $appBuildFile -PathType Leaf)) {
    throw "Android application build file was not found: $appBuildFile"
}

$buildText = Get-Content -LiteralPath $appBuildFile -Raw
$versionMatch = [regex]::Match($buildText, '(?m)^\s*versionName\s*=\s*"([^"]+)"')
if (-not $versionMatch.Success) {
    throw 'Unable to read versionName from android/app/build.gradle.kts.'
}

$versionName = $versionMatch.Groups[1].Value
$configurationName = $Configuration.ToLowerInvariant()
$taskName = "assemble$Configuration"
$sourceApk = Join-Path $androidRoot "app\build\outputs\apk\$configurationName\app-$configurationName.apk"
$destinationApk = Join-Path $artifactsRoot "MyOnOff-$versionName-$configurationName.apk"

if ($Configuration -eq 'Release' -and
    -not (Test-Path -LiteralPath (Join-Path $androidRoot 'signing.properties') -PathType Leaf)) {
    throw 'Release signing is not configured. Copy android/signing.properties.example to android/signing.properties and fill the local values.'
}

Write-Host "Building MyOnOff Android $Configuration with the committed Gradle Wrapper..."
Push-Location $androidRoot
try {
    & $gradleWrapper $taskName
    if ($LASTEXITCODE -ne 0) {
        throw "Android $Configuration build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $sourceApk -PathType Leaf)) {
    throw "Gradle completed without the expected APK: $sourceApk"
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
Copy-Item -LiteralPath $sourceApk -Destination $destinationApk -Force

Write-Host ''
Write-Host 'Android APK build succeeded.' -ForegroundColor Green
Write-Host "  Build type : $Configuration"
Write-Host "  Version    : $versionName"
Write-Host "  APK        : $destinationApk"
