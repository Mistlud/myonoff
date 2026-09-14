[CmdletBinding()]
param(
    [string]$SourcePath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Join-Path $repoRoot 'mds\assets\myonoff-icon-source.png'
}
elseif (-not [System.IO.Path]::IsPathRooted($SourcePath)) {
    $SourcePath = Join-Path $repoRoot $SourcePath
}

$SourcePath = [System.IO.Path]::GetFullPath($SourcePath)
if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
    throw "Approved icon source was not found: $SourcePath"
}

Add-Type -AssemblyName System.Drawing

function New-ResizedPngBytes {
    param(
        [System.Drawing.Image]$SourceImage,
        [int]$Size
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($SourceImage, 0, 0, $Size, $Size)
        }
        finally {
            $graphics.Dispose()
        }

        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return ,$stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

function Write-MultiResolutionIcon {
    param(
        [System.Drawing.Image]$SourceImage,
        [int[]]$Sizes,
        [string]$OutputPath
    )

    $images = @($Sizes | ForEach-Object { New-ResizedPngBytes -SourceImage $SourceImage -Size $_ })
    $outputDirectory = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

    $stream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$Sizes.Count)

        $offset = 6 + (16 * $Sizes.Count)
        for ($index = 0; $index -lt $Sizes.Count; $index++) {
            $size = $Sizes[$index]
            $imageBytes = $images[$index]
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$imageBytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $imageBytes.Length
        }

        foreach ($imageBytes in $images) {
            $writer.Write($imageBytes)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$sourceImage = [System.Drawing.Image]::FromFile($SourcePath)
try {
    if ($sourceImage.Width -ne $sourceImage.Height) {
        throw "Approved icon source must be square; found $($sourceImage.Width)x$($sourceImage.Height)."
    }

    $windowsIcon = Join-Path $repoRoot 'src\MyOnOff.DesktopController\Assets\MyOnOff.ico'
    Write-MultiResolutionIcon `
        -SourceImage $sourceImage `
        -Sizes @(16, 24, 32, 48, 64, 128, 256) `
        -OutputPath $windowsIcon

    $androidSizes = [ordered]@{
        'mipmap-mdpi' = 48
        'mipmap-hdpi' = 72
        'mipmap-xhdpi' = 96
        'mipmap-xxhdpi' = 144
        'mipmap-xxxhdpi' = 192
    }

    foreach ($entry in $androidSizes.GetEnumerator()) {
        $resourceDirectory = Join-Path $repoRoot "android\app\src\main\res\$($entry.Key)"
        New-Item -ItemType Directory -Path $resourceDirectory -Force | Out-Null
        $pngBytes = New-ResizedPngBytes -SourceImage $sourceImage -Size $entry.Value
        [System.IO.File]::WriteAllBytes((Join-Path $resourceDirectory 'ic_launcher.png'), $pngBytes)
    }

    Write-Host 'Icon assets generated from the approved source.' -ForegroundColor Green
    Write-Host "  Source  : $SourcePath"
    Write-Host "  Windows : $windowsIcon"
    Write-Host '  Android : android/app/src/main/res/mipmap-*/ic_launcher.png'
}
finally {
    $sourceImage.Dispose()
}
