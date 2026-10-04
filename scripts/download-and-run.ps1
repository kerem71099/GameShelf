<#
  GameShelf'i indir, aç, derle ve çalıştır.  Git gerekmez.

  Kullanım (PowerShell):
      powershell -ExecutionPolicy Bypass -File .\download-and-run.ps1

  Veya sağ tık -> "PowerShell ile çalıştır".
#>

$ErrorActionPreference = "Stop"

# GitHub TLS 1.2 istiyor (eski Windows PowerShell varsayılanı TLS 1.0)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$zipUrl     = "https://github.com/kerem71099/GameShelf/archive/refs/heads/arena/01a107bc-gameshelf.zip"
$folderName = "GameShelf-arena-01a107bc-gameshelf"

# Masaüstü (OneDrive yönlendirmesi varsa $HOME'u kullan)
$base = Join-Path $HOME "Desktop"
if (-not (Test-Path $base)) { $base = $HOME }

$dest = Join-Path $base "GameShelf"
$zip  = Join-Path $base "GameShelf.zip"

try {
    Write-Host "== 1/5 Indiriliyor ==" -ForegroundColor Cyan
    Write-Host "    $zipUrl"
    Invoke-WebRequest -Uri $zipUrl -OutFile $zip

    Write-Host "== 2/5 Aciliiyor ==" -ForegroundColor Cyan
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $dest -Force

    $project = Join-Path $dest $folderName
    if (-not (Test-Path $project)) {
        throw "Beklenen klasor bulunamadi: $project"
    }

    Write-Host "== 3/5 Proje klasoru ==" -ForegroundColor Cyan
    Write-Host "    $project"
    Set-Location $project
    Get-ChildItem | Select-Object -ExpandProperty Name

    Write-Host "== 4/5 Derleniyor ==" -ForegroundColor Cyan
    dotnet restore GameShelf.sln
    if ($LASTEXITCODE -ne 0) { throw "restore basarisiz (cikis kodu $LASTEXITCODE)" }

    dotnet build GameShelf.sln -c Debug --no-restore
    if ($LASTEXITCODE -ne 0) { throw "build basarisiz (cikis kodu $LASTEXITCODE)" }

    Write-Host "== 5/5 Calistiriliyor ==" -ForegroundColor Cyan
    dotnet run --project src\GameShelf.App\GameShelf.App.csproj -c Debug --no-build
}
catch {
    Write-Host ""
    Write-Host "HATA: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Ipuclari:" -ForegroundColor Yellow
    Write-Host "  * 'dotnet' bulunamazsa .NET 8 SDK kur: https://dotnet.microsoft.com/download/dotnet/8.0"
    Write-Host "  * Invoke-WebRequest internet/proxy hatasi verirse ZIP'i tarayicidan indirip"
    Write-Host "    sag tik -> Tumunu ayikla yap, sonra scripts\run.bat dosyasina cift tikla."
    Write-Host ""
    Write-Host "Devam etmek icin bir tusa bas..."
    [void][System.Console]::ReadKey($true)
    exit 1
}
