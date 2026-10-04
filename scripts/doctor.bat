@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0.."

echo ============================================================
echo  GameShelf - ortam kontrolu
echo ============================================================
echo.

echo [.NET SDK]
where dotnet >nul 2>nul
if errorlevel 1 (
    echo   YOK -> https://dotnet.microsoft.com/download/dotnet/8.0
) else (
    for /f "tokens=*" %%i in ('dotnet --version') do echo   surum: %%i
    echo   SDK listesi:
    dotnet --list-sdks
    echo   Runtime listesi:
    dotnet --list-runtimes
)

echo.
echo [Git]
where git >nul 2>nul
if errorlevel 1 (echo   YOK (opsiyonel)) else (for /f "tokens=*" %%i in ('git --version') do echo   %%i)

echo.
echo [Branch / commit]
if exist ".git" (
    for /f "tokens=*" %%i in ('git rev-parse --abbrev-ref HEAD') do echo   branch: %%i
    for /f "tokens=*" %%i in ('git log --oneline -1') do echo   commit: %%i
) else (
    echo   .git yok - ZIP olarak indirdiysen normal
)

echo.
echo [Onemli dosyalar]
if exist "GameShelf.sln" (echo   GameShelf.sln VAR) else (echo   GameShelf.sln YOK!)
if exist "src\GameShelf.App\GameShelf.App.csproj" (echo   App projesi VAR) else (echo   App projesi YOK!)

echo.
echo [Veri klasoru]
if exist "%LOCALAPPDATA%\GameShelf" (
    echo   %LOCALAPPDATA%\GameShelf var
    dir /b "%LOCALAPPDATA%\GameShelf"
) else (
    echo   Henuz olusmamis (uygulama ilk kez calismadi)
)

echo.
pause
popd
endlocal
