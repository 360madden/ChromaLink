@echo off
setlocal
call "%~dp0Run-ChromaLink.cmd" -Mode prepare-window -Argument1 32 -Argument2 32
exit /b %ERRORLEVEL%
