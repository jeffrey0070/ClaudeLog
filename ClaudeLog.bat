@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM ============================================
REM ClaudeLog - Quick Start Script
REM ============================================
REM Stops any running published instance and starts ClaudeLog.Web
REM Can be run from anywhere
REM ============================================

set "APP_ROOT=C:\Apps\ClaudeLog.Web"
set "APP_EXE=ClaudeLog.Web.exe"
set "WEB_PORT=15088"
set "PID="

echo.
echo ============================================
echo ClaudeLog - Quick Start
echo ============================================
echo.

if not exist "%APP_ROOT%\%APP_EXE%" (
    echo ERROR: Published app not found at %APP_ROOT%\%APP_EXE%
    echo Run ClaudeLog.update-and-run.bat first.
    goto End
)

REM Stop any running instances
echo Checking for running ClaudeLog.Web instances
echo.

REM Find PID using port 15088
for /f "tokens=5" %%a in ('netstat -aon ^| findstr /r ":%WEB_PORT%[ \t].*LISTENING"') do (
    set "PID=%%a"
)

if defined PID (
    echo Found process on port %WEB_PORT% (PID: !PID!)
    echo Stopping process
    taskkill /F /PID !PID! >nul 2>&1
    timeout /t 1 /nobreak >nul
    echo Process stopped.
) else (
    echo No process found on port %WEB_PORT%.
)

REM Also kill by process name as backup
taskkill /F /IM %APP_EXE% >nul 2>&1

echo.
echo ============================================
echo Starting ClaudeLog.Web
echo ============================================
echo.
echo Access at: http://localhost:%WEB_PORT%
echo Press Ctrl+C to stop the application
echo.

cd /d "%APP_ROOT%"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Cannot change directory to %APP_ROOT%
    goto End
)

set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://0.0.0.0:%WEB_PORT%
%APP_EXE%

:End
echo.
set /p "_pause=Press Enter to exit..."
endlocal
