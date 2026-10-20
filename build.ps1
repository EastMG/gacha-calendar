# Build / publish helper for GachaCalendar.
# Usage:
#   powershell -NoProfile -File build.ps1            # build only
#   powershell -NoProfile -File build.ps1 -Publish   # framework-dependent single-file publish -> publish\
param(
	[switch]$Publish
)

$ErrorActionPreference = "Stop"
$repo = $PSScriptRoot
$proj = Join-Path $repo "src\GachaCalendar\GachaCalendar.csproj"

Write-Host "==> dotnet build (Release)"
dotnet build $proj -c Release
if ($LASTEXITCODE -ne 0) { throw "build failed" }

if ($Publish) {
	$out = Join-Path $repo "publish"
	Write-Host "==> dotnet publish -> $out"
	dotnet publish $proj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $out
	if ($LASTEXITCODE -ne 0) { throw "publish failed" }

	# Ship the sample snapshot next to the exe (the app also has it embedded as a fallback).
	$dataOut = Join-Path $out "data"
	New-Item -ItemType Directory -Path $dataOut -Force | Out-Null
	Copy-Item (Join-Path $repo "data\sample-snapshot.json") (Join-Path $dataOut "sample-snapshot.json") -Force
	Write-Host "==> done: $out\GachaCalendar.exe"
}
