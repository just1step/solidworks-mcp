@echo off
echo ============================================
echo  SolidWorks MCP Server - Run All Unit Tests
echo ============================================

echo.
echo [1/2] Running Node.js MCP Server tests...
echo --------------------------------------------
cd /d "%~dp0..\mcp-server"
call npx vitest run
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: Node.js tests failed!
    exit /b 1
)

echo.
echo [2/2] Running C# Bridge unit tests...
echo --------------------------------------------
cd /d "%~dp0..\bridge"
dotnet test SolidWorksBridge.sln --filter "Category!=Integration" --no-restore
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: C# tests failed!
    exit /b 1
)

echo.
echo ============================================
echo  ALL TESTS PASSED
echo ============================================
exit /b 0
