@echo off
setlocal
call "%~dp0Run-ChromaLink.cmd" -Mode watch -Argument2 100
exit /b %ERRORLEVEL%
