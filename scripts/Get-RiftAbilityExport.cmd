@echo off
setlocal
powershell -ExecutionPolicy Bypass -File "%~dp0Get-RiftAbilityExport.ps1" %*
