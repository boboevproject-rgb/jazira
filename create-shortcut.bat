@echo off
rem Creates a "Jazira" shortcut on the desktop that starts the local site (start.bat, minimized window).
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$d = [Environment]::GetFolderPath('Desktop'); $lnk = Join-Path $d 'Jazira.lnk';" ^
  "$s = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk);" ^
  "$s.TargetPath = '%~dp0start.bat'; $s.WorkingDirectory = '%~dp0'; $s.IconLocation = '%~dp0assets\icon.ico';" ^
  "$s.WindowStyle = 7; $s.Description = 'Jazira - local site'; $s.Save(); Write-Host ('Shortcut created: ' + $lnk)"
pause
