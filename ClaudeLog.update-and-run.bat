@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM ============================================
REM ClaudeLog - Update and Run Script
REM ============================================
REM Stops running instances, builds solution, publishes all components, and starts web app
REM Can be run from anywhere
REM ============================================

REM Require admin for machine-level env vars and C:\Apps
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click this file and choose "Run as administrator".
    goto End
)

set "SOURCE_DIR=%~dp0"
set "PUBLISH_ROOT=C:\Apps"
set "WEB_PORT=15088"
set "TASK_NAME=ClaudeLog.Web"
set "TASK_EXISTS="

if not exist "%SOURCE_DIR%ClaudeLog.sln" (
    echo ERROR: Cannot find ClaudeLog.sln in %SOURCE_DIR%
    goto End
)

echo.
echo ============================================
echo ClaudeLog - Update and Run
echo ============================================
echo.

REM ============================================
REM Step 1: Stop running instances
REM ============================================
echo Step 1: Stopping running instances
echo.

REM Stop scheduled task host first
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Get-ScheduledTask -TaskName '%TASK_NAME%' -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    set "TASK_EXISTS=1"
    echo   Ending scheduled task %TASK_NAME%
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Stop-ScheduledTask -TaskName '%TASK_NAME%' -ErrorAction SilentlyContinue" >nul 2>&1
    timeout /t 1 /nobreak >nul
) else (
    echo   Scheduled task %TASK_NAME% not found. Skipping task stop.
)

REM Stop web app by port
set "PID="
for /f "tokens=5" %%a in ('netstat -aon ^| findstr /r ":%WEB_PORT%[ \t].*LISTENING"') do (
    set "PID=%%a"
)

if defined PID (
    echo   Stopping web app on port %WEB_PORT% (PID: !PID!)
    taskkill /F /PID !PID! >nul 2>&1
    timeout /t 1 /nobreak >nul
) else (
    echo   No web app found on port %WEB_PORT%.
)

REM Kill all ClaudeLog processes by name (Web, Hooks, MCP)
echo   Stopping all ClaudeLog processes
taskkill /F /IM ClaudeLog.Web.exe >nul 2>&1
taskkill /F /IM ClaudeLog.Hook.Claude.exe >nul 2>&1
taskkill /F /IM ClaudeLog.Hook.Codex.exe >nul 2>&1
taskkill /F /IM ClaudeLog.Hook.Gemini.exe >nul 2>&1
taskkill /F /IM ClaudeLog.MCP.exe >nul 2>&1

REM Wait for file handles to release
timeout /t 1 /nobreak >nul

echo   Done.
echo.

REM ============================================
REM Step 2: Build solution
REM ============================================
echo ============================================
echo Step 2: Building solution
echo ============================================
echo.

cd /d "%SOURCE_DIR%"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Cannot find source directory!
    echo Expected: %SOURCE_DIR%
    goto End
)

echo   Cleaning
dotnet clean ClaudeLog.sln --configuration Release --nologo --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Clean failed!
    goto End
)

echo   Building
dotnet build ClaudeLog.sln --configuration Release --nologo --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed!
    goto End
)

echo   Build completed successfully.
echo.

REM ============================================
REM Step 3: Publish all components
REM ============================================
echo ============================================
echo Step 3: Publishing components
echo ============================================
echo.

REM Web App
echo   Publishing ClaudeLog.Web
dotnet publish ClaudeLog.Web\ClaudeLog.Web.csproj ^
    -c Release ^
    -o "%PUBLISH_ROOT%\ClaudeLog.Web" ^
    --nologo ^
    --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Web failed!
    goto End
)

REM Claude Code Hook
echo   Publishing ClaudeLog.Hook.Claude
dotnet publish ClaudeLog.Hook.Claude\ClaudeLog.Hook.Claude.csproj ^
    -c Release ^
    -o "%PUBLISH_ROOT%\ClaudeLog.Hook.Claude" ^
    --nologo ^
    --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Claude Hook failed!
    goto End
)

REM Codex Hook
echo   Publishing ClaudeLog.Hook.Codex
dotnet publish ClaudeLog.Hook.Codex\ClaudeLog.Hook.Codex.csproj ^
    -c Release ^
    -o "%PUBLISH_ROOT%\ClaudeLog.Hook.Codex" ^
    --nologo ^
    --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Codex Hook failed!
    goto End
)

REM Gemini Hook
echo   Publishing ClaudeLog.Hook.Gemini
dotnet publish ClaudeLog.Hook.Gemini\ClaudeLog.Hook.Gemini.csproj ^
    -c Release ^
    -o "%PUBLISH_ROOT%\ClaudeLog.Hook.Gemini" ^
    --nologo ^
    --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Gemini Hook failed!
    goto End
)

REM MCP Server
echo   Publishing ClaudeLog.MCP
dotnet publish ClaudeLog.MCP\ClaudeLog.MCP.csproj ^
    -c Release ^
    -o "%PUBLISH_ROOT%\ClaudeLog.MCP" ^
    --nologo ^
    --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish MCP failed!
    goto End
)

echo   All components published successfully.
echo.

REM Copy helper scripts to C:\Apps
echo   Copying helper scripts to %PUBLISH_ROOT%
copy /Y "%SOURCE_DIR%ClaudeLog.bat" "%PUBLISH_ROOT%\ClaudeLog.bat" >nul
if %ERRORLEVEL% NEQ 0 (echo WARNING: Failed to copy ClaudeLog.bat) else (echo   ClaudeLog.bat copied successfully.)
copy /Y "%SOURCE_DIR%ClaudeLog.install-or-update-scheduled-task.ps1" "%PUBLISH_ROOT%\ClaudeLog.install-or-update-scheduled-task.ps1" >nul
if %ERRORLEVEL% NEQ 0 (echo WARNING: Failed to copy ClaudeLog.install-or-update-scheduled-task.ps1) else (echo   ClaudeLog.install-or-update-scheduled-task.ps1 copied successfully.)
copy /Y "%SOURCE_DIR%set-connection-string.bat" "%PUBLISH_ROOT%\set-connection-string.bat" >nul
if %ERRORLEVEL% NEQ 0 (echo WARNING: Failed to copy set-connection-string.bat) else (echo   set-connection-string.bat copied successfully.)
echo.

REM ============================================
REM Step 4: Start web app
REM ============================================
echo ============================================
echo Step 4: Starting ClaudeLog.Web
echo ============================================
echo.
echo Access at: http://localhost:%WEB_PORT%
echo.

if defined TASK_EXISTS (
    echo   Starting scheduled task %TASK_NAME%
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-ScheduledTask -TaskName '%TASK_NAME%'" >nul 2>&1
    if !ERRORLEVEL! NEQ 0 (
        echo WARNING: Failed to start scheduled task %TASK_NAME%.
        echo Verify the task action runs %PUBLISH_ROOT%\ClaudeLog.Web\ClaudeLog.Web.exe directly.
    ) else (
        echo   Scheduled task started.
    )
) else (
    echo   Scheduled task %TASK_NAME% not found. Skipping host start.
)

:End
echo.
set /p "_pause=Press Enter to exit..."
endlocal

