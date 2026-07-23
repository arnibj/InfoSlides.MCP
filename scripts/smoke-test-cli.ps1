#requires -Version 5.1
<#
.SYNOPSIS
    Smoke test for the `infoslides` CLI (and, optionally, the MCP stdio server).

.DESCRIPTION
    You run the CLI; agents run the MCP server. This script exercises the CLI the way a
    human would, in three stages:

      A. Offline checks (always)  - version, help, config, and `mcp install` to a temp file.
                                    No network, no account, safe to run repeatedly.
      B. Live checks (-Live)      - hits the real API. Creates a REAL tenant and sends a
                                    verification email to -OwnerEmail. Opt-in only.
      C. MCP handshake (-Mcp)     - pipes a JSON-RPC initialize + tools/list at `infoslides --mcp`
                                    and confirms the server answers with its tool list.

    The CLI is located automatically: pass -Cli to point at a published binary, otherwise the
    script builds the project (Release) and runs it via `dotnet <infoslides.dll>`.

.EXAMPLE
    # Safe, no network - run this first:
    ./scripts/smoke-test-cli.ps1

.EXAMPLE
    # Full end-to-end against the default (production) API - creates a real tenant:
    ./scripts/smoke-test-cli.ps1 -Live -OwnerEmail you+cli@yourdomain.com

.EXAMPLE
    # Point at a local/staging API and test the MCP handshake too:
    ./scripts/smoke-test-cli.ps1 -Live -Mcp -ApiUrl https://localhost:5001

.EXAMPLE
    # Test an already-published AOT binary instead of building:
    ./scripts/smoke-test-cli.ps1 -Cli ./publish/infoslides.exe -Live
#>
[CmdletBinding()]
param(
    [switch]$Live,
    [switch]$Mcp,
    [string]$ApiUrl,
    [string]$Cli,
    [string]$OwnerEmail = "cli-smoke+$(Get-Date -Format yyyyMMddHHmmss)@example.com",
    [string]$TenantName = "CLI Smoke Test",
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot   = Split-Path -Parent $PSScriptRoot
$cliProject = Join-Path $repoRoot 'src/InfoSlides.Cli/InfoSlides.Cli.csproj'

$script:pass = 0
$script:fail = 0

function Write-Head($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }

# Resolve how to invoke the CLI.
# Sets $script:CliFile + $script:CliPrefixArgs so a call is: & $CliFile @CliPrefixArgs @userArgs
function Resolve-Cli {
    if ($Cli) {
        if (-not (Test-Path $Cli)) { throw "Cli not found: $Cli" }
        $script:CliFile = (Resolve-Path $Cli).Path
        $script:CliPrefixArgs = @()
        Write-Host "CLI: native binary $script:CliFile" -ForegroundColor DarkGray
        return
    }

    if (-not $NoBuild) {
        Write-Host "Building InfoSlides.Cli (Release)..." -ForegroundColor DarkGray
        dotnet build $cliProject -c Release --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }
    }

    $dll = Get-ChildItem -Path (Join-Path $repoRoot 'src/InfoSlides.Cli/bin/Release') `
        -Filter 'infoslides.dll' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $dll) { throw "infoslides.dll not found - build first, or pass -Cli." }

    $script:CliFile = 'dotnet'
    $script:CliPrefixArgs = @($dll.FullName)
    Write-Host "CLI: dotnet $($dll.FullName)" -ForegroundColor DarkGray
}

# Runs the CLI, returns [pscustomobject]@{ Code; Text }. Adds --api-url when -ApiUrl is set.
function Invoke-Cli {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$CliArgs)
    # The CLI writes errors to stderr; under a global 'Stop' preference, 2>&1 in PS 5.1 would
    # turn those lines into terminating errors. Keep it non-terminating so we can inspect exit codes.
    $ErrorActionPreference = 'Continue'
    $full = @() + $script:CliPrefixArgs
    if ($ApiUrl) { $full += @('--api-url', $ApiUrl) }
    $full += $CliArgs
    $text = (& $script:CliFile @full 2>&1 | Out-String)
    [pscustomobject]@{ Code = $LASTEXITCODE; Text = $text }
}

# Assertion helper. -Contains checks (case-insensitive) that output includes the substring.
function Step {
    param([string]$Name, [scriptblock]$Run, [string]$Contains, [switch]$AllowNonZero)
    Write-Host "* $Name" -ForegroundColor White
    try {
        $r = & $Run
        $ok = $true
        if (-not $AllowNonZero -and $r.Code -ne 0) { $ok = $false }
        if ($Contains -and ($r.Text -notmatch [regex]::Escape($Contains))) { $ok = $false }
        if ($ok) {
            $script:pass++; Write-Host "  PASS (exit $($r.Code))" -ForegroundColor Green
        } else {
            $script:fail++; Write-Host "  FAIL (exit $($r.Code))" -ForegroundColor Red
        }
        if ($r.Text.Trim()) {
            ($r.Text.Trim() -split "`n" | Select-Object -First 8) | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        }
        $script:LastResult = $r
    } catch {
        $script:fail++; Write-Host "  FAIL (exception): $_" -ForegroundColor Red
        $script:LastResult = [pscustomobject]@{ Code = -1; Text = "$_" }
    }
}

# -----------------------------------------------------------------------------
Resolve-Cli

