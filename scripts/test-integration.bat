@echo off
echo ================================================
echo  SolidWorks MCP Server - Integration Tests
echo  Requires: SolidWorks running on this machine
echo ================================================

echo.
echo Running C# integration tests...
echo ------------------------------------------------
cd /d "%~dp0..\bridge"
dotnet test SolidWorksBridge.sln --filter "Category=Integration"
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
