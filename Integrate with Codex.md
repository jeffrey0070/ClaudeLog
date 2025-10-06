# Integrate with Codex â€” Transcript Hook Plan

## Objective
- Add a new console app `ClaudeLog.Hook.Codex` that reads Codexâ€™s local transcript (JSON/JSONL), extracts the latest user-to-assistant turn, and posts to ClaudeLogâ€™s API in real time.
- Tool-agnostic: store `Tool = "Codex"` in `Sections`/`Conversations` without schema changes.

## Scope
- Per-turn logging (no need to wait for section end).
- Local-only integration; no external webhooks required (optional future).
- Resilient to unknown transcript variants using a tolerant parser and checkpointing to avoid duplicates.

## Assumptions and Prerequisites
- Codex writes a local transcript file per session (JSONL preferred; JSON array fallback).
- Hook can be triggered per turn with `{ session_id, transcript_path }` on stdin (preferred, mirrors Claude). If not available, we can poll or watch the file.
- .NET 9 SDK installed; ClaudeLog web app reachable on `http://localhost:15088`.

## High-Level Design
- New project: `ClaudeLog.Hook.Codex` (console app)
  - Input: Read stdin JSON with fields `session_id`, `transcript_path`, `hook_event_name` (fallback: CLI args or env vars).
  - Transcript reader: Open file with read sharing, detect last completed pair (user then assistant), skip partial deltas.
  - De-duplication: Maintain per-file checkpoint (size and SHA-256 hash of `question + "\n" + response`).
  - Posting: Ensure section via `POST /api/sections { tool: "Codex", sectionId }`; create entry via `POST /api/entries`.
  - Error resilience: Best-effort error logging to `POST /api/errors` without breaking Codex flow (always print `{}` to stdout).

## Project Structure

```
ClaudeLog.Hook.Codex/
  Program.cs              # Main entry
  TranscriptParser.cs     # JSON/JSONL tolerant parsing
  Checkpoint.cs           # State persistence (per transcript)
  CodexApiClient.cs       # HTTP wrapper for sections/entries/errors
  Models/
    HookInput.cs          # stdin schema
    TranscriptMessage.cs  # message shapes (user/assistant, content blocks)
```

## Data Mapping
- SectionId: Use Codex session or run ID (from stdin) for `dbo.Sections.SectionId`.
- Conversations: Server computes `Title`; send trimmed `Question` and `Response`. Link by `SectionId`; `Tool='Codex'` inferred from section.

## API Endpoints (existing)
- `POST /api/sections` â†’ returns `{ sectionId }` (ClaudeLog.Web/Api/SectionsEndpoints.cs)
- `POST /api/entries` â†’ returns `{ id }` (ClaudeLog.Web/Api/EntriesEndpoints.cs)
- `POST /api/errors` â†’ returns `{ ok, id }` (ClaudeLog.Web/Api/ErrorsEndpoints.cs)

## Parsing Strategy (Tolerant)
- Supported message shapes (auto-detected):
  - Claude-like JSONL: `{ "type": "user|assistant", "message": { "content": [...] } }`
  - Legacy role schema: `{ "role": "user|assistant", "content": "..." }`
  - Content array blocks: `[{ "type": "text", "text": "..." }, ...]` â†’ join with newlines.
  - Plain string `content`.
- Algorithm:
  1) Read only appended content using checkpoint (seek to last offset if present).
  2) Parse valid JSON objects; skip malformed lines (log and continue).
  3) Walk backward: pick last assistant message with text, then the nearest preceding user message.
  4) If both exist and differ from last hash, post entry; otherwise skip.

## Checkpointing and Idempotency
- State file path: `%LOCALAPPDATA%/ClaudeLog/codex_state.json` (Windows). One record per `transcript_path`.
- Fields per path: `lastSize`, `lastHash`, `lastEntryAt`.
- Skip if unchanged and hash matches; tolerate growth with the same pair (retries) without duplicating.

## UI Integration
- Tool badge/label: display `Tool` (e.g., â€œCodexâ€, â€œClaudeâ€) in the list and/or detail view.
- Filter by tool: optional quick filter in the left panel to view a single toolâ€™s entries.
- No backend changes required (DTOs already include `Tool`).

## Triggering and Timing
- Preferred: Codex calls the hook on turn stop with stdin JSON.
- Fallback: `FileSystemWatcher` or 500 ms polling on the transcript file.
- Add 200â€“400 ms debounce after last write to avoid reading mid-write.

## Configuration
- `CLAUDELOG_API_BASE` env var (default `http://localhost:15088/api`).
- `CODEX_TRANSCRIPT_PATH` env var as fallback if stdin is not provided.
- Optional `CLAUDELOG_HOOK_LOGLEVEL` for verbose troubleshooting.

