# ClaudeLog

Automatic conversation logger for Claude Code and Codex CLIs with web-based browsing interface.

## Overview

ClaudeLog captures every Q&A from your CLI conversations and stores them in SQL Server with a web UI for browsing, searching, and managing your conversation history.

## Features

- **Automatic Logging**: CLI hooks capture conversations transparently
- **Web UI**: Two-pane browser interface with search and filtering
- **Smart Favorites**: Favorite conversations stay visible even when deleted
- **Section Management**: Group conversations by CLI session, soft delete entire sections
- **Real-time Search**: 300ms debounced search across titles, questions, and responses
- **Markdown Rendering**: Sanitized HTML rendering of responses
- **Editable Titles**: Click to rename conversation titles
- **Copy Functions**: Copy questions, responses, or both to clipboard
- **Resizable Layout**: Drag to resize panels, Ctrl+B to toggle sidebar
- **Chinese Support**: Unicode-safe title generation (200 text elements)
- **Error Logging**: All errors captured in database for diagnostics

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- SQL Server (LocalDB, Express, or full edition)
- Windows (for deployment scripts)

### Setup

1. **Create database:**
   ```bash
   sqlcmd -S localhost -E -Q "CREATE DATABASE ClaudeLog"
   sqlcmd -S localhost -d ClaudeLog -E -i Scripts\schema.sql
   sqlcmd -S localhost -d ClaudeLog -E -i Scripts\indexes.sql
   ```

2. **Build and publish:**
   ```bash
   ClaudeLog.update-and-run.bat
   ```
   This builds all projects, publishes to `C:\Apps\ClaudeLog.*`, and starts the web app.

3. **Configure Claude Code** (choose MCP or Hook):

   **Option A: MCP Server (Recommended)**

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

   **How to use MCP for logging:**

   When starting a conversation with Claude, tell it to:
   1. Call `CreateSection` tool with parameter `tool="Claude"` to create a new logging section
   2. Store the returned `sectionId` from the response
   3. After EVERY user/assistant conversation turn, call `LogConversation` with:
      - `sessionId`: The `sectionId` from step 1
      - `question`: The user's message
      - `response`: Your complete response

   Example workflow:
   ```
   User: "Please call CreateSection with tool='Claude' and log all our conversations"
   Claude: [calls CreateSection, gets sectionId="abc-123"]
   Claude: [after each turn, calls LogConversation(sessionId="abc-123", question="...", response="...")]
   ```

   **Option B: Hook (Alternative - may not work in VS Code extension)**

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

4. **Access UI:** http://localhost:15088

### Codex MCP Server (Recommended)

Configure Codex to use the ClaudeLog MCP server for automatic conversation logging.

1. **Create/edit** `%USERPROFILE%\.codex\config.toml`:
   ```toml
   [mcp_servers.claudelog]
   command = "C:\\Apps\\ClaudeLog.MCP\\ClaudeLog.MCP.exe"
   args = []
   startup_timeout_ms = 20000
   ```

2. **Restart Codex** after configuration

3. **How to use MCP for logging:**

   When starting a conversation with Codex, tell it to:
   1. Call `CreateSection` tool with parameter `tool="Codex"` to create a new logging section
   2. Store the returned `sectionId` from the response
   3. After EVERY user/assistant conversation turn, call `LogConversation` with:
      - `sessionId`: The `sectionId` from step 1
      - `question`: The user's message
      - `response`: Your complete response

   Example workflow:
   ```
   User: "Please call CreateSection with tool='Codex' and log all our conversations"
   Codex: [calls CreateSection, gets sectionId="abc-123"]
   Codex: [after each turn, calls LogConversation(sessionId="abc-123", question="...", response="...")]
   ```

4. **MCP Tools available:**
   - `CreateSection(tool)` - Creates logging section, returns `sectionId` (call once per session)
   - `LogConversation(sessionId, question, response)` - Logs Q&A pairs (call after each turn)
   - `GetServerInfo()` - Server information

**Notes:**
- Use double backslashes (`\\`) in Windows paths in TOML
- MCP server uses STDIO transport (required by Codex)
- WSL2 recommended for better reliability on Windows
- Codex config is shared with VS Code extension

### Codex Hook (Alternative)

If MCP is not working, use the hook-based approach:

**Stdin mode** (preferred):
- Codex invokes hook per turn with JSON payload
- Hook extracts last Q&A and posts to API

**Watcher mode** (fallback):
```bash
ClaudeLog.Hook.Codex.exe --watch "%USERPROFILE%\.codex\sessions"
```

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

### Database

**Tables:**
- `dbo.Sections` - CLI sessions (SectionId, Tool, IsDeleted, CreatedAt)
- `dbo.Conversations` - Q&A entries (Id, SectionId, Title, Question, Response, IsFavorite, IsDeleted, CreatedAt)
- `dbo.ErrorLogs` - Error tracking (Id, Source, Message, Detail, Path, SectionId, EntryId, CreatedAt)

**Key behavior:**
- All timestamps are local time (SYSDATETIME())
- Title column is NVARCHAR(400) supporting 200 Unicode text elements
- Soft delete on both conversations and sections
- Favorites always visible in queries: `WHERE (@IncludeDeleted = 1 OR c.IsFavorite = 1 OR (c.IsDeleted = 0 AND s.IsDeleted = 0))`

### API Endpoints

