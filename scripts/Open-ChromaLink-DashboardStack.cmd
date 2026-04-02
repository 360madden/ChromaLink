@echo off
setlocal

call "%~dp0Start-ChromaLinkStack.cmd"
start "" /min cmd /c "%~dp0Open-ChromaLinkDashboard.cmd"

exit /b 0
