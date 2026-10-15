$ErrorActionPreference = 'Stop'
try {
    if (Get-Process ArcanumLauncher -ErrorAction SilentlyContinue) { throw 'Fermez Arcanum Launcher avant de le remplacer.' }
    $target = Join-Path $env:LOCALAPPDATA 'Arcanum\Launcher'
    $source = Join-Path $PSScriptRoot 'Launcher'
    if (-not (Test-Path (Join-Path $source 'ArcanumLauncher.exe'))) { throw 'Décompressez toute la distribution avant de lancer ce script.' }
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    # Remplacement manuel du launcher uniquement. Game et settings.json ne sont pas distribués.
    Copy-Item -Path "$source\*" -Destination $target -Recurse -Force
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Arcanum.lnk'))
    $shortcut.TargetPath = Join-Path $target 'ArcanumLauncher.exe'
    $shortcut.WorkingDirectory = $target
    $shortcut.Save()
    Start-Process $shortcut.TargetPath
} catch { Write-Host $_.Exception.Message -ForegroundColor Red; Read-Host 'Appuyez sur Entrée'; exit 1 }
