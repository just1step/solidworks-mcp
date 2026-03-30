@echo off
setlocal

set "NODE_EXE=%npm_node_execpath%"
if "%NODE_EXE%"=="" set "NODE_EXE=node"

"%NODE_EXE%" %*
exit /b %ERRORLEVEL%