#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExportDirectory,
    [Parameter(Mandatory)][ValidatePattern('^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')][string]$Version,
    [string]$OutputDirectory = './release',
    [string[]]$ExtraFiles = @(),
    [switch]$EmbeddedPck
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $ExportDirectory).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path $out) { throw 'Choisissez un nouveau dossier de sortie, pour ne pas écraser une release existante.' }
$selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[void]$selected.Add('Arcanum.exe')
if (-not $EmbeddedPck) { [void]$selected.Add('Arcanum.pck') }
foreach ($file in $ExtraFiles) { [void]$selected.Add($file) }
foreach ($relative in $selected) {
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[/\\])\.\.([/\\]|$)|:') { throw "Chemin invalide : $relative" }
    if ($relative -match '(?i)(^|[/\\])(server|serveur|admin|secrets?|saves?|profiles?|\.git|\.godot|user_data)([/\\]|\.)|\.(gd|tscn|cs|ps1|bat|cmd|key|pem|pfx|env|log)$|(^|[/\\])\.env|token|credential') { throw "Fichier interdit dans la distribution : $relative" }
    $full = [IO.Path]::GetFullPath((Join-Path $source $relative))
    if (-not $full.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Hors dossier : $relative" }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Fichier manquant : $relative" }
    $item = Get-Item -LiteralPath $full
    while ($item -and $item.FullName -ne $source) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Lien interdit : $relative" }
        $item = $item.Parent ?? $item.Directory
    }
}
$stage = Join-Path ([IO.Path]::GetTempPath()) ('arcanum-release-' + [guid]::NewGuid())
try {
    New-Item -ItemType Directory -Path $stage, $out | Out-Null
    foreach ($relative in $selected) {
        $target = Join-Path $stage $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $target
    }
    $zip = Join-Path $out 'Arcanum-Windows.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip)
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($zip + '.sha256', "$hash  Arcanum-Windows.zip`n", [Text.UTF8Encoding]::new($false))
    $tag = 'v' + $Version.TrimStart('v')
    @"
# Arcanum $tag

## Nouveautés
- Décrire ici les changements destinés aux joueurs.

## Installation
Utilisez Arcanum Launcher. Les sauvegardes existantes sont conservées.

Fichiers à joindre : Arcanum-Windows.zip et Arcanum-Windows.zip.sha256.
"@ | Set-Content (Join-Path $out 'NOTES-RELEASE.md') -Encoding utf8
    Write-Host "Release $tag prête : $out"
    Write-Host 'Vérifiez le contenu du PCK : aucun outil serveur, secret ni donnée personnelle ne doit y être exporté.'
} finally { if (Test-Path $stage) { Remove-Item -LiteralPath $stage -Recurse -Force } }
