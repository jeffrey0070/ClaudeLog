@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM ============================================
REM ClaudeLog - Update and Run Script
REM ============================================
REM Stops app on port 15088, builds, publishes Web + Codex Hook, and starts Web
REM Can be run from anywhere
REM ============================================

echo.
echo ============================================
echo ClaudeLog - Update and Run
echo ============================================
echo.

REM Step 1: Kill Web on port 15088 and any running ClaudeLog.Web.exe
echo Step 1: Checking for processes on port 15088...
echo.

set "PID="
for /f "tokens=5" %%a in ('netstat -aon ^| findstr /r ":15088[ \t].*LISTENING"') do (
    set "PID=%%a"
)

if defined PID (
    echo Found process using port 15088 (PID: !PID!)
    echo Killing process...
    taskkill /F /PID !PID! 2>nul
    timeout /t 2 /nobreak >nul
) else (
    echo No process found on port 15088.
)

REM Kill any running ClaudeLog.Web.exe regardless of port
tasklist /fi "imagename eq ClaudeLog.Web.exe" | findstr /i "ClaudeLog.Web.exe" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo Killing all ClaudeLog.Web.exe processes...
    taskkill /F /IM ClaudeLog.Web.exe 2>nul
    timeout /t 2 /nobreak >nul
) else (
    echo No ClaudeLog.Web.exe processes running.
)

echo.
echo ============================================
echo Step 2: Cleaning and building solution...
echo ============================================
echo.

cd /d "C:\Users\jeffr\source\repos\ClaudeLog"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Cannot find source directory!
    echo Expected: C:\Users\jeffr\source\repos\ClaudeLog
    pause
    exit /b 1
)

dotnet clean ClaudeLog.sln --configuration Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Clean failed!
    pause
    exit /b 1
)

dotnet build ClaudeLog.sln --configuration Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Step 3: Publishing Web to C:\Apps\ClaudeLog.Web...
echo ============================================
echo.

dotnet publish ClaudeLog.Web\ClaudeLog.Web.csproj -c Release -o "C:\Apps\ClaudeLog.Web" --runtime win-x64 --self-contained false
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Web failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Step 4: Publishing Codex Hook to C:\Apps\ClaudeLog.Hook.Codex...
echo ============================================
echo.

dotnet publish ClaudeLog.Hook.Codex\ClaudeLog.Hook.Codex.csproj -c Release -o "C:\Apps\ClaudeLog.Hook.Codex" --runtime win-x64 --self-contained false
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Codex Hook failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Step 4ex0: Publishing Claude Hook to C:\Apps\ClaudeLog.Hook.Claude...
echo ============================================
echo.

dotnet publish ClaudeLog.Hook.Claude\ClaudeLog.Hook.Claude.csproj -c Release -o "C:\Apps\ClaudeLog.Hook.Claude" --runtime win-x64 --self-contained false
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish Claude Hook failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Step 4ex: Publishing Claude.MCP to C:\Apps\ClaudeLog.MCP...
echo ============================================
echo.

dotnet publish ClaudeLog.MCP\ClaudeLog.MCP.csproj -c Release -o "C:\Apps\ClaudeLog.MCP" --runtime win-x64 --self-contained false
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish ClaudeLog.MCP failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Step 5: Starting ClaudeLog.Web...
echo ============================================
echo.
echo Access at: http://localhost:15088
echo Press Ctrl+C to stop the application
echo.

cd "C:\Apps\ClaudeLog.Web"
set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://localhost:15088
ClaudeLog.Web.exe
endlocal
