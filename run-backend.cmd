@echo off
rem Double-click to start the backend API (http://localhost:5080).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-backend.ps1"
pause
