# ClaudeLog Project Plan

## Summary
ClaudeLog logs every CLI Q&A to SQL Server automatically and provides a web UI to browse, search, and review entries. It uses a small capture hook for each CLI (Claude Code primarily, with extensibility for other CLIs) that sends the question and response to a local API. The web app hosts both the API and the UI, and persists to SQL Server using ADO.NET (raw SQL). Titles are generated server‑side using Unicode/Chinese‑safe truncation to 200 text elements.

## Requirements (from stakeholder)
- Database: SQL Server
- Language: C# (latest .NET; Kestrel only, no IIS)
- Persistence: ADO.NET with raw SQL (no EF)
- Time: store and display local time (no UTC)
- Title generation: simple text truncation at 200 characters, must support Chinese safely
- CLI logging behavior: whenever a question is asked, automatically create a title, and save title + question + response into a database
- Sections: when a new CLI session/section starts, call API to create a Section (GUID id, tool name, CreatedAt). All following entries reference that SectionId. Left panel groups entries by Section (Sections ordered by date desc).
- Web app UI:
  - Left panel: grouped by Section, shows question titles under each Section (Sections ordered by CreatedAt desc)
  - Right panel: shows selected question and response
  - Top search box to filter
- Extra features to include:
  1) Real-time search (title + question + response)
  2) Pagination (page size 200)
  3) Highlight selected item in list
  4) Render responses as Markdown
  5) Copy buttons for question/response
  6) Session/Section grouping option
  7) Allow modifying question title in UI (inline edit)
 - Error logging:
   1) Database table to store all errors (with error source)
   2) Public API endpoint to log errors
   3) All errors (API/internal/hooks) should be logged to database whenever possible

## High-Level Architecture
- ClaudeLog.Web (single app hosting API + Web UI)
  - Minimal JSON API at `/api/sections` and `/api/entries` to receive Sections and Q&A and serve the UI
  - Razor Pages UI (Bootstrap) with two-pane layout
  - ADO.NET (Microsoft.Data.SqlClient) for persistence; raw SQL scripts in `Scripts/`
- CLI Hooks
  - ClaudeLog.Hook.Claude: capture and forward Claude Code Q&A to API (primary hook using Stop event)
  - ClaudeLog.Hook.Codex: capture and forward other CLI Q&A to API (optional)
  - Both share a small client library for posting to the API
- Hook approaches:
  - Claude Code: uses built-in `Stop` hook that reads transcript after each response
  - Other CLIs: optional PowerShell wrapper approach

Cross-cutting
- Error logging service writes errors to dbo.ErrorLogs via ADO.NET (best effort). Global exception middleware captures unhandled errors and logs them before returning a minimal error response.

Notes
- You can start with a single web project. The hook executables are optional if you prefer using only a PowerShell wrapper. If you want per-tool hook projects, they can be thin shells sharing the same client library.

## Project Structure (Visual Studio Solution)

**Solution:** ClaudeLog.sln

**Projects:**
- ClaudeLog.Web/ (ASP.NET Core Razor Pages + Minimal API + ADO.NET)
  - Api/
    - SectionsEndpoints.cs (minimal API)
    - EntriesEndpoints.cs (minimal API)
    - ErrorsEndpoints.cs (minimal API)
    - Dtos/ (request/response DTOs)
  - Pages/
    - Index.cshtml, Index.cshtml.cs (two-pane UI)
    - Partials/ (list/detail fragments, optional)
  - Data/
    - Db.cs (ADO.NET helpers; SqlConnection factory)
    - Queries.cs (raw SQL strings)
    - ErrorLogQueries.cs (raw SQL for error insert/select)
  - Services/
    - TitleGenerator.cs (Chinese-safe truncation)
    - MarkdownRenderer.cs (Markdig + HtmlSanitizer integration)
    - ErrorLogger.cs (centralized logging to DB)
  - Middleware/
    - ErrorHandlingMiddleware.cs (global try/catch → ErrorLogger)
  - wwwroot/
    - css/, js/, highlight.js assets
  - appsettings.json (ConnectionStrings)
  - Program.cs (startup)
