param([Parameter(Mandatory)][ValidateSet('codegraph', 'codeslicer')][string]$Tool)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location $root
$env:CODEGRAPH_TELEMETRY = '0'
$env:CODEGRAPH_NO_DOWNLOAD = '1'
$env:PYTHONUTF8 = '1'
if ($Tool -eq 'codegraph') {
    & "$root/.tools/codegraph/node_modules/.bin/codegraph.cmd" serve --mcp --path $root
} else {
    & "$root/.tools/python/Scripts/impact-engine-mcp.exe"
}
exit $LASTEXITCODE
