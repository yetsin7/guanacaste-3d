@echo off
rem Installs the Revit exporter and Windows thumbnails for the current user.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
pause
