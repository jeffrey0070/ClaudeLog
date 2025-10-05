@echo off
REM ============================================
REM ClaudeLog - Quick Start Script
REM ============================================
REM Stops app on port 15088 if running and starts it
REM Can be run from anywhere
REM ============================================

echo.
echo ============================================
echo ClaudeLog - Quick Start
echo ============================================
echo.

REM Step 1: Find and kill process on port 15088
echo Checking for processes on port 15088...
echo.

REM Find PID using port 15088
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :15088 ^| findstr LISTENING') do (
    set PID=%%a
)

if defined PID (
    echo Found process using port 15088 (PID: %PID%)
    echo Stopping process...
    taskkill /F /PID %PID% 2>nul
    if %ERRORLEVEL% EQU 0 (
        echo Process stopped successfully.
    ) else (
        echo Warning: Could not stop process. Continuing anyway...
    )
    timeout /t 2 /nobreak >nul
) else (
    echo No process found on port 15088. Continuing...
)

echo.
echo ============================================
echo Starting ClaudeLog.Web...
echo ============================================
echo.
echo Access at: http://localhost:15088
echo Press Ctrl+C to stop the application
echo.

cd "C:\Apps\ClaudeLog.Web"
ClaudeLog.Web.exe
