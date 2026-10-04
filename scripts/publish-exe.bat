@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0.."

echo ============================================================
echo  GameShelf - tek EXE uret
echo ============================================================
echo.
echo 1) Framework-dependent  (~8 MB,  hedefte .NET 8 Runtime gerekir)
echo 2) Self-contained       (~80 MB, hicbir sey gerekmez)
echo.
set /p SECIM="Secimin (1 veya 2): "

if "%SECIM%"=="2" (
    set SELFCONTAINED=true
    echo Self-contained EXE uretiliyor...
) else (
    set SELFCONTAINED=false
    echo Framework-dependent EXE uretiliyor...
)

dotnet publish src\GameShelf.App\GameShelf.App.csproj ^
    -c Release -r win-x64 ^
    --self-contained %SELFCONTAINED% ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
    echo PUBLISH BASARISIZ.
    popd
    endlocal
    exit /b 1
)

set OUT=%~dp0..\src\GameShelf.App\bin\Release\net8.0-windows\win-x64\publish
echo.
echo Bitti: %OUT%\GameShelf.exe
echo ONEMLI: EXE'yi baska bir klasore kopyalayip oradan calistirarak test et
echo         (SQLite native kutuphanesi gercekten aciliyor mu?).

start "" explorer "%OUT%"

popd
endlocal
exit /b 0
