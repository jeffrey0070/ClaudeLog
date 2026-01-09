@echo off
setlocal

echo ============================================
echo ClaudeLog - Set Connection String
echo ============================================
echo.
echo This script sets the CLAUDELOG_CONNECTION_STRING environment
echo variable at the MACHINE level so all ClaudeLog components
echo (Web, Hooks, MCP, services) use the same database.
echo NOTE: You must run this script from an elevated
echo       "Run as administrator" command prompt.
echo example: Server=localhost;Database=ClaudeLog;User Id=myUsername;Password=myPassword;TrustServerCertificate=true;
echo example: Server=localhost;Database=ClaudeLog;Integrated Security=true;TrustServerCertificate=true;
echo.

REM Require admin for machine-level env vars
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click this file and choose "Run as administrator".
    pause
    exit /b 1
)
echo Current CLAUDELOG_CONNECTION_STRING (process view):
if defined CLAUDELOG_CONNECTION_STRING (
    echo   %CLAUDELOG_CONNECTION_STRING%
) else (
    echo   (not set)
)
echo.

set "connString="
set /p connString=Enter SQL Server connection string for ClaudeLog: 

if "%connString%"=="" (
    echo.
    echo No connection string entered. Aborting.
    goto End
)

echo.
echo Setting system-wide CLAUDELOG_CONNECTION_STRING...
setx CLAUDELOG_CONNECTION_STRING "%connString%" /M >nul

echo.
echo Done.
echo NOTE: You must open a NEW command prompt or restart any running shells/IDEs/services for the new value to take effect.

:End
echo.
echo Press Enter to exit...
pause
