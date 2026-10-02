# Rebuilds src/AltMiUstMu.Web/wwwroot/css/app.css on every change. Run `dotnet build` once first (it downloads the CLI).
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..\src\AltMiUstMu.Web')
$bin = Get-ChildItem -Path '..\..\.tools' -Recurse -Filter 'tailwindcss-*' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $bin) { Write-Error "Tailwind CLI not found; run 'dotnet build' first." }
& $bin.FullName -i Styles/app.css -o wwwroot/css/app.css --watch
