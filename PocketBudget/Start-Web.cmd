@echo off
cd /d "%~dp0"
call pnpm export:web
if errorlevel 1 exit /b 1
node tools\serve.mjs 8092
pause
