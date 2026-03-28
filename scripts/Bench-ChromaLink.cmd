@echo off
setlocal
call "%~dp0Run-ChromaLink.cmd" -Mode bench
exit /b %ERRORLEVEL%
