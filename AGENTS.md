# Repository Guidelines

## Project Structure & Module Organization
- `ClaudeLog.Web/` - ASP.NET Core web UI (Razor Pages + Minimal APIs)
- `ClaudeLog.Data/` - Data layer (ADO.NET, models, repositories, migrations in `Scripts/`)
- `ClaudeLog.Hook.Claude/` - Claude Code stop hook (console)
- `ClaudeLog.Hook.Codex/` - Codex hook (stdin/watcher modes)
- `ClaudeLog.Hook.Gemini/` - Gemini CLI hook (console)
- `ClaudeLog.MCP/` - MCP server (STDIO)
- Root docs/scripts: `README.md`, `ClaudeLog.update-and-run.bat`, `ClaudeLog.bat`, `ClaudeLog.install-or-update-scheduled-task.ps1`, `set-connection-string.bat`

## Build, Test, and Development Commands
- Restore/build solution: `dotnet restore` then `dotnet build ClaudeLog.sln -c Release`
- Run Web (dev): `dotnet run --project ClaudeLog.Web --urls http://localhost:15089`
- Publish + restart scheduled task host: `ClaudeLog.update-and-run.bat`
- Run published Web: `ClaudeLog.bat`
- Install/update current-user logon task: `.\ClaudeLog.install-or-update-scheduled-task.ps1`
- Connection string: configure via `CLAUDELOG_CONNECTION_STRING`. Use `set-connection-string.bat`.
- `README.md` is the single source of truth for setup, deployment, hosting, and CLI configuration details.

## Coding Style & Naming Conventions
- C# (net9.0), `Nullable` and `ImplicitUsings` enabled. Use 4-space indentation.
- File-scoped namespaces; PascalCase for types/methods; camelCase for locals/parameters; suffix async methods with `Async`.
- Prefer DI for services; avoid static mutable state. Validate inputs and return early.
- Keep endpoints minimal and push logic to services/repositories.

## Testing Guidelines
- No unit test project yet. Validate changes by:
  - `dotnet build` and run Web, then visit `http://localhost:15089` (dev) or `http://localhost:15088` (published).
  - Confirm automatic DB init/migrations and API endpoints (`/api/sessions`, `/api/entries`, `/api/errors`).
- If adding tests, use xUnit and name projects `ClaudeLog.*.Tests`; mirror namespaces of code under test.

## Commit & Pull Request Guidelines
- Commits: concise, imperative summary (max ~72 chars). Include scope if helpful (e.g., `web:`, `data:`). Reference issues with `#123`.
- PRs: include what/why, test steps, and screenshots for UI changes. Note any DB migration (see below). Keep PRs focused by project.

## Security & Configuration Tips
- Never commit secrets. Prefer `CLAUDELOG_CONNECTION_STRING` for local overrides.
- Ports: Production `15088` (see `ClaudeLog.Web/Program.cs`, `ClaudeLog.bat`, and `ClaudeLog.install-or-update-scheduled-task.ps1`). Update docs if you change ports.
- DB migrations: add `ClaudeLog.Data/Scripts/X.Y.Z.sql`, rebuild, and on next run Web will apply it automatically.

