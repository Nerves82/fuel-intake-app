@echo off
rem Double-click to load fake test data into a COPY of the intake workbook (keep next to Load-TestData.ps1).
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Load-TestData.ps1"
