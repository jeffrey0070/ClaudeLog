# ClaudeLog

Automatic conversation logger for Claude Code and Codex CLIs with web-based browsing interface.

## Overview

ClaudeLog captures every Q&A from your CLI conversations and stores them in SQL Server with a web UI for browsing, searching, and managing your conversation history.

## Features
1. Log Claude Code and Codex conversations
2. Support for both Hook and MCP integration methods
3. Web UI for browsing, searching, and managing conversations

## Quick Start

### Prerequisites

- SQL Server (LocalDB, Express, or full edition)

### Setup

1. **Build and publish:**
   ```bash
   ClaudeLog.update-and-run.bat
   ```
   This builds all projects, publishes to `C:\Apps\ClaudeLog.*`, and starts the web app.

   **Database initialization is automatic!** The web app will:
   - Create the database if it doesn't exist
   - Create all tables and indexes
   - Track schema version (1.0.0)
   - Automatically upgrade on future schema changes

2. **Configure Claude Code** (choose Hook or MCP):

   **Option A: Hook**

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

   **Note:** Hooks have known issues in VS Code extension Native UI mode. Use MCP if experiencing problems.

   **Option B: MCP Server**

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

3. **Configure Codex** (choose Hook or MCP):

**Option A: Hook**

**Stdin mode** (preferred):
- Codex invokes hook per turn with JSON payload
- Hook extracts last Q&A and logs to database directly

**Watcher mode** (fallback):
```bash
ClaudeLog.Hook.Codex.exe --watch "%USERPROFILE%\.codex\sessions"
```

   **Note:** Hooks have known issues with Codex

   **Option B: MCP Server**

### Codex MCP Server (Alternative - Manual control, higher token usage)

**Create/edit** `%USERPROFILE%\.codex\config.toml`:
```toml
[mcp_servers.claudelog]
command = "C:\\Apps\\ClaudeLog.MCP\\ClaudeLog.MCP.exe"
args = []
startup_timeout_ms = 20000
```


   **How to use MCP for logging:**
   When start a Claude Code session or a Codex session, type:
    1. Please launch MCP server mcp_servers.claudelog.

    2. Please call CreateSection with parameter tool="yourname" to create a new logging section, and store the returned sectionId from the response.

    3. When I say Log or Log conversation, please call LogConversation to log our previous conversation, with the following parameters:

        sessionId: The stored sectionId from the initialization step.
        question: My complete, most recent message.
        response: Your complete, preceding response.

   **Note:** MCP logging may double token consumption because the logging go through server. So try hook first.


4. **Access UI:** http://localhost:15088

**Test:**
```powershell
$tp="$env:TEMP\codex_test.jsonl"; $sid=[guid]::NewGuid().ToString(); Set-Content -Encoding UTF8 -Path $tp -Value '{"type":"user","message":{"content":[{"type":"text","text":"test?"}]}}'; Add-Content -Encoding UTF8 -Path $tp -Value '{"type":"assistant","message":{"content":[{"type":"text","text":"response"}]}}'; $j='{"session_id":"'+$sid+'","transcript_path":"'+$tp+'","hook_event_name":"Stop"}'; $j | & 'C:\Apps\ClaudeLog.Hook.Codex\ClaudeLog.Hook.Codex.exe'
```


## Architecture

### Projects

- **ClaudeLog.Data** - Shared ADO.NET data layer (repositories, models)
- **ClaudeLog.Web** - ASP.NET Core web app (Razor Pages + Minimal APIs)
- **ClaudeLog.Hook.Claude** - Claude Code Stop hook (console app)
- **ClaudeLog.Hook.Codex** - Codex hook with stdin/watcher modes (console app)
- **ClaudeLog.MCP** - MCP server for Codex integration (STDIO transport)


## Database

**Automatic initialization** - Just run the web app:
- No database? Creates it + runs all migration scripts
- Database exists? Checks version, runs pending migrations
- Connection fails? Shows error with setup instructions

**Schema versioning:**
- Migration scripts in `ClaudeLog.Data/Scripts/`: `1.0.0.sql`, `1.1.0.sql`, etc.
- Embedded as resources in Data project DLL
- Tracked in `DatabaseVersion` table
- All-or-nothing transactions (rollback on failure)

**To add a migration:**
1. Add `ClaudeLog.Data/Scripts/X.Y.Z.sql` (semantic version)
2. Rebuild the Data project
3. Run web app - applies automatically

## Configuration

**Web app port:**
- Production: 15088 (configured in `ClaudeLog.update-and-run.bat`)
- Development: 15089 (configured in `appsettings.Development.json`)

**Database connection** (`ClaudeLog.Web/appsettings.json`):
```json
{
  "ConnectionStrings": {
    "ClaudeLog": "Server=localhost;Database=ClaudeLog;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

**Hook/MCP environment variables:**
- `CLAUDELOG_CONNECTION_STRING` - Database connection string (optional, defaults to localhost)
- `CLAUDELOG_HOOK_LOGLEVEL` - Set to `verbose` for debug logging (Codex hook only)
