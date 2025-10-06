# ClaudeLog - Conversation Logger for Claude Code

ClaudeLog automatically logs all your Claude Code Q&A conversations to SQL Server and provides a web UI to browse, search, and review them.

## Features

- **Automatic Logging**: Captures every Claude Code conversation via the Stop hook
- **Web UI**: Browse conversations with a clean, compact two-pane interface
- **Real-time Search**: Search across titles, questions, and responses (300ms debounced)
- **Section Grouping**: Conversations grouped by CLI session (newest first)
- **Favorites & Delete**: Mark conversations as favorites or deleted with inline icon buttons
- **Filtering**: Show/hide deleted entries, filter by favorites only (left panel controls)
- **Markdown Rendering**: Responses displayed with proper markdown formatting and optimized spacing
- **Editable Titles**: Click any title to rename it inline
- **Copy Functions**: Copy questions, responses, or both to clipboard
- **Chinese Support**: Proper Unicode handling for titles (200 text elements)
- **Error Logging**: All errors logged to database for diagnostics
- **Graceful Shutdown**: Ctrl+C properly stops the web application

## Project Structure

```
ClaudeLog/
â”œâ”€â”€ ClaudeLog.sln                      # Visual Studio solution
â”œâ”€â”€ ClaudeLog.Web/                     # ASP.NET Core web app
â”‚   â”œâ”€â”€ Api/                           # Minimal API endpoints
â”‚   â”œâ”€â”€ Data/                          # ADO.NET data access
â”‚   â”œâ”€â”€ Services/                      # Business logic
â”‚   â”œâ”€â”€ Middleware/                    # Error handling
â”‚   â””â”€â”€ Pages/                         # Razor Pages UI
â”œâ”€â”€ ClaudeLog.Hook.Claude/             # Claude Code hook
â””â”€â”€ Scripts/                           # SQL scripts
    â”œâ”€â”€ schema.sql
    â”œâ”€â”€ indexes.sql
    â””â”€â”€ fts.sql (Phase 3)
```

## Setup Instructions

### 1. Database Setup

The database `ClaudeLog` should already exist on `localhost` with Windows Integrated Security.

Run the SQL scripts to create tables and indexes:

```bash
sqlcmd -S localhost -d ClaudeLog -E -i Scripts\schema.sql
sqlcmd -S localhost -d ClaudeLog -E -i Scripts\indexes.sql
```

**Tables created:**
- `dbo.Sections` - CLI sessions
- `dbo.Conversations` - Q&A entries (includes Title, Question, Response, IsFavorite, IsDeleted)
- `dbo.ErrorLogs` - Error tracking

### 2. Build the Solution

Open `ClaudeLog.sln` in Visual Studio 2022 and build, or use:

```bash
dotnet build ClaudeLog.sln
```

### 3. Publish Applications

Publish both the web app and hook to production folders:

```bash
# Web application
dotnet publish ClaudeLog.Web/ClaudeLog.Web.csproj --configuration Release --output "C:/Apps/ClaudeLog.Web" --runtime win-x64 --self-contained false

# Hook application
dotnet publish ClaudeLog.Hook.Claude/ClaudeLog.Hook.Claude.csproj --configuration Release --output "C:/Apps/ClaudeLog.Hook.Claude" --runtime win-x64 --self-contained false
```

Or use the provided batch script:

```bash
build-and-publish.bat
```

### 4. Configure Claude Code Hook

Add the Stop hook to your Claude Code settings file:

**Location:** `%USERPROFILE%\.claude\settings.json`

**Add this configuration:**

```json
{
  "hooks": {
    "Stop": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "C:/Apps/ClaudeLog.Hook.Claude/ClaudeLog.Hook.Claude.exe",
            "timeout": 30
          }
        ]
      }
    ]
  }
}
```

**Important Notes:**
- Use forward slashes in paths to avoid JSON escaping issues
- The hook is configured for Claude Code v2.0.8+ transcript format
- Restart Claude Code after modifying settings

### 5. Run the Web Application

Start the published web server:

```bash
cd C:/Apps/ClaudeLog.Web
ClaudeLog.Web.exe
```

The application will be available at: **http://localhost:15088**

**For development:**
```bash
cd ClaudeLog.Web
dotnet run
```
Development runs on port 15089.

## Usage

1. **Start the web app** - Run `C:/Apps/ClaudeLog.Web/ClaudeLog.Web.exe`
2. **Use Claude Code normally** - Ask questions as usual
3. **Conversations auto-log** - Each Q&A is automatically saved after each response
4. **Browse in web UI** - Visit http://localhost:15088 to view all conversations

### Web UI Features

- **Search Bar**: Type to filter conversations (searches title, question, response)
- **Left Panel**:
  - Shows all conversations grouped by session (newest first)
  - Filter controls: Show Deleted, Favorites Only
  - Inline buttons: â­/â˜† for favorites, ðŸ—‘ï¸/â†©ï¸ for delete/restore
  - Hover over titles to see timestamp
- **Right Panel**: Shows full question and response for selected conversation
- **Click Title**: Edit title inline (in detail view)
- **Copy Buttons**: Copy question, response, or both
- **Pagination**: Loads 200 entries at a time with "Load More" button

## API Endpoints

The web app exposes these REST endpoints:

### Sections
- `POST /api/sections` - Create a new section
- `GET /api/sections` - List sections

