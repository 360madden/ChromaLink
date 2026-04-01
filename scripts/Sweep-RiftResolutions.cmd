@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Sweep-RiftResolutions.ps1" %*
exit /b %ERRORLEVEL%
