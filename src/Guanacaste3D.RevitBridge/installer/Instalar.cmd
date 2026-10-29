@echo off
rem Instala el complemento Guanacaste 3D para Revit (sin permisos de administrador). Cierre Revit antes.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
pause
