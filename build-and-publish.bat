@echo off
echo ============================================
echo ClaudeLog - Build and Publish Script
echo ============================================
echo.

echo Step 1: Cleaning previous builds...
dotnet clean ClaudeLog.sln --configuration Release
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo Step 2: Building solution in Release mode...
dotnet build ClaudeLog.sln --configuration Release
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo Step 3: Publishing ClaudeLog.Web to C:\Apps\ClaudeLog.Web...
dotnet publish ClaudeLog.Web\ClaudeLog.Web.csproj ^
  --configuration Release ^
  --output "C:\Apps\ClaudeLog.Web" ^
  --runtime win-x64 ^
  --self-contained false ^
  /p:PublishSingleFile=false
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo Step 4: Publishing ClaudeLog.Hook.Claude to C:\Apps\ClaudeLog.Hook.Claude...
dotnet publish ClaudeLog.Hook.Claude\ClaudeLog.Hook.Claude.csproj ^
  --configuration Release ^
  --output "C:\Apps\ClaudeLog.Hook.Claude" ^
  --runtime win-x64 ^
  --self-contained false
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo ============================================
echo SUCCESS! Published to C:\Apps\
echo ============================================
echo.
echo Published applications:
echo   Web App: C:\Apps\ClaudeLog.Web\ClaudeLog.Web.exe
echo   Hook:    C:\Apps\ClaudeLog.Hook.Claude\ClaudeLog.Hook.Claude.exe
echo.
echo Production settings:
echo   Port: 15088 (configured in appsettings.Production.json)
echo   Environment: Production
echo.
echo Next steps:
echo   1. Update Claude Code settings to use:
echo      "Stop": "C:\\Apps\\ClaudeLog.Hook.Claude\\ClaudeLog.Hook.Claude.exe"
echo.
echo   2. Run the web app:
echo      cd C:\Apps\ClaudeLog.Web
echo      ClaudeLog.Web.exe
echo.
echo   3. Access at: http://localhost:15088
echo.
goto :end

:error
echo.
echo ============================================
echo ERROR: Build/Publish failed!
echo ============================================
echo Please check the error messages above.
exit /b 1

:end
