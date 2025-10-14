# ClaudeLog Unified Logging System

## Overview

ClaudeLog implements a comprehensive, structured logging system with multiple severity levels. All application components (Web, API, MCP, Hooks) use a unified logging service that persists logs to the database.

## Architecture

```
┌────────────────────────────────────────────────────────────┐
│  Application Components                                     │
│  - ClaudeLog.Web (API, Middleware)                         │
│  - ClaudeLog.MCP                                           │
│  - ClaudeLog.Hook.Claude                                   │
│  - ClaudeLog.Hook.Codex                                    │
└──────────────────────┬─────────────────────────────────────┘
                       │ Uses
                       ▼
┌────────────────────────────────────────────────────────────┐
│  LoggingService (ClaudeLog.Data.Services)                  │
│  - LogAsync(source, message, level, detail...)             │
│  - LogTraceAsync()    // Detailed debugging                │
│  - LogDebugAsync()    // Diagnostic info                   │
│  - LogInfoAsync()     // Normal operation                  │
│  - LogWarningAsync()  // Potential problems                │
│  - LogErrorAsync()    // Failures                          │
│  - LogCriticalAsync() // Severe failures                   │
└──────────────────────┬─────────────────────────────────────┘
                       │ Uses
                       ▼
┌────────────────────────────────────────────────────────────┐
│  ErrorRepository → ErrorLogs Table (SQL Server)            │
│  Columns: Id, Source, Message, Detail, Path, SessionId,    │
│           EntryId, CreatedAt, LogLevel                     │
└────────────────────────────────────────────────────────────┘
```

## Log Levels

| Level | Value | Purpose | Example Use Cases |
|-------|-------|---------|-------------------|
| **Trace** | 0 | Detailed execution flow | Method entry/exit, variable values, raw data dumps |
| **Debug** | 1 | Diagnostic information | Processing steps, calculated values, state changes |
| **Info** | 2 | Normal operation events | Successful operations, milestone completions |
| **Warning** | 3 | Potentially problematic situations | Missing optional data, fallback behavior, deprecated usage |
| **Error** | 4 | Failures that don't stop the app | Failed API calls, validation errors, recoverable exceptions |
| **Critical** | 5 | Severe failures | Unhandled exceptions, initialization failures, data corruption |

## Usage Examples

### In Application Code

```csharp
// Inject or create LoggingService
var loggingService = new LoggingService(dbContext);

// Trace-level logging (very detailed)
await loggingService.LogTraceAsync("Hook.Claude",
    "Processing transcript",
    $"File: {path}, Lines: {lineCount}");

// Debug-level logging (diagnostic)
await loggingService.LogDebugAsync("Hook.Claude",
    $"Found {messages.Count} messages in transcript");

// Info-level logging (normal operations)
await loggingService.LogInfoAsync("Hook.Claude",
    $"Entry logged successfully (ID: {entryId})");

// Warning-level logging (potential issues)
await loggingService.LogWarningAsync("Hook.Claude",
    "Missing optional field",
    $"SessionId provided but TranscriptPath was null");

// Error-level logging (failures)
await loggingService.LogErrorAsync("Hook.Claude",
    "Failed to read transcript file",
    ex.StackTrace,
    path: transcriptPath);

// Critical-level logging (severe failures)
await loggingService.LogCriticalAsync("Hook.Claude",
    "Unhandled exception in Main",
    $"{ex.Message}\n{ex.StackTrace}");
```

### General-Purpose Logging

```csharp
// Log with any level
await loggingService.LogAsync(
    source: "MyComponent",
    message: "Custom operation completed",
    level: LogLevel.Info,
    detail: "Additional context here",
    path: "/api/endpoint",
    sessionId: "session-guid",
    entryId: 12345,
    createdAt: DateTime.UtcNow
);
```

## Environment.GetEnvironmentVariable Safety

### Problem
`Environment.GetEnvironmentVariable()` returns `null` when a variable is not set. Using this in static field initializers can cause `NullReferenceException` crashes **before** Main() executes, bypassing all exception handlers.

### Solution
Always use null-coalescing operator (`??`) with default values:

```csharp
// ❌ UNSAFE - Can crash if variable not set
private static readonly bool _debugEnabled =
    Environment.GetEnvironmentVariable("CLAUDELOG_DEBUG") == "1";

// ✅ SAFE - Defaults to "0" if variable not set
private static readonly bool _debugEnabled =
    (Environment.GetEnvironmentVariable("CLAUDELOG_DEBUG") ?? "0") == "1";

// ✅ SAFE - Defaults to "60" for parsing
private static readonly int _timeoutSeconds = int.TryParse(
    Environment.GetEnvironmentVariable("TIMEOUT_SECONDS") ?? "60",
    out var seconds) ? seconds : 60;
```

## Crash Handling Best Practices

### 1. Safe Static Field Initialization
```csharp
// Initialize nullable references, create actual instances in Main()
private static DbContext? _dbContext = null;
private static LoggingService? _loggingService = null;

static async Task<int> Main(string[] args)
{
    try
    {
        _dbContext = new DbContext();
        _loggingService = new LoggingService(_dbContext);
        // ... rest of main logic
    }
    catch (Exception ex)
    {
        // Log critical error if possible
        if (_loggingService != null)
        {
            await _loggingService.LogCriticalAsync(...);
        }
        return 1; // Non-zero exit code
    }
}
```

