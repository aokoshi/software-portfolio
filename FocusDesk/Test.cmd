@echo off
cd /d "%~dp0"
dotnet run --project tests\FocusDesk.Tests -c Release
pause
