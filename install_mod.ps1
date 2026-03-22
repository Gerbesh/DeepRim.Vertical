$ErrorActionPreference = "Stop"

$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameMods = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\DeepRimVertical"
$userMods = Join-Path $env:USERPROFILE "AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Mods\DeepRimVertical"

function Install-To($target) {
    if (Test-Path $target) {
        Remove-Item $target -Recurse -Force
    }

    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item (Join-Path $sourceRoot "About") $target -Recurse
    Copy-Item (Join-Path $sourceRoot "1.6") $target -Recurse
    Copy-Item (Join-Path $sourceRoot "Languages") $target -Recurse
    Copy-Item (Join-Path $sourceRoot "README.md") $target
    Copy-Item (Join-Path $sourceRoot "CHANGELOG.md") $target
    Copy-Item (Join-Path $sourceRoot "RELEASE_NOTES.md") $target
}

try {
    Install-To $gameMods
    Write-Host "Installed to game Mods folder: $gameMods"
}
catch {
    Write-Warning "Game Mods install failed: $($_.Exception.Message)"
    Install-To $userMods
    Write-Host "Installed to user Mods folder: $userMods"
}