**Sections:**
- `POST /api/sections` - Create section
- `GET /api/sections?days=30&page=1&pageSize=50&includeDeleted=false` - List sections
- `PATCH /api/sections/{sectionId}/deleted` - Toggle section deleted

**Entries:**
- `POST /api/entries` - Create entry
- `GET /api/entries?search=&page=1&pageSize=200&includeDeleted=false&showFavoritesOnly=false` - List/search entries
- `GET /api/entries/{id}` - Get entry detail
- `PATCH /api/entries/{id}/title` - Update title
- `PATCH /api/entries/{id}/favorite` - Toggle favorite
- `PATCH /api/entries/{id}/deleted` - Toggle deleted

**Errors:**
- `POST /api/errors` - Log error

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

**Codex hook environment variables:**
- `CLAUDELOG_API_BASE` - API URL (default: http://localhost:15088/api)
- `CLAUDELOG_HOOK_LOGLEVEL` - Set to `verbose` for debug logging

## Usage

### Web UI

**Search:** Type in top bar to filter (searches title, question, response)

**Left panel:**
- Conversations grouped by section (newest first)
- Checkboxes: Show Deleted, Favorites Only
- Inline buttons: ⭐/☆ (favorite), 🗑️/↩️ (delete/restore) on conversations and sections
- Favorites always visible regardless of deleted status
- Drag resize handle or press Ctrl+B to toggle sidebar
- Hover titles for timestamp

**Right panel:**
- Click conversation to view full Q&A
- Click title to edit inline
- Copy buttons for question, response, or both
- Markdown rendering for responses

**Pagination:** 200 entries per page, "Load More" button at bottom

### Hooks

**Claude Code hook:**
- Triggered automatically on Stop event (after each response)
- Reads transcript JSONL, extracts last user→assistant pair
- Creates section (once per session), posts entry

**Codex hook:**
- Stdin mode: Per-turn invocation with JSON payload
- Watcher mode: Monitors transcript folder for changes
- Duplicate prevention via SHA-256 hash
- State file: `%LOCALAPPDATA%\ClaudeLog\codex_state.json`

## Troubleshooting

**Hook not logging:**
1. Check web app running: http://localhost:15088
2. Verify hook config in `%USERPROFILE%\.claude\settings.json`
3. Restart CLI after config changes
4. Check error logs: `SELECT TOP 10 * FROM dbo.ErrorLogs ORDER BY CreatedAt DESC`

**Web app won't start:**
1. Verify SQL Server running
2. Check port 15088 not in use
3. Verify connection string in appsettings.json

**No conversations in UI:**
1. Query database: `SELECT * FROM dbo.Conversations`
2. Check browser console for JavaScript errors
3. Test API directly: http://localhost:15088/api/entries
4. Use Test page: http://localhost:15088/Test

## Development

**Technology:**
- .NET 9.0 with ASP.NET Core Kestrel
- Razor Pages (server-side rendering)
- Minimal APIs (REST endpoints)
- ADO.NET with raw SQL (no ORM)
- Bootstrap 5 + vanilla JavaScript
- SQL Server (Windows Integrated Security)

**Key libraries:**
- Microsoft.Data.SqlClient
- Markdig (Markdown parsing)
- HtmlSanitizer (XSS protection)

**Build:**
```bash
dotnet build ClaudeLog.sln
```

**Run dev server:**
```bash
cd ClaudeLog.Web
dotnet run
```

**Publish:**
```bash
ClaudeLog.update-and-run.bat
```

## Project Structure

```
ClaudeLog/
├── ClaudeLog.sln
├── ClaudeLog.Data/              # Shared data layer
│   ├── DbContext.cs
│   ├── Models/                  # DTOs (Entry, Section, ErrorLog)
│   └── Repositories/            # ADO.NET repos (EntryRepository, etc.)
├── ClaudeLog.Web/               # Web application
│   ├── Api/                     # Minimal API endpoints
│   ├── Middleware/              # ErrorHandlingMiddleware
│   ├── Pages/                   # Razor Pages (Index, Test, Error)
│   ├── Services/                # TitleGenerator, MarkdownRenderer, ErrorLogger
│   ├── wwwroot/                 # Static assets (css, js)
│   ├── appsettings.json
│   └── Program.cs
├── ClaudeLog.Hook.Claude/       # Claude Code hook
│   └── Program.cs
├── ClaudeLog.Hook.Codex/        # Codex hook (stdin/watcher)
│   └── Program.cs
├── ClaudeLog.MCP/               # MCP server for Codex
│   ├── Program.cs
│   ├── LoggingTools.cs          # MCP tool definitions
│   └── LoggingService.cs        # HTTP client for API calls
├── Scripts/                     # SQL scripts
│   ├── schema.sql
│   ├── indexes.sql
│   ├── migration_001_add_favorite_deleted.sql
│   └── fts.sql
├── ClaudeLog.update-and-run.bat # Build and deploy script
├── CLAUDE.md                    # Instructions for Claude
├── CONTEXT.md                   # Quick project status
├── PROJECT_PLAN.md              # Architecture and design decisions
└── README.md                    # This file
```

## License

Private project - All rights reserved

## Support

- Error logs: `SELECT * FROM dbo.ErrorLogs ORDER BY CreatedAt DESC`
- Browser console (F12) for client-side errors
- See PROJECT_PLAN.md for technical details
