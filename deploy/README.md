# Deploying the hosted MCP server

The hosted MCP server (`src/InfoSlides.McpServer`) runs next to the InfoSlides API on the same machine, behind the same
nginx. It is a separate process with its own systemd unit, so it deploys, restarts and fails independently of the API.

What it does: serves MCP over streamable HTTP at `https://infoslides.app/mcp`, validates OAuth access tokens issued by the
InfoSlides authorization server (RS256, against the issuer's published keys), and forwards each caller's own token or
`isk_` API key to the API. It stores no credentials. Role gates, undo, audit, plan limits and revocation are enforced by
the API (`/v1`), not here.

## One-time setup (on the server)

1. `mkdir -p /opt/infoslides/mcp`
2. Copy `deploy/infoslides-mcp.service` to `/etc/systemd/system/infoslides-mcp.service`. It assumes the same user
   (`infoslides`) and dotnet path (`/usr/local/bin/dotnet`) as `infoslides.service`; check both with
   `systemctl cat infoslides | grep '^User\|^ExecStart'` and adjust if they differ.
3. Copy `deploy/mcp.env.example` to `/etc/infoslides/mcp.env` (nothing in it is secret).
4. Add the two `location` blocks from `deploy/nginx-mcp.conf` to the `infoslides.app` server block in
   `/etc/nginx/sites-available/infoslides`, then `nginx -t && systemctl reload nginx`.
5. `systemctl daemon-reload && systemctl enable infoslides-mcp`.
6. The API needs `OAuth__SigningKeyPem` set (see the OAI-04 release note), otherwise tokens are signed with an in-memory
   key the MCP host cannot validate after a restart.

## Each deploy

From this repository, in PowerShell:

```powershell
.\deploy\deploy-mcp.ps1 -Server root@<server>
```

It publishes `src/InfoSlides.McpServer`, rsyncs it to `/opt/infoslides/mcp`, restarts `infoslides-mcp`, and runs the smoke
test below. Deploy the API first when a change touches both (the host calls `/v1`).

## Smoke test

```bash
# 1. Healthy, and unauthenticated calls are challenged (401 with resource_metadata), not served.
curl -i https://infoslides.app/mcp -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \
     -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"smoke","version":"1"}}}'
#    expect: HTTP/2 401 and  WWW-Authenticate: Bearer resource_metadata="https://infoslides.app/.well-known/oauth-protected-resource/mcp"

# 2. The metadata document names the resource and the authorization server.
curl -s https://infoslides.app/.well-known/oauth-protected-resource/mcp

# 3. With an API key, tools list (72 tools) and a read works.
curl -s https://infoslides.app/mcp -H "Authorization: Bearer $INFOSLIDES_API_KEY" -H 'Content-Type: application/json' \
     -H 'Accept: application/json, text/event-stream' -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' | head -c 300

# 4. Logs.
journalctl -u infoslides-mcp -n 60 --no-pager
```

Then connect with the MCP Inspector (`npx @modelcontextprotocol/inspector`, URL `https://infoslides.app/mcp`, transport
Streamable HTTP) and walk the OAuth flow once with a real account. A Gemini custom app (US personal account) is the third
check; ChatGPT and Claude custom connectors are the fourth and fifth.

## Rollback

Deploy the previous commit with the same script. The host is stateless, so nothing needs restoring.
