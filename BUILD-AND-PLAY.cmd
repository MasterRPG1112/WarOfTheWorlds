@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Build-Game.ps1"
if errorlevel 1 (
  echo.
  echo Build failed. See Builds\windows-build.log and PLAY-FIRST.txt.
  pause
  exit /b 1
)
exit /b 0
