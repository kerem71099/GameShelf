@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0.."

echo ============================================================
echo  GameShelf - testler
echo ============================================================

if "%~1"=="" (
    dotnet test tests\GameShelf.Tests\GameShelf.Tests.csproj
) else (
    echo Filtre: %~1
    dotnet test tests\GameShelf.Tests\GameShelf.Tests.csproj --filter "FullyQualifiedName~%~1"
)

popd
endlocal
exit /b %ERRORLEVEL%
