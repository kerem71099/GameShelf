@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0.."

echo ============================================================
echo  GameShelf - derleme logu olusturuluyor
echo ============================================================
echo.

set LOG=%~dp0..\build-log.txt

dotnet --version                        >  "%LOG%" 2>&1
echo.                                   >> "%LOG%"
echo ===== RESTORE =====                >> "%LOG%"
dotnet restore GameShelf.sln            >> "%LOG%" 2>&1
echo.                                   >> "%LOG%"
echo ===== BUILD =====                  >> "%LOG%"
dotnet build GameShelf.sln -c Debug --no-restore -v minimal >> "%LOG%" 2>&1

echo Bitti: %LOG%
echo.
echo --- Son 40 satir ---
powershell -NoProfile -Command "Get-Content '%LOG%' -Tail 40"

start "" notepad "%LOG%"

popd
endlocal
exit /b 0
