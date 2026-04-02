@echo off
setlocal

echo ================================================
echo  SolidWorks MCP Server - Local Deployment
echo ================================================

echo.
echo [1/2] Restoring and building C# Bridge (Release)...
cd /d "%~dp0..\bridge"
dotnet build SolidWorksBridge.sln -c Release
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: C# build failed.
    exit /b 1
)

echo.
echo [2/2] Building SolidWorks MCP App (Release)...
cd /d "%~dp0..\app\SolidWorksMcpApp"
dotnet build SolidWorksMcpApp.csproj -c Release
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: SolidWorks MCP App build failed.
    exit /b 1
)

echo.
echo ================================================
echo  LOCAL DEPLOYMENT COMPLETE
echo ================================================
exit /b 0