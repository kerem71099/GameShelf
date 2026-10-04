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

rem Her projeyi ayri ayri derle: bir proje patlarsa digerleri de denensin
rem (boylece tum hatalar TEK dosyada gorunur)
for %%p in (GameShelf.Domain GameShelf.Application GameShelf.Infrastructure) do (
    echo ===== BUILD src\%%p =====       >> "%LOG%"
    dotnet build src\%%p\%%p.csproj -c Debug --no-restore -v minimal >> "%LOG%" 2>&1
)
echo ===== BUILD src\GameShelf.App =====  >> "%LOG%"
dotnet build src\GameShelf.App\GameShelf.App.csproj -c Debug --no-restore -v minimal >> "%LOG%" 2>&1
echo ===== BUILD tests =====              >> "%LOG%"
dotnet build tests\GameShelf.Tests\GameShelf.Tests.csproj -c Debug --no-restore -v minimal >> "%LOG%" 2>&1

echo Bitti: %LOG%
echo.
echo --- Son 40 satir ---
powershell -NoProfile -Command "Get-Content '%LOG%' -Tail 40"

start "" notepad "%LOG%"

popd
endlocal
exit /b 0