- ClaudeLog.Client/ (shared class library for hooks)
  - ILogClient, LogClient (HTTP), Contracts
- ClaudeLog.Hook.Claude/ (console app - reads Claude Code transcript and posts to API)
  - Program.cs
  - ClaudeLog.Hook.Claude.csproj
- ClaudeLog.Hook.Codex/ (console app; optional for future use)
  - Program.cs
  - ClaudeLog.Hook.Codex.csproj

**Solution Items:**
- Scripts/
  - schema.sql (tables)
  - indexes.sql (indexes)
  - fts.sql (optional full-text search)
- PROJECT_PLAN.md (this file)
- README.md

Minimum viable: ClaudeLog.Web + ClaudeLog.Hook.Claude. Add ClaudeLog.Client library and Codex hook later as needed.

## Database Design
Tables
- dbo.Sections
  - SectionId UNIQUEIDENTIFIER PRIMARY KEY
  - Tool NVARCHAR(32) NOT NULL -- e.g., 'ClaudeCode', 'Codex'
  - CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME() -- local time
- dbo.Conversations
  - Id BIGINT IDENTITY PRIMARY KEY
  - SectionId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Sections(SectionId)
  - Title NVARCHAR(200) NOT NULL
  - Question NVARCHAR(MAX) NOT NULL
  - Response NVARCHAR(MAX) NOT NULL
  - CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME() -- local time
- dbo.ErrorLogs
  - Id BIGINT IDENTITY PRIMARY KEY
  - Source NVARCHAR(64) NOT NULL -- e.g., 'WebApi', 'Hook.Claude', 'Hook.Codex', 'UI'
  - Message NVARCHAR(1024) NOT NULL
  - Detail NVARCHAR(MAX) NULL -- stack trace, request info, payloads (if safe)
  - Path NVARCHAR(256) NULL -- API route or context
  - SectionId UNIQUEIDENTIFIER NULL -- when known
  - EntryId BIGINT NULL -- when known
  - CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME() -- local time

Indexes
- IX_Sections_CreatedAt on dbo.Sections(CreatedAt DESC)
- IX_Conversations_SectionId_CreatedAt on dbo.Conversations(SectionId, CreatedAt)
- IX_Conversations_Title on Title (for search performance)
- IX_ErrorLogs_CreatedAt on dbo.ErrorLogs(CreatedAt DESC)
- IX_ErrorLogs_Source_CreatedAt on dbo.ErrorLogs(Source, CreatedAt DESC)
- Optional (Phase 3): Full-Text index on Title, Question, Response (Chinese word breaker)

Scripts (ADO.NET will run these)
- Scripts/schema.sql: create tables and FKs using SYSDATETIME() defaults
- Scripts/indexes.sql: create indexes as above
- Scripts/fts.sql: optional full-text catalog and index

## Title Generation (Chinese-safe)
- Rule: first 200 text elements of the Question; append “…” when truncated
- Implementation (server-side):
  ```csharp
  using System.Globalization;
  public static class TitleGenerator
  {
      public static string MakeTitle(string question)
      {
          var si = new StringInfo(question ?? string.Empty);
          int len = Math.Min(si.LengthInTextElements, 200);
          var t = si.SubstringByTextElements(0, len);
          return si.LengthInTextElements > 200 ? t + "…" : t;
      }
  }
  ```

## API Design
- POST `/api/sections`
  - Request: `{ tool: string, sectionId?: string (GUID), createdAt?: string (local) }`
  - Behavior: server uses provided `sectionId` or generates a GUID; uses provided `createdAt` or `SYSDATETIME()`; inserts dbo.Sections
  - Response: `{ sectionId: string }`
- GET `/api/sections?days=&page=&pageSize=` (optional)
  - Returns recent sections `{ sectionId, tool, createdAt, count }`