## Performance
- Incremental reads keep parsing under ~10 ms per turn on typical transcripts.
- Local HTTP + single INSERT via ADO.NET typically ~5â€“15 ms.
- End-to-end ~10â€“50 ms per turn on a developer machine; entry is immediately retrievable.

## Performance Targets

| Operation                 | Target        |
|---------------------------|---------------|
| Incremental read + parse  | < 10 ms       |
| API POST (localhost)      | < 15 ms       |
| End-to-end per turn       | < 50 ms       |
| UI availability           | Immediate     |

## Error Handling
- Network or DB errors: swallow (best-effort) and log via `/api/errors` when possible.
- Corrupt JSON lines: skip and continue; log a concise parsing error.
- Always output `{}` so Codex execution continues unaffected.

## Risk Mitigation

| Risk                         | Mitigation                                                    |
|------------------------------|---------------------------------------------------------------|
| Unknown transcript format    | Tolerant parser; validate with real samples; feature flags    |
| Duplicate entries            | Hash-based checkpointing; idempotent posting                 |
| File locking/partial writes  | Read-share open; debounce 200â€“400 ms; retry on lock          |
| Hook/network failures        | Best-effort logging; swallow exceptions; never block Codex    |
| Large transcripts            | Incremental reads; backward scan; bounded memory use         |

## Testing and Validation
- Parsing tests with sample files:
  - JSONL with content blocks; legacy role schema; plain string; malformed line.
  - Assistant-only last record (no user) â†’ use last completed pair.
  - Duplicate last pair â†’ de-dup works (no new row).
- Integration checks:
  - Pipe a sample stdin JSON to the hook â†’ verify a row appears via UI and `GET /api/entries`.
  - Rapid successive turns â†’ verify no lag and no duplicates.

## Deployment Options

- Startup app: launch the hook (watcher mode) on user login; runs in background.
- Windows service: install via NSSM or service wrapper for always-on watching.
- Manual trigger: run the hook for a specific session or transcript (useful for tests).

## Rollout Plan and Timeline
- Day 1: Confirm transcript sample and invocation method; scaffold project and configs.
- Day 2: Implement tolerant parser, checkpointing, and API posting; add error reporting.
- Day 3: Validate with real transcripts; publish to `C:/Apps/ClaudeLog.Hook.Codex`; add minimal README setup notes.

### Day-by-Day Checklist
- Day 1
  - [ ] Locate transcript files and collect a redacted 10â€“30 line sample
  - [ ] Confirm invocation method (stdin vs watcher)
  - [ ] Scaffold project and basic config handling
- Day 2
  - [ ] Implement tolerant parser and checkpointing
  - [ ] Wire API client for sections, entries, errors
  - [ ] Handle edge cases (partial data, malformed JSON)
- Day 3
  - [ ] Validate de-dup on real transcripts
  - [ ] Performance test and publish
  - [ ] Add README/CLAUDE.md setup notes (non-duplicative per doc policy)

## Open Questions
- Exact transcript location(s) and a short redacted sample (last 10â€“30 lines).
- Can Codex invoke a command on turn stop with `{ session_id, transcript_path }`? If not, we will use watcher/polling.
- Any extra metadata to capture now (e.g., files changed, tool calls). We can temporarily store JSON in `ErrorLogs.Detail`.

## Acceptance Criteria
- Each completed assistant turn creates one new Conversation under the correct `SectionId` with `Tool='Codex'`.
- No duplicate entries on retries or replays (hash-based de-duplication).
- Hook failures do not block Codex and are visible in `dbo.ErrorLogs`.

### Non-Functional
- Parsing completes in < 10 ms per turn; end-to-end latency < 50 ms.
- Handles malformed JSON gracefully; works with large transcripts (10k+ lines).

### Integration
- Web UI shows Codex conversations with a tool badge/label.
- Search works across Claude and Codex entries; optional tool filter functions.

## Practical Details (Resolved)
- Transcript root (found locally): `C:\Users\jeffr\.codex\sessions` (JSONL files named `rollout-<timestamp>-<GUID>.jsonl`).
- SessionId derivation: the hook extracts the trailing GUID from filename or the first `session_meta.payload.id`; otherwise, it hashes the file path to a deterministic GUID.
- Recommended mode: watcher. Start with: `C:\Apps\ClaudeLog.Hook.Codex\ClaudeLog.Hook.Codex.exe --watch "%USERPROFILE%\.codex\sessions"`. Entries appear immediately after each assistant turn.
- Stdin mode remains available for future Codex per-turn hooks.

