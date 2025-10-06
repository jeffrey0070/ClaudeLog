# ClaudeLog Project Plan

## Vision

Automatically capture all CLI Q&A conversations to SQL Server and provide a web UI for browsing, searching, and reviewing conversation history.

## Scope

**In scope:**
- Automatic conversation logging from Claude Code and Codex CLIs
- Local SQL Server storage with web-based browsing interface
- Search, filtering, and organization of conversation history
- Soft delete and favorites for conversation management

**Out of scope:**
- Cloud storage or multi-user support
- Authentication or authorization
- Mobile apps or responsive design
- Real-time collaboration features

## Phase 1: Core Functionality (Complete)

### Data Capture
- Claude Code hook integration via Stop event
- Codex hook with stdin and watcher modes
- Automatic section creation per CLI session
- Title auto-generation from question text (Unicode-safe)
- Error logging to database

### Web Interface
- Two-pane layout for browsing conversations
- Real-time search across titles, questions, and responses
- Section grouping by CLI session
- Pagination with 200 entries per page
- Markdown rendering for responses
- Inline title editing
- Copy to clipboard (question, response, or both)
- Resizable and collapsible sidebar

### Conversation Management
- Mark conversations as favorites
- Soft delete for conversations (with restore)
- Soft delete for entire sections (with restore)
- Smart filtering: favorites always visible even when deleted
- Filter options: Show Deleted, Favorites Only

### Technical Foundation
- REST API for sections, entries, and errors
- ADO.NET data access with SQL Server
- Local timestamps (no UTC conversion)
- Error tracking in database

## Technical Architecture

### Components
- **ClaudeLog.Data** - Shared data access layer
- **ClaudeLog.Web** - Web app hosting API and UI
- **ClaudeLog.Hook.Claude** - Claude Code integration
- **ClaudeLog.Hook.Codex** - Codex CLI integration
- **ClaudeLog.MCP** - MCP server (in progress)

### Technology Stack
- .NET 9.0 with ASP.NET Core
- SQL Server with Windows Integrated Security
- Razor Pages for UI
- Minimal APIs for REST endpoints
- ADO.NET for data access (no ORM)
- Bootstrap 5 and vanilla JavaScript

### Data Model
- **Sections** - CLI sessions (one per conversation session)
- **Conversations** - Q&A entries (question, response, title, flags)
- **ErrorLogs** - Diagnostic information

### Deployment
- Localhost web server on port 15088 (production) or 15089 (dev)
- Published to `C:\Apps\ClaudeLog.*` folders
- Self-contained executables (no installer needed)

## Known Limitations

- Single-user localhost only (no multi-user or network access)
- Search uses LIKE (slow on large datasets without full-text index)
- No mobile support (desktop browser only)
- Windows-only deployment (batch scripts, SQL Server)
- Manual deployment process (no installer or auto-update)

