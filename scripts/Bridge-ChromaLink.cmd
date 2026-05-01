@echo off
setlocal
call "%~dp0Run-ChromaLink.cmd" -Mode watch -Backend screen
exit /b %ERRORLEVEL%
