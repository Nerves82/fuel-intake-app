@echo off
echo Folder this launcher is in: %~dp0
echo.
if not exist "%~dp0New-ClientFolder.ps1" (
  echo PROBLEM: New-ClientFolder.ps1 is not in this folder.
  echo Files here named New-ClientFolder*:
  dir /b "%~dp0New-ClientFolder*"
  echo.
  pause
  exit /b
)
echo Execution policy settings on this PC:
powershell.exe -NoProfile -Command "Get-ExecutionPolicy -List | Format-Table -AutoSize; 'Language mode: ' + $ExecutionContext.SessionState.LanguageMode"
echo.
echo Running the script...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0New-ClientFolder.ps1"
echo.
echo Exit code: %ERRORLEVEL%
pause
