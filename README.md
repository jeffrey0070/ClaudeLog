# ClaudeLog

Automatic conversation logger for Claude Code, Codex, and Gemini CLI, with a SQL Server backend and a web UI for browsing logged conversations.

`README.md` is the canonical setup, deployment, and configuration guide for this repo.

## Projects

- `ClaudeLog.Web` - ASP.NET Core web UI (Razor Pages + Minimal APIs)
- `ClaudeLog.Data` - ADO.NET data layer, models, repositories, SQL migrations
- `ClaudeLog.Hook.Claude` - Claude Code stop hook
- `ClaudeLog.Hook.Codex` - Codex hook (`--notify` and `--watch`)
- `ClaudeLog.Hook.Gemini` - Gemini CLI hook
- `ClaudeLog.MCP` - MCP server for manual logging flows

## Prerequisites

- Windows
- .NET SDK / runtime for the solution
- SQL Server

## Root Scripts

- `ClaudeLog.update-and-run.bat`
  Publishes all projects to `C:\Apps\ClaudeLog.*` and restarts the scheduled task host if it already exists.
- `ClaudeLog.bat`
  Manually runs the published web app in the foreground.
- `ClaudeLog.install-or-update-scheduled-task.ps1`
  Creates or updates a current-user logon task for the published web app.
- `set-connection-string.bat`
  Sets `CLAUDELOG_CONNECTION_STRING` at machine scope or current-user scope.

## Quick Start

### 1. Set the database connection string

All components read `CLAUDELOG_CONNECTION_STRING`.

Machine scope is recommended when hooks may run in other user contexts:

```bat
set-connection-string.bat
```

Current-user scope is enough for local development or a current-user scheduled task:

```bat
set-connection-string.bat user
```

Examples:

```text
Server=localhost;Database=ClaudeLog;Integrated Security=true;TrustServerCertificate=true;
Server=localhost;Database=ClaudeLog;User Id=myUsername;Password=myPassword;TrustServerCertificate=true;
```

### 2. Publish to `C:\Apps`

```bat
ClaudeLog.update-and-run.bat
```

This script:

- stops the existing scheduled task host if it is configured
- builds the solution in `Release`
- publishes all projects to `C:\Apps\ClaudeLog.*`
- restarts the existing scheduled task host if it is configured

Notes:

- Publishing to `C:\Apps` usually requires Administrator.
- The web app initializes the database automatically on startup.
- Migrations are applied from `ClaudeLog.Data/Scripts/*.sql`.
- If the scheduled task `ClaudeLog.Web` does not exist yet, publishing does not start the app automatically.

### 3. Choose how to host the published web app

#### Scheduled task

Create or update a current-user logon task:

```powershell
.\ClaudeLog.install-or-update-scheduled-task.ps1
```

The script creates a task named `ClaudeLog.Web` with these settings:

- current user
- run only when that user is logged on
- no highest-privileges elevation
- trigger at logon
- action launches `C:\Apps\ClaudeLog.Web\ClaudeLog.Web.exe`
- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://localhost:15088`

Start it immediately:

```powershell
Start-ScheduledTask -TaskName "ClaudeLog.Web"
```

#### Manual foreground run

```bat
ClaudeLog.bat
```

This is useful for local verification and troubleshooting.

### 4. Open the web UI

```text
http://localhost:15088
```

## Client Configuration

### Claude Code

#### Hook

Edit `%USERPROFILE%\.claude\settings.json`:

```json
{
  "hooks": {
    "Stop": [{
      "hooks": [{
        "type": "command",
        "command": "C:/Apps/ClaudeLog.Hook.Claude/ClaudeLog.Hook.Claude.exe",
        "timeout": 30
      }]
    }]
  }
}
```

Notes:

- Hooks have known issues in VS Code extension Native UI mode. Use MCP if needed.
- For cross-user hook execution, prefer a machine-level `CLAUDELOG_CONNECTION_STRING`.

#### MCP

Edit `%USERPROFILE%\.claude\settings.json`:

```json
{
  "mcpServers": {
    "claudelog": {
      "type": "stdio",
      "command": "C:\\Apps\\ClaudeLog.MCP\\ClaudeLog.MCP.exe",
      "args": []
    }
  }
}
```

### Codex

#### Hook

Notify mode is the preferred option for newer Codex builds.

Edit `%USERPROFILE%\.codex\config.toml` and place `notify = [...]` near the top of the file, before any `[profiles.*]` blocks:

```toml
notify = [
  "C:\\Apps\\ClaudeLog.Hook.Codex\\ClaudeLog.Hook.Codex.exe",
  "--notify"
]
```

Notes:

- Codex only reads `notify` from the root config.
- The hook derives a stable session ID from `thread-id` and logs directly from the notify payload.
- For cross-user execution, prefer a machine-level `CLAUDELOG_CONNECTION_STRING`.

Watcher mode remains available for older Codex builds:

```bat
ClaudeLog.Hook.Codex.exe --watch "%USERPROFILE%\.codex\sessions"
```

#### MCP

Edit `%USERPROFILE%\.codex\config.toml`:

```toml
[mcp_servers.claudelog]
command = "C:\\Apps\\ClaudeLog.MCP\\ClaudeLog.MCP.exe"
args = []
startup_timeout_ms = 20000
```

MCP usage flow:

1. Launch `mcp_servers.claudelog`.
2. Call `CreateSession` to create a logging session.
3. Call `LogConversation` with the current question and previous response.

Hook integration is usually better because MCP logging consumes extra tokens.

### Gemini CLI

Edit `%USERPROFILE%\.gemini\settings.json`:

```json
{
  "tools": {
    "enableHooks": true,
    "enableMessageBusIntegration": true
  },
  "hooks": {
    "AfterModel": [{
      "matcher": "*",
      "hooks": [{
        "name": "claudelog-gemini",
        "type": "command",
        "command": "C:/Apps/ClaudeLog.Hook.Gemini/ClaudeLog.Hook.Gemini.exe",
        "timeout": 30000
      }]
    }]
  }
}
```

Notes:

- `AfterModel` is the recommended event for per-turn logging.
- `SessionEnd` is optional if you want one log entry per session instead.
- For payload inspection, set `CLAUDELOG_GEMINI_DUMP_PAYLOAD=1`.
- For cross-user execution, prefer a machine-level `CLAUDELOG_CONNECTION_STRING`.

## Configuration

### Web URLs

- Production: `http://localhost:15088`
- Development: `http://localhost:15089`

Production URL comes from:

- `ASPNETCORE_URLS`
- otherwise the non-development default in `ClaudeLog.Web/Program.cs`

### Database initialization

On startup, the web app:

- creates the database if it does not exist
- creates required tables and indexes
- records the schema version
- applies pending SQL migrations

To add a migration:

1. Add `ClaudeLog.Data/Scripts/X.Y.Z.sql`
2. Rebuild or republish
3. Start the web app

## Permissions

- `ClaudeLog.update-and-run.bat`
  Usually requires Administrator because it publishes to `C:\Apps`.
- `set-connection-string.bat`
  Requires Administrator only for machine scope.
- `ClaudeLog.install-or-update-scheduled-task.ps1`
  Does not require Administrator for the current-user logon task it creates.
- `ClaudeLog.bat`
  Does not require Administrator if the published files are already accessible.

## Debugging

### Claude hook

Enable debug logging:

```bat
set CLAUDELOG_DEBUG=1
claude
```

View the log:

```bat
type %USERPROFILE%\.claudelog\hook-claude-debug.log
```

Watch it live:

```powershell
Get-Content $env:USERPROFILE\.claudelog\hook-claude-debug.log -Wait -Tail 20
```

Pause the hook for debugger attach:

```bat
set CLAUDELOG_DEBUG=1
set CLAUDELOG_WAIT_FOR_DEBUGGER=1
set CLAUDELOG_DEBUGGER_WAIT_SECONDS=60
claude
```

If the default Claude hook timeout is too short, either:

- lower `CLAUDELOG_DEBUGGER_WAIT_SECONDS`
- or increase the hook timeout in `.claude\settings.json`

### Gemini hook notes

- Hook timeouts are milliseconds. Use `30000` for 30 seconds.
- Gemini runs hook commands under PowerShell on Windows.
- `AfterModel` is streaming. Log only on the final chunk.
- If needed, republish the Gemini hook directly:

```bat
dotnet publish ClaudeLog.Hook.Gemini\ClaudeLog.Hook.Gemini.csproj -c Release -o C:\Apps\ClaudeLog.Hook.Gemini
```

## Build and Development

- Restore/build: `dotnet restore` then `dotnet build ClaudeLog.sln -c Release`
- Run web in development: `dotnet run --project ClaudeLog.Web --urls http://localhost:15089`
- Verify endpoints:
  - `/api/sessions`
  - `/api/entries`
  - `/api/errors`
