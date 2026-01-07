 # Download the latest Plotly JS to Resources/Raw for offline use
 $outDir = Join-Path -Path $PSScriptRoot -ChildPath "Resources\Raw"
 If (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
 $outFile = Join-Path -Path $outDir -ChildPath "plotly-latest.min.js"
 $url = 'https://cdn.plot.ly/plotly-latest.min.js'

 Write-Host "Downloading Plotly from $url to $outFile ..."
 try {
     Invoke-WebRequest -Uri $url -OutFile $outFile -UseBasicParsing -ErrorAction Stop
     Write-Host "Downloaded Plotly to $outFile"
 } catch {
     Write-Error "Failed to download Plotly: $_"
     exit 1
 }

 Write-Host "Done. The file will be included in the MAUI app as a MauiAsset (Resources/Raw)."

