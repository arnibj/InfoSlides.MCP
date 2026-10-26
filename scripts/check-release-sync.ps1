# ── Copyright notice ──────────────────────────────────────────────────────────────────
# (c) 2026 Arni Bjorgvinsson. All rights reserved.
# ─────────────────────────────────────────────────────────────────────────────────
#requires -Version 7
<#
.SYNOPSIS
    Checks that every public MCP surface matches a release: git tag, GitHub release, MCP registry, hosted host.

.DESCRIPTION
    Prints one PASS / FAIL / SKIP line per check and exits 1 when any check fails. It changes nothing: the
    registry publish stays a manual workflow, this only says when it is overdue.

      - Git tag          the newest v* tag is v<version>.
      - GitHub release   v<version> exists and has a server.json.
      - Registry         the latest registry listing has <version> and the release's fileSha256.
      - Server card      https://infoslides.app/.well-known/mcp/server-card.json reports <version>, and its tool
                         names equal the hosted list in src/InfoSlides.Mcp.Tools/Instructions/hosted-rules.json.
      - Host initialize  the hosted /mcp initialize reports <version>. Needs INFOSLIDES_API_KEY (an API key gets
                         past the 401); skipped when it is not set.

    No credential is needed for the rest: the hosted tool list is read from the public server card.

.PARAMETER Version
    The release version without the leading v. Defaults to <Version> in src/InfoSlides.Cli/InfoSlides.Cli.csproj.

.PARAMETER HostUrl
    The hosted server's origin. Defaults to https://infoslides.app.

.EXAMPLE
    ./scripts/check-release-sync.ps1

.EXAMPLE
    $env:INFOSLIDES_API_KEY = 'isk_admin_...'; ./scripts/check-release-sync.ps1 -Version 1.9.0
#>
param(
    [string]$Version,
    [string]$HostUrl = 'https://infoslides.app'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$repo = 'arnibj/InfoSlides.MCP'
$registryName = 'io.github.arnibj/infoslides'
# The production edge refuses some default client user agents.
$headers = @{ 'User-Agent' = 'Mozilla/5.0 (InfoSlides release sync check)' }

if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $repoRoot 'src/InfoSlides.Cli/InfoSlides.Cli.csproj') -Raw)).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
}

$failures = 0
function Report([string]$Status, [string]$Check, [string]$Detail) {
    $color = @{ PASS = 'Green'; FAIL = 'Red'; SKIP = 'Yellow' }[$Status]
    Write-Host ("{0,-5} {1,-16} {2}" -f $Status, $Check, $Detail) -ForegroundColor $color
    if ($Status -eq 'FAIL') { $script:failures++ }
}

function Compare-Names([string[]]$Expected, [string[]]$Actual) {
    $missing = $Expected | Where-Object { $_ -notin $Actual }
    $extra = $Actual | Where-Object { $_ -notin $Expected }
    $parts = @()
    if ($missing) { $parts += "missing: $($missing -join ', ')" }
    if ($extra) { $parts += "extra: $($extra -join ', ')" }
    return $parts -join '; '
}

Write-Host "Release sync check for $Version"

# Git tag
try {
    $tag = git -C $repoRoot tag --list 'v*' --sort=-version:refname | Select-Object -First 1
    if ($tag -eq "v$Version") { Report PASS 'Git tag' $tag }
    else { Report FAIL 'Git tag' "newest tag is '$tag', expected v$Version" }
}
catch { Report FAIL 'Git tag' $_.Exception.Message }

# GitHub release (its server.json carries the bundle hash the registry must match)
$releaseSha = $null
try {
    $serverJson = Invoke-RestMethod -Headers $headers "https://github.com/$repo/releases/download/v$Version/server.json"
    $releaseSha = $serverJson.packages[0].fileSha256
    Report PASS 'GitHub release' "v$Version, fileSha256 $releaseSha"
}
catch { Report FAIL 'GitHub release' "no server.json on release v$Version ($($_.Exception.Message))" }

# MCP registry
try {
    $listing = Invoke-RestMethod -Headers $headers "https://registry.modelcontextprotocol.io/v0/servers?search=$registryName"
    $latest = $listing.servers |
        Where-Object { $_.server.name -eq $registryName -and $_._meta.'io.modelcontextprotocol.registry/official'.isLatest } |
        Select-Object -First 1
    if (-not $latest) { Report FAIL 'Registry' "no listing for $registryName" }
    else {
        $listed = $latest.server.version
        $listedSha = $latest.server.packages[0].fileSha256
        if ($listed -ne $Version) { Report FAIL 'Registry' "lists $listed, expected ${Version}: run the 'Publish to MCP Registry' workflow" }
        elseif ($releaseSha -and $listedSha -ne $releaseSha) { Report FAIL 'Registry' "fileSha256 $listedSha differs from the release's $releaseSha" }
        else { Report PASS 'Registry' "$listed, fileSha256 $listedSha" }
    }
}
catch { Report FAIL 'Registry' $_.Exception.Message }

# Server card against the hosted tool list in the repo
try {
    $card = Invoke-RestMethod -Headers $headers "$HostUrl/.well-known/mcp/server-card.json"
    $hosted = (Get-Content (Join-Path $repoRoot 'src/InfoSlides.Mcp.Tools/Instructions/hosted-rules.json') -Raw | ConvertFrom-Json).tools
    $diff = Compare-Names $hosted @($card.tools.name)
    if ($card.serverInfo.version -ne $Version) { Report FAIL 'Server card' "reports $($card.serverInfo.version), expected ${Version}: redeploy the host" }
    elseif ($diff) { Report FAIL 'Server card' "tools differ from hosted-rules.json ($diff)" }
    else { Report PASS 'Server card' "$Version, $(@($card.tools).Count) tools match the hosted list" }
}
catch { Report FAIL 'Server card' "$HostUrl/.well-known/mcp/server-card.json: $($_.Exception.Message)" }

# Host initialize (an API key is the only credential a script can hold; the 401 path carries no version)
if (-not $env:INFOSLIDES_API_KEY) {
    Report SKIP 'Host initialize' 'set INFOSLIDES_API_KEY to check the version the hosted /mcp reports'
}
else {
    try {
        $body = @{
            jsonrpc = '2.0'; id = 1; method = 'initialize'
            params  = @{ protocolVersion = '2025-06-18'; capabilities = @{}; clientInfo = @{ name = 'release-sync-check'; version = $Version } }
        } | ConvertTo-Json -Depth 5
        $response = Invoke-WebRequest -Method Post -Uri "$HostUrl/mcp" -Body $body -ContentType 'application/json' -Headers ($headers + @{
            Authorization = "Bearer $($env:INFOSLIDES_API_KEY)"
            Accept        = 'application/json, text/event-stream'
        })
        # The answer is either plain JSON or one server-sent event whose data line holds it.
        $text = [string]$response.Content
        $json = if ($text.TrimStart().StartsWith('{')) { $text } else { ($text -split "`n" | Where-Object { $_ -like 'data:*' } | Select-Object -First 1).Substring(5) }
        $reported = ($json | ConvertFrom-Json).result.serverInfo.version
        if ($reported -eq $Version) { Report PASS 'Host initialize' $reported }
        else { Report FAIL 'Host initialize' "reports $reported, expected ${Version}: redeploy the host" }
    }
    catch { Report FAIL 'Host initialize' $_.Exception.Message }
}

if ($failures -gt 0) {
    Write-Host "$failures check(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host 'Every surface matches the release.' -ForegroundColor Green
