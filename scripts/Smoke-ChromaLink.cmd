@echo off
setlocal
call "%~dp0Run-ChromaLink.cmd" -Mode smoke
exit /b %ERRORLEVEL%
