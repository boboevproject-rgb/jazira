@echo off
rem Jazira local site: http://localhost:8791/  (close this window to stop)
rem Forms are saved to dev-submissions.jsonl while the real Google form URL is not set.
cd /d "%~dp0"
title Jazira - http://localhost:8791/
start "" cmd /c "timeout /t 2 /nobreak >nul & start http://localhost:8791/"
python tools\serve.py 8791
