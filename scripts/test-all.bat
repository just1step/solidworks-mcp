@echo off
echo ============================================
echo  SolidWorks MCP Server - Run All Tests
echo ============================================

echo.
echo [1/2] Running full .NET test suite...
echo --------------------------------------------
cd /d "%~dp0..\bridge"
dotnet test SolidWorksBridge.sln --no-restore
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: full .NET test suite failed!
    exit /b 1
)

echo.
echo [2/2] Running non-integration .NET tests...
echo --------------------------------------------
dotnet test SolidWorksBridge.sln --filter "Category!=Integration" --no-restore
if %ERRORLEVEL% neq 0 (
    echo.
    echo FAILED: non-integration .NET tests failed!
    exit /b 1
)

echo.
echo ============================================
echo  ALL TESTS PASSED
echo ============================================
exit /b 0
