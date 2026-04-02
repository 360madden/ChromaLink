@echo off
setlocal

set "SNAPSHOT=%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json"
if not exist "%SNAPSHOT%" (
    echo Telemetry snapshot not found: %SNAPSHOT%
    exit /b 1
)

start "" "%SNAPSHOT%"
exit /b %ERRORLEVEL%
