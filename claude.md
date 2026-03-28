# Instructions for Claude

## Read This First

When you start a new session in this project, read `CONTEXT.md` first to understand the project state.

## Database Access

Direct database access is allowed. Connection string is in the machine environment variable `CLAUDELOG_CONNECTION_STRING`. Use `sqlcmd -S localhost -d ClaudeLog -E` to query.

## Build/Test/Deploy

After making any code changes, always run `ClaudeLog.update-and-run.bat` as Administrator to deploy.

## Documentation Preferences

This project follows a single source of truth principle:

1. `claude.md` — Instructions for Claude
2. `CONTEXT.md` — Current project state
3. `README.md` — Setup and usage guide
4. `PROJECT_PLAN.md` — Requirements and roadmap

