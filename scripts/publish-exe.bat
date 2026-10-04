@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0.."

rem ============================================================
rem  GameShelf - tek EXE uret  (soru sormaz)
rem
rem  Kullanim:
rem    scripts\publish-exe.bat          -> SELF-CONTAINED (~70 MB, hicbir sey gerekmez)
rem    scripts\publish-exe.bat small    -> framework-dependent (~10 MB, .NET 8 runtime gerekir)
rem ============================================================

echo ============================================================
echo  GameShelf - TEK EXE uret
echo ============================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET 8 SDK bulunamadi. Once kur:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    goto :fail
)

set MODE=%1
if "%MODE%"=="" set MODE=self
set SMALL=
if /i "%MODE%"=="small" set SMALL=1
if /i "%MODE%"=="2" set SMALL=1

set OPTS=-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if defined SMALL (
    echo Secim: FRAMEWORK-DEPENDENT ^(~10 MB, hedef PC'de .NET 8 Desktop Runtime gerekir^)
    set OPTS=%OPTS% -p:SelfContained=false
) else (
    echo Secim: SELF-CONTAINED ^(~70 MB, baska PC'de hicbir sey gerekmez^)
    set OPTS=%OPTS% -p:SelfContained=true -p:EnableCompressionInSingleFile=true
)

echo.
echo [1/2] Yayinlaniyor ^(Release / win-x64^). Ilk kez birkac dakika surebilir...
dotnet publish src\GameShelf.App\GameShelf.App.csproj -c Release -r win-x64 %OPTS%
if errorlevel 1 goto :fail

set PUB=%~dp0..\src\GameShelf.App\bin\Release\net8.0-windows\win-x64\publish
set DIST=%~dp0..\dist

if not exist "%PUB%\GameShelf.exe" (
    echo HATA: EXE bulunamadi: %PUB%\GameShelf.exe
    goto :fail
)

echo.
echo [2/2] dist klasorune kopyalaniyor...
if not exist "%DIST%" mkdir "%DIST%"
copy /Y "%PUB%\GameShelf.exe" "%DIST%\GameShelf.exe" >nul

if not exist "%DIST%\emulators" mkdir "%DIST%\emulators"
if not exist "%DIST%\emulators\PS1" mkdir "%DIST%\emulators\PS1"
if not exist "%DIST%\emulators\PS2" mkdir "%DIST%\emulators\PS2"
if not exist "%DIST%\emulators\PS3" mkdir "%DIST%\emulators\PS3"

set INFO=%DIST%\KURULUM-BILGI.txt
echo GameShelf - Offline PlayStation Kutuphanesi > "%INFO%"
echo. >> "%INFO%"
echo 1^) GameShelf.exe dosyasina cift tikla. Ilk acilista kendi oyun >> "%INFO%"
echo    klasorunu olusturur: USERPROFILE icinde GameShelf / Games >> "%INFO%"
echo 2^) Uygulama icinde "Indirmeler" sekmesinden DuckStation, PCSX2 >> "%INFO%"
echo    ve RPCS3 indirilip kurulabilir ^(7z icin 7-Zip gerekir^). >> "%INFO%"
echo 3^) Oyun dosyalarini oyun klasorune at, sonra Araclar / Yeniden tara. >> "%INFO%"
echo. >> "%INFO%"
echo ONEMLI: >> "%INFO%"
echo  - Bu uygulama emuletor, BIOS veya oyun dosyasi DAGITMAZ. >> "%INFO%"
echo  - BIOS'i kendi konsolundan dump etmen gerekir. PS3 firmware'i resmi >> "%INFO%"
echo    olarak Sony sayfasindan indirilir. >> "%INFO%"
echo  - Emulatorleri istersen EXE'nin yanindaki emulators klasorune koy; >> "%INFO%"
echo    uygulama otomatik bulur. >> "%INFO%"

echo.
echo ============================================================
echo  Bitti: %DIST%\GameShelf.exe
echo ============================================================
for %%I in ("%DIST%\GameShelf.exe") do echo  Boyut: %%~zI bayt
echo.
echo  Simdi cift tikla: %DIST%\GameShelf.exe
echo  ^(Baska PC'ye kopyalayip calistirmak icin de ayni dosya.^)
echo.

start "" explorer "%DIST%"

popd
endlocal
exit /b 0

:fail
echo.
echo ISLEM BASARISIZ. Yukaridaki hatayi oku.
echo Log klasoru: %%LOCALAPPDATA%%\GameShelf\logs
popd
endlocal
exit /b 1
