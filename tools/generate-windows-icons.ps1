<#
PowerShell script to generate Windows PNG tile assets from the MAUI SVG splash.
Usage (PowerShell):
 .\tools\generate-windows-icons.ps1

Requires either ImageMagick (`magick`) or Inkscape (`inkscape`) on PATH.
#>

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path "$scriptDir\..")
$sourceSvg = Join-Path $repoRoot 'ObsidianScout\Resources\Splash\splash.svg'
$targetDir = Join-Path $repoRoot 'ObsidianScout\Platforms\Windows\Resources'

if (-not (Test-Path $sourceSvg)) {
 Write-Error "Source SVG not found: $sourceSvg"
 exit 1
}

if (-not (Test-Path $targetDir)) {
 New-Item -ItemType Directory -Path $targetDir | Out-Null
}

$sizes = @(
 @{ Name = 'Square44x44Logo.png'; Width =44; Height =44 },
 @{ Name = 'Square71x71Logo.png'; Width =71; Height =71 },
 @{ Name = 'Square150x150Logo.png'; Width =150; Height =150 },
 @{ Name = 'Wide310x150Logo.png'; Width =310; Height =150 },
 @{ Name = 'Square310x310Logo.png'; Width =310; Height =310 },
 @{ Name = 'StoreLogo.png'; Width =50; Height =50 }
)

function Use-ImageMagick {
 param($in, $out, $w, $h)
 # Preserve aspect; pad transparent background to exact size
 magick convert `"$in`" -background none -resize ${w}x${h}^ -gravity center -extent ${w}x${h} `"$out`"
}

function Use-Inkscape {
 param($in, $out, $w, $h)
 # Inkscape CLI differs by version; use export-area-drawing with width/height
 inkscape `"$in`" --export-type=png --export-filename=`"$out`" --export-width=$w --export-height=$h
}

$hasMagick = Get-Command magick -ErrorAction SilentlyContinue
$hasInkscape = Get-Command inkscape -ErrorAction SilentlyContinue

if (-not $hasMagick -and -not $hasInkscape) {
 Write-Error "Neither ImageMagick (magick) nor Inkscape (inkscape) was found on PATH. Install one tool and re-run."
 exit 1
}

foreach ($s in $sizes) {
 $outPath = Join-Path $targetDir $s.Name
 Write-Host "Generating $($s.Name) => ${s.Width}x${s.Height}"
 try {
 if ($hasMagick) {
 Use-ImageMagick $sourceSvg $outPath $s.Width $s.Height
 }
 else {
 Use-Inkscape $sourceSvg $outPath $s.Width $s.Height
 }
 Write-Host "Written: $outPath"
 }
 catch {
 Write-Warning "Failed to generate $($s.Name): $_"
 }
}

Write-Host "All done. Manifest already references these files: Platforms/Windows/Package.appxmanifest"
Write-Host "Run a build (dotnet build) or open the solution in Visual Studio to verify packaging."