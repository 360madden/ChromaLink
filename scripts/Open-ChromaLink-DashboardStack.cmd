@echo off
setlocal

call "%~dp0Launch-ChromaLinkHttpBridge.cmd"
start "" cmd /c "%~dp0Open-ChromaLinkDashboard.cmd"

exit /b 0
