#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('arcanum-ps-tests-' + [guid]::NewGuid())
$repo = Split-Path $PSScriptRoot
$script = Join-Path $repo 'scripts/preparer-release.ps1'
try {
    $export = Join-Path $root 'export'
    New-Item -ItemType Directory -Path $export | Out-Null
    Set-Content (Join-Path $export 'Arcanum.exe') 'test only'
    Set-Content (Join-Path $export 'Arcanum.pck') 'test only'
    Set-Content (Join-Path $export 'secret.key') 'must not be distributed'
    Set-Content (Join-Path $export 'server.bat') 'must not be distributed'
    $out = Join-Path $root 'release1'
    & $script -ExportDirectory $export -Version v0.1.0 -OutputDirectory $out
    $zip = Join-Path $out 'Arcanum-Windows.zip'
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $names = @($archive.Entries | ForEach-Object FullName)
        if ($names.Count -ne 2 -or 'Arcanum.exe' -notin $names -or 'Arcanum.pck' -notin $names) { throw 'ZIP incorrect' }
    } finally { $archive.Dispose() }
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ((Get-Content ($zip + '.sha256') -Raw).Trim() -ne "$hash  Arcanum-Windows.zip") { throw 'SHA incorrect' }
    Write-Host 'PASS ZIP / exclusions par défaut / SHA-256'
    $failed = $false
    try { & $script -ExportDirectory $export -Version v0.1.0 -OutputDirectory (Join-Path $root 'forbidden') -ExtraFiles @('secret.key') } catch { $failed = $true }
    if (-not $failed) { throw 'Secret accepté' }
    Write-Host 'PASS rejet fichier secret explicitement sélectionné'
    Remove-Item (Join-Path $export 'Arcanum.pck')
    $failed = $false
    try { & $script -ExportDirectory $export -Version v0.1.1 -OutputDirectory (Join-Path $root 'missing') } catch { $failed = $true }
    if (-not $failed) { throw 'PCK manquant accepté' }
    Write-Host 'PASS rejet PCK manquant'
    & $script -ExportDirectory $export -Version v0.1.1 -OutputDirectory (Join-Path $root 'embedded') -EmbeddedPck
    Write-Host 'PASS option PCK intégré'
    foreach ($file in Get-ChildItem $repo -Recurse -Filter '*.ps1') {
        $tokens = $null; $errors = $null
        [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
        if ($errors.Count) { throw ($errors | Out-String) }
    }
    Write-Host 'PASS analyse syntaxique PowerShell'
} finally { if (Test-Path $root) { Remove-Item $root -Recurse -Force } }
