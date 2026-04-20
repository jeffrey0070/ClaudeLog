# Export/Import Core Data Plan

## Goal

Implement file-based export/import for core data with this end state:

- `Sessions` remain keyed by `SessionId`
- `Conversations.Id BIGINT IDENTITY` is removed
- `Conversations.ConversationId UNIQUEIDENTIFIER` becomes the primary key
- `Sessions` and `Conversations` both gain `LastModifiedAt`
- import behavior is timestamp-based:
  - not found: insert
  - found: compare `LastModifiedAt`
  - imported row wins only when `import.LastModifiedAt > db.LastModifiedAt`
- soft-deleted rows are intentionally excluded from transfer:
  - do not export soft-deleted rows
  - do not update an existing target row that is already soft-deleted

---

## Task 1. Finalize the target contract

### Objective

Lock down the identifiers, timestamps, and file format before any schema or code work starts.

### Decisions

- `Sessions` match on `SessionId`
- `Conversations` match on `ConversationId`
- `ConversationId` is a `UNIQUEIDENTIFIER`, not a string
- `LastModifiedAt` exists on both `Sessions` and `Conversations`
- export/import uses one JSON file containing both sessions and conversations
- API and UI should move to `conversationId`, not keep a generic `id`
- diagnostics should move from `EntryId` to `ConversationId`

### Deliverable

- this plan document is accepted as the implementation baseline

---

## Task 2. Define the export/import file contract

### Objective

Create a stable JSON contract that the export and import features will use.

### Work

- define top-level metadata:
  - `formatVersion`
  - `exportedAt`
- define session payload fields:
  - `sessionId`
  - `tool`
  - `isDeleted`
  - `createdAt`
  - `lastModifiedAt`
- define conversation payload fields:
  - `conversationId`
  - `title`
  - `question`
  - `response`
  - `isFavorite`
  - `isDeleted`
  - `createdAt`
  - `lastModifiedAt`
- decide validation rules for missing, invalid, or duplicate IDs
- define soft-delete transfer rules:
  - export excludes soft-deleted sessions and conversations
  - import skips updates when the target row is already soft-deleted

### Deliverable

- DTO/contract design ready for implementation

### Reference shape

```json
{
  "formatVersion": 1,
  "exportedAt": "2026-04-19T12:00:00Z",
  "sessions": [
    {
      "sessionId": "abc",
      "tool": "Codex",
      "isDeleted": false,
      "createdAt": "2026-04-18T10:00:00Z",
      "lastModifiedAt": "2026-04-18T10:05:00Z",
      "conversations": [
        {
          "conversationId": "11111111-1111-1111-1111-111111111111",
          "title": "Example",
          "question": "Question text",
          "response": "Response text",
          "isFavorite": false,
          "isDeleted": false,
          "createdAt": "2026-04-18T10:01:00Z",
          "lastModifiedAt": "2026-04-18T10:05:00Z"
        }
      ]
    }
  ]
}
```

---

## Task 3. Design the schema migration

### Objective

Prepare a safe SQL migration path from numeric conversation IDs to GUID conversation IDs.

### Work

- add `LastModifiedAt` to `dbo.Sessions`
- add `ConversationId UNIQUEIDENTIFIER NULL` to `dbo.Conversations`
- add `LastModifiedAt` to `dbo.Conversations`
- add `ConversationId UNIQUEIDENTIFIER NULL` to `dbo.ErrorLogs`
- backfill:
  - `Sessions.LastModifiedAt = CreatedAt`
  - `Conversations.ConversationId = NEWID()`
  - `Conversations.LastModifiedAt = CreatedAt`
- migrate diagnostics references:
  - populate `ErrorLogs.ConversationId` by joining `ErrorLogs.EntryId` to `Conversations.Id`
- add new indexes and constraints for `ConversationId`
- cut over the primary key from `Conversations.Id` to `Conversations.ConversationId`
- drop the old columns only after migration validation succeeds:
  - `Conversations.Id`
  - `ErrorLogs.EntryId`

### Validation checks

- fail if any conversation still has `ConversationId IS NULL`
- fail if duplicate `ConversationId` values exist
- fail if any `ErrorLogs.EntryId` rows could not be mapped before drop

