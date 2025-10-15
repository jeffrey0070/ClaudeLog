# ClaudeLog Implementation Summary

## Summary

The system uses a clear App–Service–Repo architecture:
- Application components (Web, MCP, Hooks)
- Services: `ConversationService`, `DiagnosticsService`
- Repositories: `SessionRepository`, `EntryRepository`, `DiagnosticsRepository`

Logging is centralized through `DiagnosticsService.WriteDiagnosticsAsync(source, message, level, ...)` with `LogLevel` persisted to `ErrorLogs`.

## Recent Changes

- Mapped Web API responses to DTOs to decouple from data-layer records.
- Renamed repository file to `DiagnosticsRepository` for consistency with class name.
- Cleaned documentation and removed mojibake/encoding issues.
- MCP tools now serialize JSON responses using `System.Text.Json`.
- Improved Web startup URL reporting when Kestrel endpoint is not explicitly configured.
- Made the update script portable by deriving `SOURCE_DIR` from the script location.

## Database

- Automatic initialization and migrations via `DatabaseInitializer`.
- Current scripts: `1.0.0.sql` (core schema), `1.1.0.sql` (adds `LogLevel` to `ErrorLogs`).

## Next Steps (Optional)

- Add a Logs UI to browse diagnostics.
- Consider consolidating duplicate transcript message models across hooks.