Write-Head "A. Offline checks (no network)"
Step "version"              { Invoke-Cli '--version' }
Step "root --help lists mcp + tenant" { Invoke-Cli '--help' } -Contains 'mcp'
Step "tenant --help"        { Invoke-Cli 'tenant' '--help' } -Contains 'create'
Step "stream --help"        { Invoke-Cli 'stream' '--help' } -Contains 'link'
Step "config get shows resolved apiUrl" { Invoke-Cli 'config' 'get' } -Contains 'apiUrl:'
$cfg = $script:LastResult
if ($ApiUrl -and $cfg.Text -notmatch [regex]::Escape($ApiUrl)) {
    Write-Host "  NOTE: -ApiUrl was set but config get did not echo it (flag applies per-call)." -ForegroundColor Yellow
}

$tmpMcp = Join-Path ([System.IO.Path]::GetTempPath()) ("mcp-smoke-{0}.json" -f (Get-Date -Format yyyyMMddHHmmss))
Step "mcp install --client claude-code --config-path <temp>" `
    { Invoke-Cli 'mcp' 'install' '--client' 'claude-code' '--config-path' $tmpMcp } -Contains 'infoslides'
if (Test-Path $tmpMcp) {
    Step "installed config contains --mcp launch args" `
        { [pscustomobject]@{ Code = 0; Text = (Get-Content $tmpMcp -Raw) } } -Contains '--mcp'
    Remove-Item $tmpMcp -Force -ErrorAction SilentlyContinue
}
Step "unknown client is rejected" { Invoke-Cli 'mcp' 'install' '--client' 'nope' } -Contains 'unknown client' -AllowNonZero

# -----------------------------------------------------------------------------
if ($Live) {
    $apiLabel = if ([string]::IsNullOrEmpty($ApiUrl)) { 'CLI default' } else { $ApiUrl }
    Write-Head "B. Live checks (API: $apiLabel)"
    Write-Host "  This creates a REAL tenant '$TenantName' and emails $OwnerEmail." -ForegroundColor Yellow
    Write-Host "  Most operations stay locked until that email is verified." -ForegroundColor Yellow

    Step "tenant create --save --json" `
        { Invoke-Cli 'tenant' 'create' $TenantName $OwnerEmail '--save' '--json' } -Contains 'apiKey'
    $created = $script:LastResult

    $tenantId = $null
    try {
        $json = $created.Text | ConvertFrom-Json
        $tenantId = $json.tenantId
        if ($json.apiKey) {
            $keyPreview = $json.apiKey.Substring(0, [Math]::Min(12, $json.apiKey.Length))
            Write-Host "  Saved admin key: $keyPreview..." -ForegroundColor DarkGray
        }
    } catch {
        Write-Host "  (could not parse tenant JSON - check output above)" -ForegroundColor Yellow
    }

    Step "config get now shows a credential" { Invoke-Cli 'config' 'get' } -Contains 'credential:'
    Step "tenant info (verification + quotas)" { Invoke-Cli 'tenant' 'info' '--json' }
    Step "gallery list" { Invoke-Cli 'gallery' 'list' '--json' }

    Write-Host "  The steps below often require a verified email; failures here are informational." -ForegroundColor Yellow
    Step "device create" { Invoke-Cli 'device' 'create' 'Smoke Screen' '--width' '1920' '--height' '1080' '--json' } -AllowNonZero
    Step "slideshow list" { Invoke-Cli 'slideshow' 'list' '--json' } -AllowNonZero

    if ($tenantId) {
        Write-Host "  Cleanup: tenant '$tenantId' persists on the server - delete it from the admin panel if this was production." -ForegroundColor Yellow
    }
} else {
    Write-Host "`n(Skipping live API checks - pass -Live to run them.)" -ForegroundColor DarkGray
}

# -----------------------------------------------------------------------------
if ($Mcp) {
    Write-Head "C. MCP stdio handshake"
    Write-Host "  Tip: the repo's ``dotnet test`` already runs full MCP stdio smoke tests against a fake backend." -ForegroundColor DarkGray
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        if ($script:CliFile -eq 'dotnet') {
            $psi.FileName  = 'dotnet'
            $psi.Arguments = '"' + $script:CliPrefixArgs[0] + '" --mcp'
        } else {
            $psi.FileName  = $script:CliFile
            $psi.Arguments = '--mcp'
        }
        $psi.RedirectStandardInput  = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError  = $true
        $psi.UseShellExecute        = $false
        $p = [System.Diagnostics.Process]::Start($psi)

        $p.StandardInput.WriteLine('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0"}}}')
        $p.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
        $p.StandardInput.WriteLine('{"jsonrpc":"2.0","id":2,"method":"tools/list"}')
        $p.StandardInput.Close()   # stdio transport ends on stdin EOF, so the server drains and exits

        $out = $p.StandardOutput.ReadToEnd()
        if (-not $p.WaitForExit(5000)) { $p.Kill() }

        Write-Host "* tools/list returns create_tenant" -ForegroundColor White
        if ($out -match 'create_tenant') {
            $script:pass++; Write-Host "  PASS - server responded with its tool list" -ForegroundColor Green
        } else {
            $script:fail++; Write-Host "  FAIL - no tool list in response (first 300 chars below)" -ForegroundColor Red
            Write-Host "    $($out.Substring(0, [Math]::Min(300, $out.Length)))" -ForegroundColor DarkGray
        }
    } catch {
        $script:fail++; Write-Host "  FAIL (exception): $_" -ForegroundColor Red
        Write-Host "  Fall back to: dotnet test (tests/InfoSlides.Mcp.Tests)" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n(Skipping MCP handshake - pass -Mcp to run it. Or run: dotnet test)" -ForegroundColor DarkGray
}

# -----------------------------------------------------------------------------
Write-Head "Summary"
$summaryColor = if ($script:fail) { 'Red' } else { 'Green' }
Write-Host ("PASS: {0}  FAIL: {1}" -f $script:pass, $script:fail) -ForegroundColor $summaryColor
exit $(if ($script:fail) { 1 } else { 0 })
