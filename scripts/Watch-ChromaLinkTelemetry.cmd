@echo off
setlocal

call "%~dp0Show-ChromaLinkTelemetry.cmd" -Watch
exit /b %ERRORLEVEL%
