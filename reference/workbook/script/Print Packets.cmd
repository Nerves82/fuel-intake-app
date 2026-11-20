@echo off
rem Double-click to save the client's forms as combined PDF packets (keep next to Print-Packets.ps1).
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Print-Packets.ps1"
