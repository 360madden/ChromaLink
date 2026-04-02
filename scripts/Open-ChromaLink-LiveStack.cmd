@echo off
setlocal

call "%~dp0Start-ChromaLinkStack.cmd"
start "" /min cmd /c "%~dp0Open-ChromaLink-Monitor.cmd"

exit /b 0
