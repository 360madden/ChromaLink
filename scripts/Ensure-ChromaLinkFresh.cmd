@echo off
setlocal

python "%~dp0ensure_chromalink_fresh.py" %*
exit /b %ERRORLEVEL%
