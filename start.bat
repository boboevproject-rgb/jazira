@echo off
rem Jazira local site: http://localhost:8791/  (close this window to stop)
rem Forms are saved to dev-submissions.jsonl while the real Google form URL is not set.
cd /d "%~dp0"
title Jazira - http://localhost:8791/

rem "python --version" also catches the Microsoft Store stub that exists without a real Python
python --version >nul 2>&1
if errorlevel 1 (
  echo Python not found. Install Python 3.8+ from https://www.python.org/downloads/
  echo and tick "Add python.exe to PATH" during setup, then run start.bat again.
  start "" https://www.python.org/downloads/
  pause
  exit /b 1
)

start "" cmd /c "timeout /t 2 /nobreak >nul & start http://localhost:8791/"
python tools\serve.py 8791
if errorlevel 1 pause
