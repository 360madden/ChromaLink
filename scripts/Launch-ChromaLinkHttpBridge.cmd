@echo off
setlocal

set "REPO_ROOT=%~dp0.."
set "HTTP_BRIDGE_PROJECT=%REPO_ROOT%\DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj"

if not exist "%HTTP_BRIDGE_PROJECT%" (
  echo HTTP bridge project not found: %HTTP_BRIDGE_PROJECT%
  exit /b 1
)

start "" /min dotnet run --project "%HTTP_BRIDGE_PROJECT%" %*
exit /b 0
