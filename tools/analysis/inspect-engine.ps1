param([string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$managed = Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
$assembly = Join-Path $managed 'Assembly-CSharp.dll'
if (-not (Test-Path $assembly)) { throw "Game assembly not found: $assembly" }
Push-Location $root
try {
    $env:DOTNET_ROOT = Join-Path $root '.tools/dotnet'
    $output = Join-Path $root '.analysis/engine'
    New-Item -ItemType Directory -Force $output | Out-Null
    $targets = Get-Content (Join-Path $PSScriptRoot 'engine-types.txt') | Where-Object { $_ -and -not $_.StartsWith('#') }
    foreach ($type in $targets) {
        $code = & ./.tools/ilspy/ilspycmd.exe --disable-updatecheck -t $type -r $managed $assembly
        if ($LASTEXITCODE) { throw "Decompilation failed for $type" }
        $code | Set-Content -Encoding utf8 (Join-Path $output "$type.cs")
    }
    [ordered]@{
        gameVersion = (Get-Content (Join-Path $RimWorldDir 'Version.txt') -Raw).Trim()
        assemblySha256 = (Get-FileHash $assembly -Algorithm SHA256).Hash
        ilspyVersion = '9.1.0.7988'
        types = @($targets)
        inspectedAtUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $output 'manifest.json')
    Write-Output "Inspected $($targets.Count) types. Private output: .analysis/engine/"
} finally { Pop-Location }
