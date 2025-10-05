@echo off
REM ============================================
REM ClaudeLog - Update and Run Script
REM ============================================
REM Stops app on port 15088, builds, publishes, and starts
REM ============================================

echo.
echo ============================================
echo ClaudeLog - Update and Run
echo ============================================
echo.

REM Step 1: Find and kill process on port 15088
echo Step 1: Checking for processes on port 15088...
echo.

REM Find PID using port 15088
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :15088 ^| findstr LISTENING') do (
    set PID=%%a
)

if defined PID (
    echo Found process using port 15088 (PID: %PID%)
    echo Killing process...
    taskkill /F /PID %PID%
    if %ERRORLEVEL% EQU 0 (
        echo Process killed successfully.
    ) else (
        echo Failed to kill process. You may need to run as Administrator.
        pause
        exit /b 1
    )
    timeout /t 2 /nobreak >nul
) else (
    echo No process found on port 15088.
)

echo.
echo ============================================
echo Step 2: Cleaning and building project...
echo ============================================
echo.

cd /d "%~dp0"
cd ClaudeLog.Web
dotnet clean
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Clean failed!
    pause
    exit /b 1
)

dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Step 3: Publishing to C:\Apps\ClaudeLog.Web...
echo ============================================
echo.

dotnet publish -c Release -o "C:\Apps\ClaudeLog.Web"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Step 4: Starting ClaudeLog.Web...
echo ============================================
echo.

cd "C:\Apps\ClaudeLog.Web"
start ClaudeLog.Web.exe

timeout /t 3 /nobreak >nul

echo.
echo ============================================
echo SUCCESS!
echo ============================================
echo.
echo ClaudeLog is starting...
echo Access at: http://localhost:15088
echo.
echo Press any key to exit this window...
pause >nul