### Entries
- `POST /api/entries` - Create a new conversation entry
- `GET /api/entries?search=&page=&pageSize=&includeDeleted=&showFavoritesOnly=` - List/search entries with filters
- `GET /api/entries/{id}` - Get entry detail
- `PATCH /api/entries/{id}/title` - Update entry title
- `PATCH /api/entries/{id}/favorite` - Toggle favorite status
- `PATCH /api/entries/{id}/deleted` - Toggle deleted status

### Errors
- `POST /api/errors` - Log an error

## Configuration

### Database Connection String

Edit `ClaudeLog.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ClaudeLog": "Server=localhost;Database=ClaudeLog;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

Update `Server` if using a different SQL Server instance.

### API Base URL (Hook)

The hook is configured to connect to the production web app port:

```csharp
private const string ApiBaseUrl = "http://localhost:15088/api";
```

If running the web app on a different port, update this in `ClaudeLog.Hook.Claude/Program.cs` and republish.

## Troubleshooting

### Hook not logging conversations

1. **Check web app is running** on http://localhost:15088
2. **Verify hook configuration** in `%USERPROFILE%\.claude\settings.json`:
   - Must use proper nested structure for Stop hook
   - Use forward slashes in paths (e.g., `C:/Apps/...`)
   - Restart Claude Code after modifying settings
3. **Check for errors** in database `dbo.ErrorLogs` table:
   ```sql
   SELECT TOP 10 * FROM dbo.ErrorLogs ORDER BY CreatedAt DESC
   ```
4. **Test hook manually**:
   ```bash
   echo '{"session_id":"test-session","transcript_path":"C:/Users/jeffr/.claude/projects/.../session.jsonl","hook_event_name":"Stop"}' | C:/Apps/ClaudeLog.Hook.Claude/ClaudeLog.Hook.Claude.exe
   ```

### Web app won't start

1. **Check database connection** - Verify SQL Server is running
2. **Check port** - Ensure port 15088 is not in use (production) or 15089 (development)
3. **Check connection string** - Verify in `appsettings.Production.json` or `appsettings.json`

### No conversations showing in UI

1. **Check database** - Query `SELECT * FROM dbo.Conversations`
2. **Check browser console** - Look for JavaScript errors
3. **Check API** - Visit http://localhost:15088/api/entries directly
4. **Test API page** - Visit http://localhost:15088/Test for manual testing

## Development

### Technology Stack

- **.NET 9.0** with ASP.NET Core
- **Razor Pages** for UI
- **Minimal APIs** for REST endpoints
- **ADO.NET** with raw SQL (no EF)
- **SQL Server** for persistence
- **Bootstrap 5** for styling
- **Vanilla JavaScript** for interactivity

### Key Libraries

- `Microsoft.Data.SqlClient` - Database access
- `Markdig` - Markdown parsing
- `HtmlSanitizer` - XSS protection
- `System.Net.Http.Json` - HTTP JSON helpers

## Roadmap

### Phase 1 (Current - MVP)
âœ… Web app with API and UI
âœ… Claude Code hook integration
âœ… Search and pagination
âœ… Error logging
âœ… Markdown rendering
âœ… Editable titles

### Phase 2 (Future)
- Shared client library (ClaudeLog.Client)
- Additional CLI support (Codex)
- Code syntax highlighting
- Export functionality (CSV/JSON)

### Phase 3 (Future)
- Full-text search (Chinese)
- Error viewer page
- Tags/metadata
- Authentication/HTTPS

## License

Private project - All rights reserved

## Support

For issues or questions, check:
1. Error logs in database: `SELECT * FROM dbo.ErrorLogs ORDER BY CreatedAt DESC`
2. Browser console for client-side errors
3. Project plan: `PROJECT_PLAN.md`

## Codex Transcript Hook (Beta)

- Executable: `C:\Apps\ClaudeLog.Hook.Codex\ClaudeLog.Hook.Codex.exe`
- Modes:
  - Stdin (preferred): Codex invokes the exe per turn and writes `{ "session_id", "transcript_path", "hook_event_name" }` to stdin. The hook logs the latest user→assistant pair.
  - Watcher (fallback): run `ClaudeLog.Hook.Codex.exe --watch "%USERPROFILE%\.codex\sessions"` to monitor JSONL transcripts and log turns automatically.
- Config:
  - API base: `CLAUDELOG_API_BASE` (default `http://localhost:15088/api`)
  - State file: `%LOCALAPPDATA%\ClaudeLog\codex_state.json`
  - Optional root: `CODEX_TRANSCRIPT_PATH` for watcher mode
- Notes:
  - Duplicate prevention via SHA-256 hash per transcript
  - Tolerant parser supports Claude-like and legacy role schemas

### Quick test (stdin mode, fake transcript)

Run this one-liner in PowerShell to simulate a Codex transcript and invoke the hook (uses a new GUID session id):

```
$tp="$env:TEMP\codex_test.jsonl"; $sid=[guid]::NewGuid().ToString(); Set-Content -Encoding UTF8 -Path $tp -Value '{"type":"user","message":{"content":[{"type":"text","text":"What is 2+2?"}]}}'; Add-Content -Encoding UTF8 -Path $tp -Value '{"type":"assistant","message":{"content":[{"type":"text","text":"4"}]}}'; $j='{"session_id":"'+$sid+'","transcript_path":"'+$tp+'","hook_event_name":"Stop"}'; $j | & 'C:\Apps\ClaudeLog.Hook.Codex\ClaudeLog.Hook.Codex.exe'
```

Then browse the web UI at `http://localhost:15088` and search for "What is 2+2?".
