@echo off
setlocal
cd /d "%~dp0"
start "Pixel Forge Studio Server" /min "%~dp0.publish\PixelForgeStudio.exe" serve
timeout /t 2 /nobreak >nul
start "" http://127.0.0.1:4876