### Deliverable

- one new migration script in `ClaudeLog.Data/Scripts`

### Risk note

This is a destructive schema migration. Database backup before deployment is strongly recommended.

---

## Task 4. Implement timestamp maintenance rules

### Objective

Ensure `LastModifiedAt` is maintained correctly for normal writes and imports.

### Work

- define insert behavior:
  - local writes set `CreatedAt` and `LastModifiedAt`
  - imports preserve source timestamps
- define update behavior:
  - local updates stamp `LastModifiedAt = SYSDATETIME()`
  - imports update `LastModifiedAt` to the imported value
- add DB triggers for normal update stamping if that remains the chosen approach
- ensure import paths can preserve historical timestamps without trigger interference

### Deliverable

- final timestamp strategy documented in code and implemented in SQL/repositories

### Recommendation

Use triggers for ordinary app updates, but use dedicated import SQL paths so imported timestamps are preserved intentionally.

---

## Task 5. Refactor data models to use `ConversationId`

### Objective

Replace numeric conversation IDs in the in-process model layer.

### Work

- update `ClaudeLog.Data/Models/Entry.cs`
- replace `long Id` with `Guid ConversationId`
- rename properties to use `ConversationId` explicitly where touched
- update any export/import contract models accordingly

### Deliverable

- all entry-related models use `Guid ConversationId`

---

## Task 6. Refactor repositories to use `ConversationId`

### Objective

Move database access from `Id`-based reads and writes to `ConversationId`.

### Work

Update:

- `ClaudeLog.Data/Repositories/EntryRepository.cs`
- `ClaudeLog.Data/Repositories/SessionRepository.cs`
- `ClaudeLog.Data/Repositories/DiagnosticsRepository.cs`

Required repository changes:

- `CreateAsync` generates and inserts `ConversationId`
- replace `OUTPUT INSERTED.Id` with `OUTPUT INSERTED.ConversationId`
- replace `WHERE Id = @Id` with `WHERE ConversationId = @ConversationId`
- replace `SELECT c.Id` with `SELECT c.ConversationId`
- update count logic from `COUNT(c.Id)` to `COUNT(*)` or `COUNT(c.ConversationId)`
- replace diagnostics repository methods to use `Guid? conversationId`

### Deliverable

- repositories compile and persist/read conversations by `ConversationId`

---

## Task 7. Refactor services to use `ConversationId`

### Objective

Update service contracts so all conversation operations use GUIDs.

### Work

Update:

- `ClaudeLog.Data/Services/ConversationService.cs`
- `ClaudeLog.Data/Services/DiagnosticsService.cs`

Required changes:

- change conversation-facing method signatures from `long` to `Guid`
- rename parameters from `id` to `conversationId`
- return `Guid` from create methods
- update diagnostics service to accept `Guid? conversationId`

### Deliverable

- service layer no longer exposes numeric conversation IDs

---

## Task 8. Refactor the web API and DTOs

### Objective

Update the server API to use `conversationId` in routes and payloads.

### Work

Update:

- `ClaudeLog.Web/Api/EntriesEndpoints.cs`
- `ClaudeLog.Web/Api/Dtos/EntryDtos.cs`
- `ClaudeLog.Web/Api/Dtos/ErrorDtos.cs`
- `ClaudeLog.Web/Api/ErrorsEndpoints.cs`

Required changes:

- change route parameters from `{id}` to `{conversationId}`
- change handler parameter types from `long` to `Guid`
- return `conversationId` in create/read payloads
- return diagnostics payloads keyed by `conversationId`

### Deliverable

- API no longer exposes numeric conversation IDs

### Breaking change note

Any existing callers using `/api/entries/{id}` or expecting numeric IDs must be updated in lockstep.

---

## Task 9. Refactor the front-end to use `conversationId`

### Objective

Remove numeric ID assumptions from the web UI.

### Work

Update:

- `ClaudeLog.Web/wwwroot/js/site.js`

Required changes:

- replace `entryId` state with `conversationId`
- replace any `Number(...)` conversion for conversation keys
- update DOM dataset names where necessary
- update fetch URLs to use `conversationId`
- verify selection, detail load, title edit, question edit, response edit, favorite toggle, delete toggle, and session move flows

