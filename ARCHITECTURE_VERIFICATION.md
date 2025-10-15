# ClaudeLog Architecture Verification

## App–Service–Repo Boundaries

- Application layer:
  - `ClaudeLog.Web` (Minimal APIs, Razor Pages)
  - `ClaudeLog.MCP` (MCP server tools)
  - `ClaudeLog.Hook.Claude` and `ClaudeLog.Hook.Codex` (console hooks)
- Service layer (ClaudeLog.Data.Services):
  - `ConversationService` — sessions and entries
  - `DiagnosticsService` — logging with levels
- Repository layer (ClaudeLog.Data.Repositories):
  - `SessionRepository`, `EntryRepository`, `DiagnosticsRepository`

Verification:
- Web endpoints depend only on services (no direct repo access).
- Services depend only on repositories and DbContext.
- Repositories depend only on DbContext and ADO.NET.

## Notes

- Response DTOs are now mapped in Web endpoints to avoid leaking data-layer records.
- Diagnostics repository/file naming aligned (`DiagnosticsRepository`).
- Docs updated to reflect `DiagnosticsService` as the logging service.

Date: 2025-10-14

