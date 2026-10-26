@echo off
rem Removes Jazira: closes it, turns off "Start with Windows", deletes shortcuts and the program folder.
rem Your water log and settings in %APPDATA%\Jazira are kept; delete that folder too for a clean removal.
taskkill /IM Jazira.exe /F >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v Jazira /f >nul 2>&1
powershell -NoProfile -Command ^
  "foreach ($dir in @([Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Desktop'))) {" ^
  "  $l = Join-Path $dir 'Jazira.lnk'; if (Test-Path $l) { Remove-Item $l } }"
timeout /t 1 /nobreak >nul
rmdir /S /Q "%LOCALAPPDATA%\Programs\Jazira" 2>nul
echo Jazira removed. Settings stay in %APPDATA%\Jazira
timeout /t 5 >nul
