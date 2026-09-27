@echo off
cd /d "%~dp0"
if exist "%~dp0app\FocusDesk.exe" (
    start "" "%~dp0app\FocusDesk.exe"
    exit /b
)
echo Building FocusDesk...
dotnet publish "%~dp0src\FocusDesk.App\FocusDesk.App.csproj" -c Release -o "%~dp0app"
if errorlevel 1 (
    echo Build failed. Open FocusDesk.slnx in Visual Studio to inspect the error.
    pause
    exit /b 1
)
start "" "%~dp0app\FocusDesk.exe"
