@echo off
setlocal

start "" cmd /c "%~dp0Launch-ChromaLinkHttpBridge.cmd"
start "" cmd /c "%~dp0Bridge-ChromaLink.cmd"
start "" cmd /c "%~dp0Open-ChromaLink-Monitor.cmd"

exit /b 0
