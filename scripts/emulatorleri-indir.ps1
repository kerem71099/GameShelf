<#
.SYNOPSIS
  Emulatorleri RESMI kaynaklarindan (GitHub releases) indirip emulators\ klasorune acar.

.NOT
  GameShelf emulator DAGITMAZ. Bu betik yalnizca resmi surumleri indirir.
  BIOS/firmware ve oyun dosyalari hicbir zaman indirilmez: kendi yasal
  yedeklerinizi kullanmalisiniz.

.ORNEK
  powershell -ExecutionPolicy Bypass -File .\scripts\emulatorleri-indir.ps1
#>
[CmdletBinding()]
param(
    # Nereye indirilsin? Varsayilan: depo kokundeki emulators\ klasoru
    [string]$Hedef = ''
)

# $PSScriptRoot bazen bos gelir, o yuzden elle hesapliyoruz.
if ([string]::IsNullOrWhiteSpace($Hedef)) {
    if ($PSScriptRoot) {
        $kok = Split-Path -Parent $PSScriptRoot
    }
    else {
        $kok = (Get-Location).Path
    }

    $Hedef = Join-Path $kok 'emulators'
}

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$emulatorler = @(
    @{
        Ad = 'DuckStation'; Platform = 'PS1'; Sahip = 'stenzek'; Depo = 'duckstation'
        Desen = 'windows-x64-release\.zip$'
        Yedek = 'windows.*x64.*\.(zip|7z)$'
    },
    @{
        Ad = 'PCSX2'; Platform = 'PS2'; Sahip = 'PCSX2'; Depo = 'pcsx2'
        Desen = 'windows.*(x64|64bit).*\.(zip|7z)$'
        Yedek = 'windows.*\.(zip|7z)$'
    },
    @{
        Ad = 'RPCS3'; Platform = 'PS3'; Sahip = 'RPCS3'; Depo = 'rpcs3'
        Desen = 'win64.*\.(zip|7z)$'
        Yedek = 'windows.*\.(zip|7z)$'
    }
)

function Expand-Arsiv {
    param([string]$Dosya, [string]$Klasor)

    if ($Dosya -like '*.zip') {
        Expand-Archive -Path $Dosya -DestinationPath $Klasor -Force
        return $true
    }

    $yediZip = @('C:\Program Files\7-Zip\7z.exe', 'C:\Program Files (x86)\7-Zip\7z.exe') |
               Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($yediZip) {
        & $yediZip x $Dosya "-o$Klasor" -y | Out-Null
        return $true
    }

    return $false
}

New-Item -ItemType Directory -Force -Path $Hedef | Out-Null
Write-Host "Hedef klasor: $Hedef" -ForegroundColor Cyan

foreach ($e in $emulatorler) {
    Write-Host ""
    Write-Host "=== $($e.Ad) ($($e.Platform)) ===" -ForegroundColor Cyan

    $klasor = Join-Path $Hedef $e.Platform
    $sayfa  = "https://github.com/$($e.Sahip)/$($e.Depo)/releases/latest"

    try {
        $surum = Invoke-RestMethod -Uri "https://api.github.com/repos/$($e.Sahip)/$($e.Depo)/releases/latest" `
                                   -Headers @{ 'User-Agent' = 'GameShelf-Setup' }

        $varlik = $surum.assets | Where-Object { $_.name -match $e.Desen } | Select-Object -First 1

        if (-not $varlik) {
            $varlik = $surum.assets | Where-Object { $_.name -match $e.Yedek } | Select-Object -First 1
        }

        if (-not $varlik) {
            throw "surumde uygun Windows dosyasi bulunamadi"
        }

        $gecici = Join-Path $env:TEMP $varlik.name
        Write-Host "  indiriliyor: $($varlik.name) ($([math]::Round($varlik.size / 1MB, 1)) MB)"
        Invoke-WebRequest -Uri $varlik.browser_download_url -OutFile $gecici

        New-Item -ItemType Directory -Force -Path $klasor | Out-Null

        if (Expand-Arsiv -Dosya $gecici -Klasor $klasor) {
            Write-Host "  acildi -> $klasor" -ForegroundColor Green
        }
        else {
            Write-Host "  7-Zip bulunamadi. Arsiv: $gecici" -ForegroundColor Yellow
            Write-Host "  Elle acip icindekileri su klasore atin: $klasor" -ForegroundColor Yellow
            Start-Process $sayfa
        }

        Remove-Item $gecici -Force -ErrorAction SilentlyContinue
    }
    catch {
        Write-Host "  otomatik indirme basarisiz: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "  tarayicida resmi sayfa aciliyor: $sayfa" -ForegroundColor Yellow
        Start-Process $sayfa
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Bitti: $Hedef" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host " Sonraki adimlar:"
Write-Host "  1) GameShelf > Araclar > KURULUM > 'Otomatik ara' (PS1 / PS2 / PS3)"
Write-Host "     Uygulama su klasorlere bakar: EXE'nin yanindaki emulators\ ve"
Write-Host "     ust klasorlerdeki emulators\ (kaynaktan calistirirken depo koku)."
Write-Host "  2) Oyun klasoru: $env:USERPROFILE\GameShelf\Games"
Write-Host "  3) BIOS/firmware ve oyun dosyalarini KENDI yasal yedeklerinizden saglayin."
Write-Host ""
