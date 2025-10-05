@echo off
REM ============================================
REM ClaudeLog - Update and Run Script
REM ============================================
REM Stops app on port 15088, builds, publishes, and starts
REM Can be run from anywhere
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
    taskkill /F /PID %PID% 2>nul
    if %ERRORLEVEL% EQU 0 (
        echo Process killed successfully.
    ) else (
        echo Warning: Could not kill process. Continuing anyway...
    )
    timeout /t 2 /nobreak >nul
) else (
    echo No process found on port 15088. Continuing...
)

echo.
echo ============================================
echo Step 2: Cleaning and building project...
echo ============================================
echo.

REM Navigate to source directory (absolute path)
cd /d "C:\Users\jeffr\source\repos\ClaudeLog\ClaudeLog.Web"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Cannot find source directory!
    echo Expected: C:\Users\jeffr\source\repos\ClaudeLog\ClaudeLog.Web
    pause
    exit /b 1
)
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
echo Access at: http://localhost:15088
echo Press Ctrl+C to stop the application
echo.

cd "C:\Apps\ClaudeLog.Web"
ClaudeLog.Web.exe
