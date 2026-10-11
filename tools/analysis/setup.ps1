[CmdletBinding()]
param([switch]$Engine, [switch]$ConfigureCodex)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$versions = Get-Content (Join-Path $PSScriptRoot 'versions.json') -Raw | ConvertFrom-Json
Push-Location $root
try {
    New-Item -ItemType Directory -Force '.tools/codegraph', '.analysis' | Out-Null
    Copy-Item 'tools/codegraph/package*.json' '.tools/codegraph/'
    & npm ci --prefix .tools/codegraph --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE) { throw 'CodeGraph installation failed' }
    if (-not (Test-Path '.tools/python/Scripts/python.exe')) {
        & python -m venv .tools/python
        if ($LASTEXITCODE) { throw 'Python 3.10+ is required' }
    }
    & ./.tools/python/Scripts/python.exe -m pip install -r tools/analysis/requirements-lock.txt --disable-pip-version-check
    if ($LASTEXITCODE) { throw 'Python dependencies installation failed' }
    $package = "impact-engine @ git+$($versions.codeslicerRepository)@$($versions.codeslicerCommit)"
    & ./.tools/python/Scripts/python.exe -m pip install --no-deps $package --disable-pip-version-check
    if ($LASTEXITCODE) { throw 'CodeSlicer installation failed' }
    if ($Engine) {
        if (-not (Test-Path '.tools/dotnet/dotnet.exe')) {
            Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile '.tools/dotnet-install.ps1'
            & ./.tools/dotnet-install.ps1 -Version $versions.dotnetSdk -InstallDir (Join-Path $root '.tools/dotnet') -NoPath
            if ($LASTEXITCODE) { throw '.NET installation failed' }
        }
        $env:DOTNET_ROOT = Join-Path $root '.tools/dotnet'
        if (-not (Test-Path '.tools/ilspy/ilspycmd.exe')) {
            & ./.tools/dotnet/dotnet.exe tool install ilspycmd --version $versions.ilspycmd --tool-path .tools/ilspy
            if ($LASTEXITCODE) { throw 'ILSpy installation failed' }
        }
    }
    if ($ConfigureCodex) {
        New-Item -ItemType Directory -Force '.codex' | Out-Null
        $config = Join-Path $root '.codex/config.toml'
        if (Test-Path $config) {
            Write-Output 'Existing .codex/config.toml preserved. Add the servers from docs/wiki/Analysis-tools.md manually if needed.'
        } else {
        $pwsh = (Get-Command pwsh).Source.Replace('\', '/')
        $path = $root.Replace('\', '/')
        $launcher = "$path/tools/analysis/mcp.ps1"
        @"
[mcp_servers.deep_rim_codegraph]
command = "$pwsh"
args = ["-NoProfile", "-File", "$launcher", "-Tool", "codegraph"]
cwd = "$path"
startup_timeout_sec = 60

[mcp_servers.deep_rim_codeslicer]
command = "$pwsh"
args = ["-NoProfile", "-File", "$launcher", "-Tool", "codeslicer"]
cwd = "$path"
startup_timeout_sec = 60
"@ | Set-Content -Encoding utf8 $config
        }
    }
    & ./tools/analysis/run.ps1 -Tool codegraph init . --yes
    if ($LASTEXITCODE) { throw 'CodeGraph indexing failed' }
    & ./tools/analysis/run.ps1 -Tool codegraph sync .
    if ($LASTEXITCODE) { throw 'CodeGraph sync failed' }
    & ./tools/analysis/run.ps1 -Tool codeslicer --json analyze . --scope Source --no-daemon --no-research-requests --out .analysis/codeslicer-graph.json > .analysis/codeslicer-result.json
    if ($LASTEXITCODE) { throw 'CodeSlicer indexing failed' }
    Write-Output 'Ready. CodeSlicer graph and diagnostics: .analysis/'
} finally { Pop-Location }
