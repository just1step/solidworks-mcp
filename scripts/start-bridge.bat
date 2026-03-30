@echo off
setlocal

echo ================================================
echo  SolidWorks MCP Server - Start Bridge
echo ================================================

set "POWERSHELL_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

"%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0ensure-bridge.ps1"

exit /b %ERRORLEVEL%