- POST `/api/entries`
  - Request: `{ sectionId: string, question: string, response: string }`
  - Behavior: server computes `title` (Chinese-safe 200) and sets `createdAt` to local time; inserts dbo.Conversations
  - Response: `{ id: number }`
- GET `/api/entries?search=&page=1&pageSize=200`
  - Returns list rows ordered by Section.CreatedAt DESC and then Entry.CreatedAt ASC; server returns flattened rows that include section info so UI can group:
  - `[ { id, title, createdAt, sectionId, sectionCreatedAt, tool } ]`
- GET `/api/entries/{id}`
  - Returns `{ id, title, question, response, createdAt, sectionId, tool, sectionCreatedAt }`
- PATCH `/api/entries/{id}/title`
  - Request: `{ title: string }`
  - Behavior: updates title; returns `{ ok: true }`
- POST `/api/errors`
  - Request: `{ source: string, message: string, detail?: string, path?: string, sectionId?: string, entryId?: number, createdAt?: string }`
  - Behavior: insert into dbo.ErrorLogs using local time if `createdAt` not provided; return `{ ok: true, id }`

Security
- Bind to localhost (or LAN) only; no auth, no login; personal use
- JSON only; parameterized queries (ADO.NET)

Search Strategy
- v1: LIKE on Title, Question, Response (parameterized), with paging
- v2: Full-Text Search (Chinese) using `CONTAINS`/`FREETEXT` for better ranking

## Web UI (Razor Pages + Bootstrap)
Layout
- Top: sticky search input + Clear (debounce ~300ms)
- Body: two scrollable panes (flexbox)
  - Left: “Question List” — date (yyyy-MM-dd) | one-line title; highlight selected; paged newest-first
  - Right: “Question & Response Detail” — full title, `[yyyy-MM-dd HH:mm:ss]`, `[Session: abc123]`; question (plain text), response (Markdown)

Features
- Real-time search across title + question + response (debounced)
- Pagination (200/page default)
- Selection highlight in list; keyboard navigation optional
- Markdown rendering for response:
  - Markdig for Markdown → HTML
  - HtmlSanitizer to prevent XSS
  - highlight.js CSS/JS for code blocks
- Copy buttons (Clipboard API) for Question, Response, Both
- Group by Section (Sections date desc); entries within a Section shown in chronological order
- Inline edit for Title (similar to ChatGPT UI: click title to rename → PATCH)
 - Error capture: on fetch errors or client exceptions, POST to `/api/errors` (best effort)

Time & Locale
- Store local time in DB (`SYSDATETIME()` default or app-provided `DateTime.Now`)
- Render local date in list; local timestamp `[yyyy-MM-dd HH:mm:ss]` in detail

Accessibility
- Use buttons/links with aria-current for selection
- Keep content encoded; only sanitized Markdown becomes HTML

## CLI Hooks

Goals
- Never block your prompt if logging fails
- Keep CLI UX identical; logging is side-effect only

### Claude Code Hook (Primary)
Uses Claude Code's built-in `Stop` hook system:
1. Configure hook in `%USERPROFILE%\.claude\settings.json`:
   ```json
   {
     "hooks": {
       "Stop": "dotnet run --project C:\\path\\to\\ClaudeLog.Hook.Claude\\ClaudeLog.Hook.Claude.csproj"
     }
   }
   ```
2. Hook receives JSON input via stdin with `transcript_path` and `session_id`
3. Hook reads the transcript (JSONL format), extracts last Q&A
4. Posts to API: `POST /api/sections` (once per session), then `POST /api/entries`

