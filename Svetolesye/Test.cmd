@echo off
cd /d "%~dp0"
dotnet run --project tests\Svetolesye.Tests -c Release
pause
