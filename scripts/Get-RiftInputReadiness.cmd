@echo off
setlocal
powershell -ExecutionPolicy Bypass -File "%~dp0Get-RiftInputReadiness.ps1" %*
