@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0.."

echo ============================================================
echo  GameShelf - derle ve calistir
echo ============================================================

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK bulunamadi. Once .NET 8 SDK kur:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    goto :fail
)

echo [1/3] NuGet paketleri geri yukleniyor...
dotnet restore GameShelf.sln
if errorlevel 1 goto :fail

echo [2/3] Derleniyor (Debug)...
dotnet build GameShelf.sln -c Debug --no-restore
if errorlevel 1 goto :fail

echo [3/3] Uygulama aciliyor...
dotnet run --project src\GameShelf.App\GameShelf.App.csproj -c Debug --no-build

popd
endlocal
exit /b 0

:fail
echo.
echo ISLEM BASARISIZ. Yukaridaki hata mesajini oku.
echo Log klasoru: %%LOCALAPPDATA%%\GameShelf\logs
popd
endlocal
exit /b 1
