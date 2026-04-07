@echo off
setlocal
echo ================================================
echo  SolidWorks MCP Server - Integration Tests
echo  Requires: SolidWorks running on this machine
echo  Path: test -^> proxy -^> hub -^> MCP tools -^> SolidWorks
echo ================================================

echo.
echo Building SolidWorks MCP App (Release)...
echo ------------------------------------------------
cd /d "%~dp0..\app\SolidWorksMcpApp"
dotnet build SolidWorksMcpApp.csproj --configuration Release --nologo
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: SolidWorks MCP App build failed!
    exit /b 1
)

set "SOLIDWORKS_MCP_APP_EXE=%~dp0..\app\SolidWorksMcpApp\bin\Release\net8.0-windows\win-x64\SolidWorksMcpApp.exe"

echo.
echo Note: if a tray-based SolidWorksMcpApp hub is already running from an older build,
echo close it before rerunning this script so the proxy can wake the freshly built hub.
echo.
echo Running hub-based C# integration tests...
echo ------------------------------------------------
cd /d "%~dp0..\bridge"
dotnet test SolidWorksBridge.sln --configuration Release --filter "Category=Integration" --logger "console;verbosity=minimal" --nologo
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: Integration tests failed!
    exit /b 1
)

echo.
echo ================================================
echo  INTEGRATION TESTS PASSED
echo ================================================
exit /b 0
