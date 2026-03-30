@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PACKAGE_ROOT=%SCRIPT_DIR%.."
set "NODE_EXE=%npm_node_execpath%"

if not "%NODE_EXE%"=="" if exist "%NODE_EXE%" goto run
if exist "%PACKAGE_ROOT%\node.exe" set "NODE_EXE=%PACKAGE_ROOT%\node.exe"
if not "%NODE_EXE%"=="" if exist "%NODE_EXE%" goto run
if defined ProgramFiles if exist "%ProgramFiles%\nodejs\node.exe" set "NODE_EXE=%ProgramFiles%\nodejs\node.exe"
if not "%NODE_EXE%"=="" if exist "%NODE_EXE%" goto run
if defined ProgramFiles(x86) if exist "%ProgramFiles(x86)%\nodejs\node.exe" set "NODE_EXE=%ProgramFiles(x86)%\nodejs\node.exe"
if not "%NODE_EXE%"=="" if exist "%NODE_EXE%" goto run
for %%I in (node.exe) do set "NODE_EXE=%%~$PATH:I"
if not "%NODE_EXE%"=="" goto run

>&2 echo Unable to locate node.exe. Install Node.js 18+ or set npm_node_execpath.
exit /b 1

:run
"%NODE_EXE%" "%PACKAGE_ROOT%\dist\index.js" %*
exit /b %ERRORLEVEL%