### 2. Top-Level Exception Handler
All hooks and MCP services have comprehensive exception handlers that:
- Log critical errors to database (if possible)
- Write to stderr for console visibility
- Always output `{}` for hooks (so Claude Code doesn't fail)
- Return appropriate exit codes

### 3. Fail-Safe Error Logging
The `LoggingService` methods:
- Never throw exceptions (all wrapped in try-catch)
- Return `null` on failure instead of throwing
- Prevent infinite loops in error logging

## Debugging Mechanisms

### 1. Database Logging (Always Available)
All log messages are persisted to `ErrorLogs` table with:
- Timestamp
- Source component
- Log level
- Message and detailed information
- Optional context (path, sessionId, entryId)

Query logs:
```sql
-- View all logs from last 24 hours
SELECT * FROM ErrorLogs
WHERE CreatedAt >= DATEADD(HOUR, -24, GETDATE())
ORDER BY CreatedAt DESC;

-- View only errors and criticals
SELECT * FROM ErrorLogs
WHERE LogLevel >= 4  -- Error or Critical
ORDER BY CreatedAt DESC;

-- View logs for specific component
SELECT * FROM ErrorLogs
WHERE Source = 'Hook.Claude'
ORDER BY CreatedAt DESC;
```

### 2. Debug File Logging (Hook.Claude Only)
When `CLAUDELOG_DEBUG=1` environment variable is set:
- Logs are also written to `%USERPROFILE%\.claudelog\hook-claude-debug.log`
- Includes all database logs plus additional debug output
- Useful when database is unavailable

Enable debug logging:
```powershell
# PowerShell
$env:CLAUDELOG_DEBUG = "1"

# Batch/CMD
set CLAUDELOG_DEBUG=1
```

### 3. Verbose Console Logging (Hook.Codex Only)
When `CLAUDELOG_HOOK_LOGLEVEL=verbose` environment variable is set:
- Additional console output for watcher mode
- Shows file processing steps
- Useful for real-time monitoring

Enable verbose logging:
```powershell
$env:CLAUDELOG_HOOK_LOGLEVEL = "verbose"
```

### 4. Debugger Attachment (Hook.Claude Only)
For advanced debugging, Hook.Claude supports waiting for debugger attachment:

```powershell
# Wait 60 seconds for debugger
$env:CLAUDELOG_WAIT_FOR_DEBUGGER = "1"

# Custom wait time (seconds)
$env:CLAUDELOG_DEBUGGER_WAIT_SECONDS = "120"
```

The hook will:
1. Start execution
2. Display Process ID (PID)
3. Wait for debugger to attach
4. Break at attachment point
5. Continue normal execution

## Troubleshooting

### Logs Not Appearing in Database

1. **Check database connection**:
   ```sql
   SELECT COUNT(*) FROM ErrorLogs;
   ```

2. **Verify service initialization**:
   - Check stderr output for "CRITICAL: Failed to initialize database services"
   - Logs before initialization cannot be persisted

3. **Check log level filtering**:
   - Trace and Debug logs may be filtered in production
   - Use Error or Critical for important messages

### Hook Crashes Immediately

1. **Check static field initialization**:
   - Look for null reference exceptions
   - Ensure all `Environment.GetEnvironmentVariable()` calls use `??` operator

2. **Check database connectivity**:
   - Hook.Claude and Hook.Codex crash if database unavailable
   - Check connection string in appsettings.json or environment variables

3. **Enable debug logging**:
   ```powershell
   $env:CLAUDELOG_DEBUG = "1"
   ```
   Check `%USERPROFILE%\.claudelog\hook-claude-debug.log`

### Missing Stack Traces

Ensure you're passing `ex.StackTrace` to logging methods:
```csharp
catch (Exception ex)
{
    // ✅ Includes stack trace
    await loggingService.LogErrorAsync("Source",
        ex.Message,
        ex.StackTrace ?? "");

    // ❌ No stack trace
    await loggingService.LogErrorAsync("Source", ex.Message);
}
```

## Performance Considerations

1. **Async Logging**: All logging methods are async to avoid blocking
2. **Database Writes**: Each log call writes immediately (no batching)
3. **Log Level**: Use appropriate levels to avoid excessive database writes
   - Use Trace/Debug sparingly in production
   - Prefer Info/Warning/Error for operational logs

## Migration

### Database Schema Version 1.1.0
The logging system requires database schema v1.1.0 or higher.

Migration automatically applies when the Web application starts:
- Adds `LogLevel INT NOT NULL DEFAULT 4` to `ErrorLogs` table
- Creates index on `(LogLevel, CreatedAt DESC)`
- Existing logs default to Error level (4)

## Summary

### Key Benefits
✅ **Unified logging** across all components
✅ **Structured log levels** (Trace → Critical)
✅ **Database persistence** with queryable history
✅ **Safe initialization** preventing startup crashes
✅ **Comprehensive exception handling** at all layers
✅ **Multiple debugging mechanisms** (database, file, console)
✅ **Fail-safe design** preventing infinite error loops

### Best Practices
1. Use appropriate log levels for each message
2. Include stack traces for Error and Critical logs
3. Use null-coalescing for environment variables
4. Initialize services inside try-catch blocks
5. Always have a top-level exception handler
6. Query database logs for troubleshooting

---

**Date**: 2025-10-13
**Schema Version**: 1.1.0
**Status**: ✅ Implemented and Verified
