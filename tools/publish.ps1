param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repo "dist"
$out = Join-Path $dist "gacha-calendar-$Runtime"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out -Force | Out-Null

$publishArgs = @(
    "publish",
    (Join-Path $repo "src\GachaCalendar\GachaCalendar.csproj"),
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $out
)
if ($SelfContained) { $publishArgs += @("--self-contained", "true") }
else { $publishArgs += @("--self-contained", "false") }

Write-Host "dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

# Engine: host + core + bundled Node runtime
$engine = Join-Path $out "engine"
New-Item -ItemType Directory -Path $engine -Force | Out-Null
Copy-Item (Join-Path $repo "packages\engine\*.mjs") $engine -Force

$coreSrc = Join-Path $repo "node_modules\gacha-calendar-core"
if (-not (Test-Path $coreSrc)) { throw "gacha-calendar-core not installed; run npm install first" }
$coreDst = Join-Path $engine "node_modules\gacha-calendar-core"
New-Item -ItemType Directory -Path (Split-Path $coreDst -Parent) -Force | Out-Null
Copy-Item $coreSrc $coreDst -Recurse -Force

$node = (Get-Command node -ErrorAction Stop).Source
Copy-Item $node (Join-Path $engine "node.exe") -Force

# Sample data / default config
$dataOut = Join-Path $out "data"
New-Item -ItemType Directory -Path $dataOut -Force | Out-Null
Copy-Item (Join-Path $repo "data\sample-snapshot.json") $dataOut -Force
Copy-Item (Join-Path $repo "data\config.example.json") $dataOut -Force

$zip = Join-Path $dist "gacha-calendar-$Runtime.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($out, $zip)

Write-Host "OUT=$out"
Write-Host "ZIP=$zip"
Write-Host "NODE=$node"
