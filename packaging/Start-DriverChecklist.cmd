@echo off
cd /d "%~dp0"
DriverChecklist.Api.exe --portable
if errorlevel 1 (
  echo.
  echo The application could not start. Check the error above.
  echo If port 5080 is already in use, close the other instance first.
  pause
)
