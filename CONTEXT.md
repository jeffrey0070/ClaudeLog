# ClaudeLog Project Context

> **FOR CLAUDE**: Read this file at the start of each session to understand the project state.

## 📚 Documents to Read First

1. **CONTEXT.md** (this file) - Current project state and context
2. **README.md** - Complete setup instructions and documentation
3. **PROJECT_PLAN.md** - Original requirements and future roadmap

## 📋 What This Project Does

Automatically logs Claude Code CLI conversations to SQL Server with a web UI for browsing/searching. See PROJECT_PLAN.md for full requirements.

## ✅ What Has Been Done

### Completed Tasks
- ✅ Full implementation of web app (ASP.NET Core 9.0 + Razor Pages)
- ✅ Database schema created (Sections, Conversations, ErrorLogs)
- ✅ Database indexes for performance optimization
- ✅ IsFavorite and IsDeleted columns with filtering support
- ✅ Claude Code hook integration (Stop hook)
- ✅ Hook successfully parsing Claude Code v2.0.8 transcript format
- ✅ Production deployment to `C:/Apps/ClaudeLog.Web` (port 15088)
- ✅ Development environment configured (VS 2022, port 15089)
- ✅ Hook published to `C:/Apps/ClaudeLog.Hook.Claude`
- ✅ Hook configured in `~/.claude/settings.json`
- ✅ Hook tested and working - conversations logging successfully
- ✅ Git repository initialized with commits
- ✅ Comprehensive documentation (README, PROJECT_PLAN, CLAUDE.md)
- ✅ `.gitignore` configured
- ✅ UI improvements: inline favorite/delete buttons, compact layout
- ✅ Graceful shutdown handling (Ctrl+C)
- ✅ Whitespace trimming and markdown rendering optimization

## 🎯 What Needs to Be Done Next

### Immediate Tasks
1. **Push to GitHub** - User will run commands manually:
   ```bash
   cd C:/Users/jeffr/source/repos/ClaudeLog
   gh repo create ClaudeLog --public --source=. --description="Automatic conversation logger for Claude Code CLI with web-based browsing interface" --push
   ```

### Future Enhancements
See PROJECT_PLAN.md Phase 2/3 for planned features.

## Key Technical Details

### Architecture
```
ClaudeLog.Web (port 5089)
├── Minimal APIs (/api/sections, /api/entries, /api/errors)
├── Razor Pages (/, /Test)
├── SQL Server (localhost\ClaudeLog)
└── Services (TitleGenerator, MarkdownRenderer, ErrorLogger)

ClaudeLog.Hook.Claude
└── Parses JSONL transcript → Posts to API
```

### Database
- **Server**: localhost
- **Database**: ClaudeLog
- **Auth**: Windows Integrated Security
- **Tables**: Sections, Conversations, ErrorLogs
- **All timestamps**: Local time (SYSDATETIME())

### Hook Configuration
**Location**: `C:/Users/jeffr/.claude/settings.json`
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

### Important Code Changes Made
1. **Fixed transcript parsing** - Updated `ClaudeLog.Hook.Claude/Program.cs` to handle Claude Code v2.0.8 format:
   - Changed from `{"role":"user"}` to `{"type":"user", "message":{...}}`
   - Created `TranscriptMessage` and `MessageContent` classes

2. **Fixed conversation ordering** - Updated `ClaudeLog.Web/Data/Queries.cs`:
   - Changed from `ORDER BY s.CreatedAt DESC, c.CreatedAt ASC`
   - To `ORDER BY s.CreatedAt DESC, c.CreatedAt DESC`
   - Now shows newest conversations first within each section

3. **Added favorite/delete functionality** - Database and UI updates:
   - Added IsFavorite and IsDeleted columns to Conversations table
   - Created migration script and updated schema.sql
   - Added API endpoints: PATCH /api/entries/{id}/favorite and /api/entries/{id}/deleted
   - Added inline icon buttons (⭐/☆ and 🗑️/↩️) in conversation list
   - Moved filters to left panel for better UX

4. **UI/UX improvements**:
   - Compact layout with reduced padding and whitespace
   - Fixed overflow issues in right panel
   - Optimized markdown rendering with proper spacing
   - Added graceful shutdown with Ctrl+C handling
   - Trimmed whitespace from questions and responses
   - Added startup information display

## How to Start the System

1. **Start web app**:
   ```bash
   cd C:/Apps/ClaudeLog.Web
   ClaudeLog.Web.exe
   ```

2. **Use Claude Code normally** - hook automatically logs after each response

3. **View conversations**: http://localhost:5089

## Important Files
- **Source**: `C:/Users/jeffr/source/repos/ClaudeLog/`
- **Published Web**: `C:/Apps/ClaudeLog.Web/`
- **Published Hook**: `C:/Apps/ClaudeLog.Hook.Claude/`
- **Settings**: `C:/Users/jeffr/.claude/settings.json`

## Quick Commands

**Rebuild and republish everything**:
```bash
cd C:/Users/jeffr/source/repos/ClaudeLog
build-and-publish.bat
```

**Check database**:
```sql
SELECT TOP 10 * FROM dbo.Conversations ORDER BY CreatedAt DESC
SELECT TOP 10 * FROM dbo.ErrorLogs ORDER BY CreatedAt DESC
```

**Test hook manually**:
```bash
echo '{"session_id":"test","transcript_path":"C:/Users/jeffr/.claude/projects/.../session.jsonl","hook_event_name":"Stop"}' | C:/Apps/ClaudeLog.Hook.Claude/ClaudeLog.Hook.Claude.exe
```

## Known Issues & Solutions

### Issue: Transcript format mismatch
**Solution**: Already fixed in current code. Hook now correctly parses Claude Code v2.0.8 JSONL format.

### Issue: Hook not running
**Solutions**:
1. Check web app is running on port 5089
2. Verify settings.json has correct format
3. Restart Claude Code after changing settings
4. Check ErrorLogs table in database

### Issue: Can't republish web app (file in use)
**Solution**:
```bash
taskkill /F /IM ClaudeLog.Web.exe
```

## 💡 Important Things to Remember

### Critical Bugs Fixed
1. **Transcript parsing** - Hook was failing because Claude Code v2.0.8 changed transcript format:
   - Old: `{"role":"user", "content":"..."}`
   - New: `{"type":"user", "message":{"role":"user", "content":"..."}}`
   - Fix: Updated `TranscriptMessage` and `MessageContent` classes in `ClaudeLog.Hook.Claude/Program.cs:214-230`

2. **Conversation ordering** - Conversations were showing oldest first within sections:
   - Fixed in `ClaudeLog.Web/Data/Queries.cs:33`
   - Changed to `ORDER BY s.CreatedAt DESC, c.CreatedAt DESC`

### System is Working
- ✅ Hook tested and successfully logged "what is 2+2?" conversation
- ✅ Web app running on port 5089
- ✅ Database confirmed working with test data
- ✅ All code changes published to production folders

### If Something Breaks
1. **Hook not logging**: Check ErrorLogs table, verify web app is running, restart Claude Code
2. **Can't republish**: `taskkill /F /IM ClaudeLog.Web.exe`
3. **Database issues**: Connection string in `appsettings.Production.json`

---
**Last Updated**: 2025-10-05 (Session: de1881c3-292d-4dbc-97a4-519ff0b95898)
