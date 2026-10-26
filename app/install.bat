@echo off
rem Installs Jazira for the current user, no admin rights needed:
rem copies Jazira.exe to %LOCALAPPDATA%\Programs\Jazira, adds Start menu and desktop shortcuts,
rem then starts the island. Running it again updates Jazira and keeps your settings.
setlocal
set "SRC=%~dp0Jazira.exe"
set "DEST=%LOCALAPPDATA%\Programs\Jazira"
if not exist "%SRC%" (
  echo Jazira.exe not found next to install.bat. Extract the whole ZIP first.
  pause
  exit /b 1
)
rem An old copy may be running: close it so the file can be replaced
taskkill /IM Jazira.exe /F >nul 2>&1
timeout /t 1 /nobreak >nul
if not exist "%DEST%" mkdir "%DEST%"
copy /Y "%SRC%" "%DEST%\Jazira.exe" >nul
if errorlevel 1 (
  echo Could not copy Jazira.exe to %DEST%
  pause
  exit /b 1
)
powershell -NoProfile -Command ^
  "$exe = Join-Path $env:LOCALAPPDATA 'Programs\Jazira\Jazira.exe'; $sh = New-Object -ComObject WScript.Shell;" ^
  "foreach ($dir in @([Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Desktop'))) {" ^
  "  $s = $sh.CreateShortcut((Join-Path $dir 'Jazira.lnk')); $s.TargetPath = $exe;" ^
  "  $s.WorkingDirectory = Split-Path $exe; $s.Description = 'Jazira - island at the top of the screen'; $s.Save() }"
start "" "%DEST%\Jazira.exe"
echo.
echo Jazira is installed: %DEST%
echo The island is at the top of the screen. Settings: the Jazira icon in the tray, next to the clock.
timeout /t 6 >nul
