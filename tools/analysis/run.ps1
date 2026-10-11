param([ValidateSet('codegraph', 'codeslicer')][string]$Tool)
$ToolArguments = $args
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $root
try {
    $env:CODEGRAPH_TELEMETRY = '0'
    $env:PYTHONUTF8 = '1'
    $exe = if ($Tool -eq 'codegraph') { '.tools/codegraph/node_modules/.bin/codegraph.cmd' } else { '.tools/python/Scripts/codeslicer.exe' }
    if (-not (Test-Path $exe)) { throw 'Run tools/analysis/setup.ps1 first' }
    & $exe @ToolArguments
    $result = $LASTEXITCODE
} finally { Pop-Location }
exit $result