### Deliverable

- front-end uses GUID-safe identifiers consistently

---

## Task 10. Refactor hooks and MCP outputs

### Objective

Update non-web callers and payloads to use GUID conversation identifiers.

### Work

Update:

- `ClaudeLog.Hook.Claude/Program.cs`
- `ClaudeLog.Hook.Codex/Program.cs`
- `ClaudeLog.Hook.Gemini/Program.cs`
- `ClaudeLog.MCP/LoggingTools.cs`

Required changes:

- `WriteEntryAsync` returns `Guid`
- logging statements refer to `ConversationId`
- MCP success payload returns `conversationId`

### Deliverable

- hooks and MCP no longer assume numeric entry IDs

---

## Task 11. Implement export

### Objective

Add file export for sessions and conversations.

### Work

- add export contract models
- add repository/service queries to load sessions and conversations for export
- build JSON payload using the agreed contract
- expose an API endpoint:
  - `GET /api/export/core`
- add UI affordance if the feature should be directly accessible from the web app

### Deliverable

- export endpoint returns a downloadable JSON file

---

## Task 12. Implement import

### Objective

Add file import with timestamp-based merge logic.

### Work

- parse and validate the JSON file
- validate `formatVersion`
- import sessions first
- import conversations second
- for sessions:
  - match on `SessionId`
  - insert when missing
  - skip update when the target session is soft-deleted
  - update only when imported `LastModifiedAt` is newer
- for conversations:
  - match on `ConversationId`
  - insert when missing
  - skip update when the target conversation is soft-deleted
  - update only when imported `LastModifiedAt` is newer
- return a summary including:
  - sessions inserted
  - sessions updated
  - sessions skipped
  - conversations inserted
  - conversations updated
  - conversations skipped
  - validation or import errors

Recommended endpoint:

- `POST /api/import/core`

### Deliverable

- import endpoint processes a file and returns a structured result summary

---

## Task 13. Update diagnostics references

### Objective

Ensure diagnostics still link back to conversations after the key migration.

### Work

- replace `ErrorLogs.EntryId` usage with `ConversationId`
- update diagnostics repository write and read logic
- update diagnostics DTOs and pages that display the reference
- confirm historical rows are migrated by SQL script

### Deliverable

- diagnostics references survive the conversation key replacement

---

## Task 14. Update documentation

### Objective

Document the new export/import functionality and the breaking identifier change.

### Work

- update `README.md` if export/import is user-facing
- document the JSON file purpose and usage
- document any API route changes if relevant
- document deployment caution for the destructive migration

### Deliverable

- repo docs reflect the shipped behavior

---

## Task 15. Execute rollout in a safe order

### Objective

Sequence work so schema and code stay compatible during implementation.

### Recommended order

1. finalize contract decisions
2. implement additive migration pieces first:
   - new columns
   - backfill
   - new constraints
3. update repositories and services to read/write `ConversationId`
4. update diagnostics to use `ConversationId`
5. update API DTOs and routes
6. update front-end JavaScript
7. update hooks and MCP
8. implement export
9. implement import
10. finalize migration by dropping:
    - `Conversations.Id`
    - `ErrorLogs.EntryId`

### Deliverable

- a coordinated rollout plan that avoids code depending on already-dropped columns

---

## Task 16. Validate the change before release

### Objective

Confirm the refactor is coherent across schema, API, UI, and import/export behavior.

### Validation checklist

- database migration succeeds on an existing database
- existing conversations are preserved and assigned `ConversationId`
- diagnostics references are preserved
- create entry returns `conversationId`
- get/update entry flows work with GUID route parameters
- UI actions work with GUID identifiers
- export file shape matches the contract
- import inserts missing rows
- import updates only when imported `LastModifiedAt` is newer
- import skips older or equal rows

### Deliverable

- manual verification completed by the user

---

## Summary

This is a medium-to-large coordinated refactor, not just an export/import feature.

The actual work naturally breaks into these streams:

1. schema and migration
2. conversation ID refactor through the application
3. export/import implementation
4. diagnostics migration
5. rollout and validation

The highest-risk part is not the JSON import/export logic. It is the replacement of the conversation primary key and every place in the application that currently assumes a numeric ID.
