@echo off
setlocal
call "%~dp0Run-ChromaLink.cmd" -Mode validate
exit /b %ERRORLEVEL%
