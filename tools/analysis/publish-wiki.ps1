param([switch]$Push)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$wiki = Join-Path $root '.tools/wiki'
$source = Join-Path $root 'docs/wiki'
if (-not (Test-Path (Join-Path $wiki '.git'))) {
    & git clone 'https://github.com/Gerbesh/DeepRim.Vertical.wiki.git' $wiki
    if ($LASTEXITCODE) { throw 'Enable Wiki and create its first page on GitHub before publishing.' }
}
$remote = & git -C $wiki remote get-url origin
if ($LASTEXITCODE -or $remote -ne 'https://github.com/Gerbesh/DeepRim.Vertical.wiki.git') { throw 'Unexpected wiki remote' }
$existing = @(& git -C $wiki status --porcelain)
if ($existing.Count) {
    foreach ($entry in $existing) {
        $name = $entry.Substring(3)
        $canonical = Join-Path $source $name
        if (-not (Test-Path -LiteralPath $canonical -PathType Leaf)) { throw "Pending unrelated wiki change preserved: $name" }
        $expected = [regex]::Replace([IO.File]::ReadAllText($canonical), '\]\(([A-Za-z0-9_-]+)\.md(#[^)]+)?\)', ']($1$2)')
        if ([IO.File]::ReadAllText((Join-Path $wiki $name)) -ne $expected) { throw "Pending manual wiki edit preserved: $name" }
    }
} else {
    & git -C $wiki pull --ff-only
    if ($LASTEXITCODE) { throw 'Wiki fetch failed; no files were overwritten' }
}
foreach ($file in Get-ChildItem -LiteralPath $source -Filter '*.md') {
    $body = [IO.File]::ReadAllText($file.FullName)
    # Local Markdown uses filenames; GitHub Wiki navigation uses page slugs.
    $body = [regex]::Replace($body, '\]\(([A-Za-z0-9_-]+)\.md(#[^)]+)?\)', ']($1$2)')
    [IO.File]::WriteAllText((Join-Path $wiki $file.Name), $body, [Text.UTF8Encoding]::new($false))
}
& git -C $wiki diff --stat
& git -C $wiki status --short
if ($Push) {
    $names = @(Get-ChildItem -LiteralPath $source -Filter '*.md' | ForEach-Object Name)
    & git -C $wiki add -- @names
    if ($LASTEXITCODE) { throw 'Wiki staging failed' }
    & git -C $wiki diff --cached --quiet
    if ($LASTEXITCODE -eq 1) {
        & git -C $wiki commit -m 'Document RimWorld engine contracts, rendering audit and analysis workflow'
        if ($LASTEXITCODE) { throw 'Wiki commit failed' }
    } elseif ($LASTEXITCODE -ne 0) { throw 'Wiki diff failed' }
    & git -C $wiki push origin HEAD
    if ($LASTEXITCODE) { throw 'Wiki push failed' }
} else {
    Write-Output 'Prepared wiki changes. Review .tools/wiki, then run this script with -Push.'
}
