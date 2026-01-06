@echo off
REM ============================================
REM ClaudeLog - Quick Start Script
REM ============================================
REM Stops any running instance and starts ClaudeLog.Web
REM Can be run from anywhere
REM ============================================

echo.
echo ============================================
echo ClaudeLog - Quick Start
echo ============================================
echo.

REM Stop any running instances
echo Checking for running ClaudeLog.Web instances
echo.

REM Find PID using port 15088
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :15088 ^| findstr LISTENING') do (
    set PID=%%a
)

if defined PID (
    echo Found process on port 15088 (PID: %PID%)
    echo Stopping process
    taskkill /F /PID %PID% >nul 2>&1
    timeout /t 1 /nobreak >nul
    echo Process stopped.
) else (
    echo No process found on port 15088.
)

REM Also kill by process name as backup
taskkill /F /IM ClaudeLog.Web.exe >nul 2>&1

echo.
echo ============================================
echo Starting ClaudeLog.Web
echo ============================================
echo.
echo Access at: http://localhost:15088
echo Press Ctrl+C to stop the application
echo.

cd "C:\Apps\ClaudeLog.Web"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: ClaudeLog.Web not found at C:\Apps\ClaudeLog.Web
    echo Please run ClaudeLog.update-and-run.bat first to publish the app
    pause
    exit /b 1
)

set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://localhost:15088
ClaudeLog.Web.exe

echo.
echo Press Enter to exit...
pause
