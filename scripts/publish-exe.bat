@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0.."

echo ============================================================
echo  GameShelf - tek EXE uret
echo ============================================================
echo.
echo 1) Framework-dependent  (~8 MB,  hedefte .NET 8 Desktop Runtime gerekir)
echo 2) Self-contained       (~45 MB, hicbir sey gerekmez)
echo.
set /p SECIM="Secimin (1 veya 2): "

set EXTRA=
if "%SECIM%"=="2" (
    set SELF=-p:SelfContained=true -p:EnableCompressionInSingleFile=true
    echo Self-contained EXE uretiliyor...
) else (
    set SELF=-p:SelfContained=false
    echo Framework-dependent EXE uretiliyor...
)

dotnet publish src\GameShelf.App\GameShelf.App.csproj ^
    -c Release -r win-x64 %SELF%

if errorlevel 1 (
    echo.
    echo PUBLISH BASARISIZ.
    popd
    endlocal
    exit /b 1
)

set OUT=%~dp0..\src\GameShelf.App\bin\Release\net8.0-windows\win-x64\publish
echo.
echo Bitti: %OUT%\GameShelf.exe
echo ONEMLI: EXE'yi baska bir klasore kopyalayip oradan calistirarak test et.

start "" explorer "%OUT%"

popd
endlocal
exit /b 0
