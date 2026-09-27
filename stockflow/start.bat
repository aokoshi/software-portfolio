@echo off
cd /d "%~dp0"
C:\xampp\php\php.exe -S 127.0.0.1:8081 -t public public\router.php
pause
