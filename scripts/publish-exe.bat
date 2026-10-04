@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0.."

echo ============================================================
echo  GameShelf - tek EXE uret
echo ============================================================
echo.
echo 1) Self-contained   (~70 MB, baska PC'de hicbir sey gerekmez)  [onerilen]
echo 2) Framework-dep.   (~10 MB, hedefte .NET 8 Desktop Runtime gerekir)
echo.
set /p SECIM="Secimin (1 veya 2, varsayilan 1): "
if "%SECIM%"=="" set SECIM=1

if "%SECIM%"=="2" (
    set SELF=-p:SelfContained=false
    echo Framework-dependent EXE uretiliyor...
) else (
    set SELF=-p:SelfContained=true -p:EnableCompressionInSingleFile=true
    echo Self-contained EXE uretiliyor...
)

dotnet publish src\GameShelf.App\GameShelf.App.csproj ^
    -c Release -r win-x64 %SELF% -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
    echo.
    echo PUBLISH BASARISIZ. Yukaridaki hatayi oku.
    popd
    endlocal
    exit /b 1
)

set PUB=%~dp0..\src\GameShelf.App\bin\Release\net8.0-windows\win-x64\publish
set DIST=%~dp0..\dist

echo.
echo EXE kopyalaniyor: %DIST%
if not exist "%DIST%" mkdir "%DIST%"
copy /Y "%PUB%\GameShelf.exe" "%DIST%\GameShelf.exe" >nul

REM Emulatorler icin hazir klasor (kullanici indirdigi emulatorleri buraya koyabilir)
if not exist "%DIST%\emulators" mkdir "%DIST%\emulators"

echo.
echo ============================================================
echo  Bitti: %DIST%\GameShelf.exe
echo ============================================================
echo.
echo  ONEMLI:
echo   - EXE'yi baska bir klasore kopyalayip oradan calistirarak test et.
echo   - Emulatorleri (DuckStation / PCSX2 / RPCS3) resmi sitelerinden indirip
echo     EXE'nin yanindaki "emulators" klasorune koyarsan, uygulama
echo     "Araclar ^> Otomatik ara" ile otomatik bulur.
echo     Hazir betik: scripts\emulatorleri-indir.ps1
echo   - Bu uygulama emulator, BIOS/firmware veya oyun dosyasi DAGITMAZ.
echo.

start "" explorer "%DIST%"

popd
endlocal
exit /b 0
