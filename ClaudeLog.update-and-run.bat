@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM ============================================
REM ClaudeLog - Update and Run Script
REM ============================================
REM Stops running instances, builds solution, publishes all components, and starts web app
REM Can be run from anywhere
REM ============================================

set "SOURCE_DIR=C:\Users\jeffr\source\repos\ClaudeLog"
set "PUBLISH_ROOT=C:\Apps"
set "WEB_PORT=15088"

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
    pause
    exit /b 1
)

echo   Cleaning
dotnet clean ClaudeLog.sln --configuration Release --nologo --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Clean failed!
    pause
    exit /b 1
)

echo   Building
dotnet build ClaudeLog.sln --configuration Release --nologo --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
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
    --runtime win-x64 ^
    --self-contained false ^
    --nologo ^
    --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Web failed!
    pause
    exit /b 1
)

REM Claude Code Hook
echo   Publishing ClaudeLog.Hook.Claude
dotnet publish ClaudeLog.Hook.Claude\ClaudeLog.Hook.Claude.csproj ^
    -c Release ^
    -o "%PUBLISH_ROOT%\ClaudeLog.Hook.Claude" ^
    --runtime win-x64 ^
    --self-contained false ^
    --nologo ^
    --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Claude Hook failed!
    pause
    exit /b 1
)

REM Codex Hook
echo   Publishing ClaudeLog.Hook.Codex
dotnet publish ClaudeLog.Hook.Codex\ClaudeLog.Hook.Codex.csproj ^
    -c Release ^
    -o "%PUBLISH_ROOT%\ClaudeLog.Hook.Codex" ^
    --runtime win-x64 ^
    --self-contained false ^
    --nologo ^
    --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Codex Hook failed!
    pause
    exit /b 1
)

REM MCP Server
echo   Publishing ClaudeLog.MCP
dotnet publish ClaudeLog.MCP\ClaudeLog.MCP.csproj ^
    -c Release ^
    -o "%PUBLISH_ROOT%\ClaudeLog.MCP" ^
    --runtime win-x64 ^
    --self-contained false ^
    --nologo ^
    --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish MCP failed!
    pause
    exit /b 1
)

echo   All components published successfully.
echo.

REM ============================================
REM Step 4: Start web app
REM ============================================
echo ============================================
echo Step 4: Starting ClaudeLog.Web
echo ============================================
echo.
echo Access at: http://localhost:%WEB_PORT%
echo Press Ctrl+C to stop the application
echo.

cd "%PUBLISH_ROOT%\ClaudeLog.Web"
set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://localhost:%WEB_PORT%
ClaudeLog.Web.exe

endlocal
