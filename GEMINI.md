# Gemini Code Assistant Context

This document provides context for the Gemini Code Assistant to understand the ClaudeLog project.

## Project Overview

ClaudeLog is a .NET-based solution for automatically logging conversations from the Claude Code, Codex, and Gemini CLIs. It stores the conversations in a SQL Server database and provides a web-based interface for browsing, searching, and managing the logged entries.

The solution consists of several projects:

*   **`ClaudeLog.Web`**: An ASP.NET Core web application that serves as the user interface. It uses Razor Pages for server-side rendering and Minimal APIs for its web services.
*   **`ClaudeLog.Data`**: A data access layer that handles all interactions with the SQL Server database. It uses raw ADO.NET for data access and includes a mechanism for automatic database schema creation and migration.
*   **`ClaudeLog.Hook.Claude`**: A console application that integrates with the Claude Code CLI as a "hook" to capture conversation data.
*   **`ClaudeLog.Hook.Codex`**: A similar console application that acts as a hook for the Codex CLI.
*   **`ClaudeLog.Hook.Gemini`**: A console application that integrates with the Gemini CLI as a "hook" to capture conversation data.
*   **`ClaudeLog.MCP`**: A Model Context Protocol (MCP) server that provides an alternative, more interactive way to log conversations from supported CLIs.

## Building and Running

The primary method for building and running the project is via the provided batch scripts.

*   **To build, publish, and run for the first time:**
    ```bash
    ClaudeLog.update-and-run.bat
    ```
    This script handles the entire process, including publishing the applications to `C:\Apps\ClaudeLog.*`.

*   **To run the application after it has been built:**
    ```bash
    ClaudeLog.bat
    ```

### Database Setup

The database connection is configured using the `CLAUDELOG_CONNECTION_STRING` environment variable. The `set-connection-string.bat` script is provided to help set this system-wide.

The database schema is managed automatically. When the web application starts, it checks if the database and tables exist and runs any necessary migration scripts from the `ClaudeLog.Data/Scripts` directory.

### Accessing the Web UI

The web interface is available at `http://localhost:15088` by default when running in production mode (via the batch scripts).

## Development Conventions

*   **Technology Stack**: The project is built using .NET and C#. The web application uses ASP.NET Core.
*   **Data Access**: Data access is performed using raw ADO.NET with `Microsoft.Data.SqlClient`. There is no ORM like Entity Framework in use.
*   **API Style**: The web application uses ASP.NET Core Minimal APIs for its web services.
*   **Configuration**: Configuration, especially the database connection string, is primarily handled through environment variables.
*   **Integration**: The core logic for capturing conversations is implemented in separate "hook" applications that are invoked by the target CLIs. This keeps the logging mechanism decoupled from the main web application.
*   **Debugging**: Debugging for the hook applications is facilitated by environment variables (`CLAUDELOG_DEBUG`, `CLAUDELOG_WAIT_FOR_DEBUGGER`) which enable logging and allow attaching a debugger.
