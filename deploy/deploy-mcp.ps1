# Publishes the hosted MCP server, uploads it and restarts its service, then smoke-tests it.
# Usage:  .\deploy\deploy-mcp.ps1 -Server root@<server>
# Requires rsync (included with Git for Windows) and SSH access. Nothing here touches the API or its secrets.

param(
    [Parameter(Mandatory = $true)][string]$Server,
    [string]$Url = "https://infoslides.app"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "publish\mcp"

if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
dotnet publish (Join-Path $root "src\InfoSlides.McpServer\InfoSlides.McpServer.csproj") -c Release -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# rsync --delete keeps the server folder identical to the build (no stale files).
# The rsync on Windows is MSYS2 (Git for Windows or Tizen Studio): it wants /c/... paths, and its own ssh does not
# find the Windows ~/.ssh key, so the key is passed explicitly. --no-o --no-g: Windows uid/gid do not map to Linux.
$toMsys = { param($p) '/' + (($p -replace '\\', '/') -replace '^([A-Za-z]):', '$1') }
$key = @("id_ed25519", "id_rsa") | ForEach-Object { Join-Path $HOME ".ssh\$_" } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $key) { throw "No SSH key found in ~/.ssh (id_ed25519 or id_rsa)." }
rsync -a --delete --no-o --no-g -e "ssh -i $(& $toMsys $key)" "$(& $toMsys $publish)/" "${Server}:/opt/infoslides/mcp/"
if ($LASTEXITCODE -ne 0) { throw "rsync failed" }

ssh $Server "systemctl restart infoslides-mcp && sleep 2 && systemctl is-active infoslides-mcp"
if ($LASTEXITCODE -ne 0) { throw "infoslides-mcp did not start; see: ssh $Server journalctl -u infoslides-mcp -n 60 --no-pager" }

# Unauthenticated requests must be challenged with a pointer to the resource metadata.
$body = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"smoke","version":"1"}}}'
try {
    Invoke-WebRequest -Uri "$Url/mcp" -Method Post -Body $body -ContentType "application/json" -Headers @{ Accept = "application/json, text/event-stream" } -UseBasicParsing | Out-Null
    throw "Expected 401 from $Url/mcp but the request was served."
} catch [System.Net.WebException] {
    $response = $_.Exception.Response
    $challenge = $response.Headers["WWW-Authenticate"]
    if ([int]$response.StatusCode -ne 401 -or $challenge -notmatch "resource_metadata=") {
        throw "Smoke test failed: status $([int]$response.StatusCode), WWW-Authenticate '$challenge'"
    }
    Write-Host "OK: $Url/mcp answers 401 with $challenge" -ForegroundColor Green
}
