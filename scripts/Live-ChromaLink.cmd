@echo off
setlocal
call "%~dp0Run-ChromaLink.cmd" -Mode live -Argument1 20 -Argument2 100
exit /b %ERRORLEVEL%
