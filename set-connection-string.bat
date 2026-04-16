@echo off
setlocal EnableExtensions

set "SCOPE_LABEL=MACHINE"
set "SETX_ARGS=/M"

if /I "%~1"=="user" (
    set "SCOPE_LABEL=USER"
    set "SETX_ARGS="
)

echo ============================================
echo ClaudeLog - Set Connection String
echo ============================================
echo.
echo This script sets the CLAUDELOG_CONNECTION_STRING environment
echo variable at the %SCOPE_LABEL% level.
if /I "%SCOPE_LABEL%"=="MACHINE" (
    echo Machine scope is recommended when hooks may run in other user contexts.
    echo NOTE: You must run this script from an elevated
    echo       "Run as administrator" command prompt.
) else (
    echo User scope is enough for local development and current-user scheduled tasks.
)
echo example: Server=localhost;Database=ClaudeLog;User Id=myUsername;Password=myPassword;TrustServerCertificate=true;
echo example: Server=localhost;Database=ClaudeLog;Integrated Security=true;TrustServerCertificate=true;
echo.

REM Require admin for machine-level env vars only
if /I "%SCOPE_LABEL%"=="MACHINE" (
    net session >nul 2>&1
    if %ERRORLEVEL% NEQ 0 (
        echo ERROR: This script must be run as Administrator for machine scope.
        echo Re-run as administrator, or use: set-connection-string.bat user
        goto End
    )
)

echo Current CLAUDELOG_CONNECTION_STRING ^(process view^):
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
echo Setting %SCOPE_LABEL%-level CLAUDELOG_CONNECTION_STRING...
setx CLAUDELOG_CONNECTION_STRING "%connString%" %SETX_ARGS% >nul

echo.
echo Done.
echo NOTE: You must open a NEW command prompt or restart any running shells/IDEs/services for the new value to take effect.

:End
echo.
set /p "_pause=Press Enter to exit..."
endlocal
