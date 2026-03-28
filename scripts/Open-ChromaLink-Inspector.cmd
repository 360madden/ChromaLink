@echo off
setlocal
set "REPO_ROOT=%~dp0.."
dotnet run --project "%REPO_ROOT%\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj" %*
exit /b %ERRORLEVEL%
