@echo off
cd /d "%~dp0"
if exist "%~dp0app\Svetolesye.exe" (
    start "" "%~dp0app\Svetolesye.exe"
    exit /b
)
dotnet publish "%~dp0src\Svetolesye.Game\Svetolesye.Game.csproj" -c Release -o "%~dp0app"
if errorlevel 1 (
    echo Build failed. Open Svetolesye.slnx in Visual Studio.
    pause
    exit /b 1
)
start "" "%~dp0app\Svetolesye.exe"
