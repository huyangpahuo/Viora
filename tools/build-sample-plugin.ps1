# Builds the sample plugin into a ready-to-install package (.vplugin = zip with plugin.json at root).
# Usage: powershell -File tools\build-sample-plugin.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

dotnet build "$root\samples\SamplePlugin\SamplePlugin.csproj" -c Release -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$stage = Join-Path $env:TEMP "viora-sample-plugin"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item "$root\samples\SamplePlugin\plugin.json" $stage
Copy-Item "$root\samples\SamplePlugin\bin\Release\net8.0\SamplePlugin.dll" $stage

$package = "$root\samples\SamplePlugin\DuotoneSample.vplugin"
$zipTmp = "$stage\..\DuotoneSample.zip"
if (Test-Path $zipTmp) { Remove-Item $zipTmp -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zipTmp
if (Test-Path $package) { Remove-Item $package -Force }
Move-Item $zipTmp $package
Write-Host "Package ready: $package"
