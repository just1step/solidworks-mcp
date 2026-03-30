@echo off
setlocal

set "NODE_EXE=%ProgramFiles%\nodejs\node.exe"
set "NPM_CMD=%ProgramFiles%\nodejs\npm.cmd"
if not exist "%NODE_EXE%" set "NODE_EXE=%LOCALAPPDATA%\Programs\nodejs\node.exe"
if not exist "%NPM_CMD%" set "NPM_CMD=%LOCALAPPDATA%\Programs\nodejs\npm.cmd"

for %%I in ("%NODE_EXE%") do set "NODE_DIR=%%~dpI"

if not exist "%NODE_EXE%" (
    echo FAILED: node.exe not found.
    exit /b 1
)

if not exist "%NPM_CMD%" (
    echo FAILED: npm.cmd not found.
    exit /b 1
)

set "PATH=%NODE_DIR%;%PATH%"

echo ==================================================
echo  SolidWorks MCP Server - MCP Tool Acceptance Tests
echo ==================================================

echo.
echo [1/2] Building MCP server acceptance prerequisites...
cd /d "%~dp0..\mcp-server"
call "%NPM_CMD%" run build
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: MCP server build failed.
    exit /b 1
)

echo.
echo [2/2] Running end-to-end MCP tool acceptance tests...
call "%NPM_CMD%" run test:acceptance
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: MCP tool acceptance tests failed.
    exit /b 1
)

echo.
echo ==================================================
echo  MCP TOOL ACCEPTANCE PASSED
echo ==================================================
exit /b 0