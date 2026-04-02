@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Package-ChromaLinkDesktop.ps1" %*
exit /b %ERRORLEVEL%
