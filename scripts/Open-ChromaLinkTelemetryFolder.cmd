@echo off
setlocal

set "OUTDIR=%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out"
if not exist "%OUTDIR%" (
    echo Telemetry output folder not found: %OUTDIR%
    exit /b 1
)

start "" explorer "%OUTDIR%"
exit /b %ERRORLEVEL%
