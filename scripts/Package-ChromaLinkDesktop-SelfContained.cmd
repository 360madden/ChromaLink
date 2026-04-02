@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Package-ChromaLinkDesktop.ps1" -SelfContained -OutputRoot "%~dp0..\artifacts\package-selfcontained" %*
exit /b %ERRORLEVEL%