### Other CLI Hooks (Optional - PowerShell Wrapper Example)
For CLIs without built-in hook support:
```powershell
function codex {
  param([Parameter(ValueFromRemainingArguments=$true)][string[]]$parts)
  $q = ($parts -join ' ')
  $out = & codex.exe @parts 2>&1 | Tee-Object -Variable lines
  $resp = ($lines -join "`n")
  # Ensure we have a SectionId for this shell session
  if (-not $env:CODEX_SECTION_ID) {
    $sid = [guid]::NewGuid().ToString()
    try {
      $r = Invoke-RestMethod -Method POST -Uri http://localhost:5088/api/sections -ContentType 'application/json' -Body (@{tool='Codex';sectionId=$sid} | ConvertTo-Json)
      $env:CODEX_SECTION_ID = $r.sectionId
    } catch { $env:CODEX_SECTION_ID = $sid }
  }
  try {
    Invoke-RestMethod -Method POST -Uri http://localhost:5088/api/entries -ContentType 'application/json' -Body (@{sectionId=$env:CODEX_SECTION_ID;question=$q;response=$resp} | ConvertTo-Json -Depth 5) | Out-Null
  } catch {}
  return $out
}
```

## Security & Safety
- API listens on localhost (or home network) via Kestrel; no IIS
- No auth/login (personal use); bind to specific interface/port
- Sanitize rendered Markdown (HtmlSanitizer)
- Parameterize all queries (ADO.NET)
- Use NVARCHAR for Unicode safety

## Performance & Scaling
- Index on CreatedAt; server-side paging (50/page)
- Consider Full-Text Search early if responses are large and search is frequent
- Optional: cache sanitized HTML per entry in memory; recompute on demand

## Deployment
- Target latest .NET (net9.0) with Kestrel
- Local/home network: run ClaudeLog.Web self-hosted (`http://localhost:5088` by default)
- No IIS required; optional Windows Service if you want it always-on
- Config: connection string via appsettings.json or user-secrets
- Database: SQL Server (LocalDB, Express, or full edition)

## Roadmap
Phase 1 (MVP)
- Visual Studio solution: ClaudeLog.sln
- Projects: ClaudeLog.Web (ASP.NET Core) + ClaudeLog.Hook.Claude (console app)
- Database: SQL Server with schema.sql and indexes.sql
- API: Sections (create) + Entries (create/list/get/patch-title) + Errors (create)
- UI: two-pane layout, LIKE search, paging (200), Markdown rendering, copy buttons, Section grouping, editable title
- Claude Code hook integration via Stop hook
- Error logging: dbo.ErrorLogs table; POST /api/errors; global exception middleware logs to DB

Phase 2
- Add ClaudeLog.Client shared library (refactor hook to use it)
- Add ClaudeLog.Hook.Codex for other CLI tools
- Session grouping UI and session filter endpoint
- Code syntax highlighting (highlight.js)
- Export functionality (CSV/JSON)

Phase 3
- Full-Text Search (Chinese) with ranking
- Error viewer page in UI with filters by source/date/section
- Dedup options, tags/metadata
- Authentication/HTTPS if exposing beyond localhost
- Optional: Windows Service deployment

## Implementation Notes
- Project naming: ClaudeLog (matches current folder)
- Database name: ClaudeLog (or ClaudeConversations - to be decided during implementation)
- Default port: 5088
- Primary CLI: Claude Code (via Stop hook)

## Acceptance Criteria (Phase 1)
- A new Claude Code session creates a Section via API; subsequent questions log Entries tied to that SectionId
- When using Claude Code, every Q&A automatically logs `{sectionId, title, question, response, createdAt}` to SQL Server (local time)
- Hook receives transcript via stdin, extracts last Q&A, posts to API without blocking CLI
- Web UI shows two scrollable panes; left groups entries by Section (Sections date desc); search works in real-time with paging (200) and selection highlight
- Responses render as sanitized Markdown; copy buttons work; titles can be edited inline and saved via PATCH
- Unicode (Chinese) preserved end-to-end; title truncation is text-element safe (200 text elements)
- All errors (API/internal/hooks) are logged to dbo.ErrorLogs with `source`, `message`, and `createdAt` (local time); POST /api/errors accepts external error logs
- Solution builds and runs in Visual Studio; database scripts create all tables and indexes successfully
