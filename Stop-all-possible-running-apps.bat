@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM ============================================
REM ClaudeLog - Stop All Possible Running Apps
REM ============================================

set "TASK_NAME=ClaudeLog.Web"
set "WEB_PORT=15088"
set "FOUND_ANYTHING="

echo.
echo ============================================
echo ClaudeLog - Stop All Possible Running Apps
echo ============================================
echo.

REM Stop the scheduled task if it exists.
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Get-ScheduledTask -TaskName '%TASK_NAME%' -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    echo Stopping scheduled task %TASK_NAME%
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Stop-ScheduledTask -TaskName '%TASK_NAME%' -ErrorAction SilentlyContinue" >nul 2>&1
    set "FOUND_ANYTHING=1"
    timeout /t 1 /nobreak >nul
) else (
    echo Scheduled task %TASK_NAME% not found.
)

REM Stop anything still listening on the web port.
set "PID="
for /f "tokens=5" %%a in ('netstat -aon ^| findstr /r ":%WEB_PORT%[ \t].*LISTENING"') do (
    set "PID=%%a"
)

if defined PID (
    echo Stopping process on port %WEB_PORT% ^(PID: !PID!^)
    taskkill /F /PID !PID! >nul 2>&1
    set "FOUND_ANYTHING=1"
    timeout /t 1 /nobreak >nul
) else (
    echo No listening process found on port %WEB_PORT%.
)

REM Kill all known ClaudeLog executables in case any are detached or stale.
call :KillProcess "ClaudeLog.Web.exe"
call :KillProcess "ClaudeLog.Hook.Claude.exe"
call :KillProcess "ClaudeLog.Hook.Codex.exe"
call :KillProcess "ClaudeLog.Hook.Gemini.exe"
call :KillProcess "ClaudeLog.MCP.exe"

echo.
if defined FOUND_ANYTHING (
    echo ClaudeLog stop pass completed.
) else (
    echo No running ClaudeLog processes were found.
)

echo.
set /p "_pause=Press Enter to exit..."
endlocal
goto :eof

:KillProcess
tasklist /FI "IMAGENAME eq %~1" | find /I "%~1" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    echo Stopping %~1
    taskkill /F /IM "%~1" >nul 2>&1
    set "FOUND_ANYTHING=1"
) else (
    echo %~1 not running.
)
goto :eof
