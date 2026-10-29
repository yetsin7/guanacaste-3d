@echo off
rem Quita el complemento Guanacaste 3D de Revit. Cierre Revit antes.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Uninstall
pause
