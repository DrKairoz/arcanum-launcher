#requires -Version 7.0
param([string]$Owner = '', [string]$Repository = '')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot)
& ./tests/test-publication.ps1
dotnet run --project tests/Arcanum.Tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests en échec.' }
dotnet publish src/Arcanum.Launcher -c Release -r win-x64 --self-contained true -o dist/Launcher
if ($LASTEXITCODE -ne 0) { throw 'Compilation en échec.' }
@{ Owner = $Owner; Repository = $Repository; AutoCheck = $true; Channel = 'stable'; Language = 'fr' } | ConvertTo-Json | Set-Content dist/Launcher/Configuration/defaults.json -Encoding utf8NoBOM
Copy-Item scripts/installer-launcher.ps1 dist/
Copy-Item scripts/Installer-Arcanum.cmd dist/
Copy-Item docs/INSTALLATION-TESTEURS.md dist/LISEZ-MOI.md
if (Test-Path ArcanumLauncher-Windows-x64.zip) { Remove-Item ArcanumLauncher-Windows-x64.zip }
Compress-Archive -Path dist/* -DestinationPath ArcanumLauncher-Windows-x64.zip
