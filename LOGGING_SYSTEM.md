# ClaudeLog Unified Logging System

## Overview

ClaudeLog components (Web, MCP, Hooks) log diagnostics to SQL Server via a single service: `DiagnosticsService`. Logs include a severity `LogLevel` and optional context (path, sessionId, entryId).

## Architecture

- Application components → DiagnosticsService → DiagnosticsRepository → `ErrorLogs` table
- `LogLevel` enum: Trace(0), Debug(1), Info(2), Warning(3), Error(4), Critical(5)

## Usage Example

```csharp
await diagnosticsService.WriteDiagnosticsAsync(
    source: "Hook.Claude",
    message: "Processing transcript",
    level: LogLevel.Debug,
    detail: null,
    path: transcriptPath,
    sessionId: sessionId);
```

## Environment Variable Safety

Use null-coalescing defaults:

```csharp
var debugEnabled = (Environment.GetEnvironmentVariable("CLAUDELOG_DEBUG") ?? "0") == "1";
var waitForDebugger = (Environment.GetEnvironmentVariable("CLAUDELOG_WAIT_FOR_DEBUGGER") ?? "0") == "1";
var seconds = int.TryParse(Environment.GetEnvironmentVariable("CLAUDELOG_DEBUGGER_WAIT_SECONDS") ?? "60", out var s) ? s : 60;
```

## Crash Handling

- Initialize services inside `Main` with try/catch in console apps
- On failure: log critical if possible, write to stderr, return non-zero exit code
- Hooks should still output `{}` to not break caller

## Querying Logs

```sql
-- Last 24 hours
SELECT * FROM dbo.ErrorLogs WHERE CreatedAt >= DATEADD(HOUR, -24, SYSDATETIME()) ORDER BY CreatedAt DESC;

-- Errors and above
SELECT * FROM dbo.ErrorLogs WHERE LogLevel >= 4 ORDER BY CreatedAt DESC;

-- By component
SELECT * FROM dbo.ErrorLogs WHERE Source = 'Hook.Claude' ORDER BY CreatedAt DESC;
```

