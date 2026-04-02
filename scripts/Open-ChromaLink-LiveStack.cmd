@echo off
setlocal

start "" /min cmd /c "%~dp0Launch-ChromaLinkHttpBridge.cmd"
start "" /min cmd /c "%~dp0Bridge-ChromaLink.cmd"
start "" /min cmd /c "%~dp0Open-ChromaLink-Monitor.cmd"

exit /b 0
