@echo off
setlocal

echo ============================================
echo ClaudeLog - Set Connection String
echo ============================================
echo.
echo This script sets the CLAUDELOG_CONNECTION_STRING environment
echo variable for your user account so all ClaudeLog components
echo (Web, Hooks, MCP) use the same database.
echo example: Server=localhost;Database=ClaudeLog;User Id=myUsername;Password=myPassword;TrustServerCertificate=true;
echo example: Server=localhost;Database=ClaudeLog;Integrated Security=true;TrustServerCertificate=true;
echo.
echo Current CLAUDELOG_CONNECTION_STRING:
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
echo Setting CLAUDELOG_CONNECTION_STRING for current user...
setx CLAUDELOG_CONNECTION_STRING "%connString%" >nul

echo.
echo Done.
echo NOTE: You must open a NEW command prompt or restart any running shells/IDEs for the new value to take effect.

:End
echo.
echo Press Enter to exit...
pause
