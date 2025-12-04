$base='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII='
$files=@(
 'ObsidianScout\Platforms\Windows\Resources\Square44x44Logo.png',
 'ObsidianScout\Platforms\Windows\Resources\Square71x71Logo.png',
 'ObsidianScout\Platforms\Windows\Resources\Square150x150Logo.png',
 'ObsidianScout\Platforms\Windows\Resources\Wide310x150Logo.png',
 'ObsidianScout\Platforms\Windows\Resources\Square310x310Logo.png',
 'ObsidianScout\Platforms\Windows\Resources\StoreLogo.png',
 'ObsidianScout\Platforms\Windows\Resources\appicon.png'
)

foreach($f in $files){
 $dir = Split-Path $f -Parent
 if(-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
 [System.IO.File]::WriteAllBytes($f,[System.Convert]::FromBase64String($base))
 Write-Host "Wrote: $f"
}
