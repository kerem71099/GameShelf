@echo off
chcp 65001 >nul
start "" explorer "%LOCALAPPDATA%\GameShelf"
echo GameShelf veri klasoru acildi: %LOCALAPPDATA%\GameShelf
echo   settings.json  - ayarlar
echo   library.db     - kutuphane (SQLite)
echo   Metadata\      - kapak gorselleri
echo   logs\          - uygulama loglari
echo   plugins\       - (opsiyonel) plugin klasorleri
