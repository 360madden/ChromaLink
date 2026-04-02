@echo off
setlocal
set "REPO_ROOT=%~dp0.."
start "" /min dotnet run --project "%REPO_ROOT%\DesktopDotNet\ChromaLink.Monitor\ChromaLink.Monitor.csproj" -- --start-minimized %*
exit /b 0
