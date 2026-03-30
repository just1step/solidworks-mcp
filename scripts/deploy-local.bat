@echo off
setlocal

set "NODE_EXE=%ProgramFiles%\nodejs\node.exe"
set "NPM_CMD=%ProgramFiles%\nodejs\npm.cmd"
if not exist "%NODE_EXE%" set "NODE_EXE=%LOCALAPPDATA%\Programs\nodejs\node.exe"
if not exist "%NPM_CMD%" set "NPM_CMD=%LOCALAPPDATA%\Programs\nodejs\npm.cmd"

for %%I in ("%NODE_EXE%") do set "NODE_DIR=%%~dpI"

echo ================================================
echo  SolidWorks MCP Server - Local Deployment
echo ================================================

if not exist "%NODE_EXE%" (
    echo.
    echo FAILED: node.exe not found.
    echo Checked:
    echo   %ProgramFiles%\nodejs\node.exe
    echo   %LOCALAPPDATA%\Programs\nodejs\node.exe
    echo.
    echo Please install Node.js or update scripts\deploy-local.bat with the correct node.exe path.
    exit /b 1
)

if not exist "%NPM_CMD%" (
    echo.
    echo FAILED: npm.cmd not found.
    echo Checked:
    echo   %ProgramFiles%\nodejs\npm.cmd
    echo   %LOCALAPPDATA%\Programs\nodejs\npm.cmd
    echo.
    echo Please install Node.js or update scripts\deploy-local.bat with the correct npm.cmd path.
    exit /b 1
)

set "PATH=%NODE_DIR%;%PATH%"

echo.
echo [1/3] Restoring and building C# Bridge (Release)...
cd /d "%~dp0..\bridge"
dotnet build SolidWorksBridge.sln -c Release
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: C# build failed.
    exit /b 1
)

echo.
echo [2/3] Installing Node.js dependencies...
cd /d "%~dp0..\mcp-server"
call "%NPM_CMD%" install
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: npm install failed.
    exit /b 1
)

echo.
echo [3/3] Building MCP server (TypeScript -> dist)...
call "%NPM_CMD%" run build
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: MCP server build failed.
    exit /b 1
)

echo.
echo ================================================
echo  LOCAL DEPLOYMENT COMPLETE
echo ================================================
exit /b 0