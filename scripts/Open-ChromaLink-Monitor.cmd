@echo off
setlocal
set "REPO_ROOT=%~dp0.."
start "" dotnet run --project "%REPO_ROOT%\DesktopDotNet\ChromaLink.Monitor\ChromaLink.Monitor.csproj" %*
exit /b 0
