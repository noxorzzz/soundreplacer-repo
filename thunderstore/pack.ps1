<#
.SYNOPSIS
    Собирает мод и упаковывает Thunderstore-пакет (zip для загрузки на thunderstore.io).

.DESCRIPTION
    Требует установленную игру: DLL мода ссылается на BepInEx.dll, 0Harmony.dll и DLL Unity,
    поэтому собрать пакет без игры нельзя.

    Если BepInEx не установлен в папку игры (например, моды запускаются через профиль
    r2modman / Thunderstore Mod Manager), укажите папку с BepInEx.dll через -BepInExCore.

.EXAMPLE
    .\pack.ps1 -GameDir "D:\SteamLibrary\steamapps\common\REPO"

.EXAMPLE
    .\pack.ps1 -GameDir "J:\SteamLibrary\steamapps\common\REPO" `
        -BepInExCore "$env:APPDATA\Thunderstore Mod Manager\DataFolder\REPO\profiles\Default\BepInEx\core"
#>
[CmdletBinding()]
param(
    [string]$GameDir = $env:REPO_GAME_DIR,
    [string]$BepInExCore,
    [string]$FrameworkOverride,
    [string]$Namespace = 'noxorzzz',
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not $GameDir) {
    throw "Укажите папку игры: .\pack.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\REPO' (или переменную REPO_GAME_DIR)."
}
if (-not (Test-Path (Join-Path $GameDir 'REPO.exe'))) {
    Write-Warning "В '$GameDir' нет REPO.exe — проверьте путь."
}
if (-not $BepInExCore) { $BepInExCore = Join-Path $GameDir 'BepInEx\core' }
if (-not (Test-Path (Join-Path $BepInExCore 'BepInEx.dll'))) {
    throw "В '$BepInExCore' нет BepInEx.dll. Установите BepInEx 5 в игру или передайте -BepInExCore <папка профиля>\BepInEx\core."
}
if (-not $FrameworkOverride) { $FrameworkOverride = Join-Path $GameDir 'REPO_Data\Managed' }
if (-not (Test-Path (Join-Path $FrameworkOverride 'mscorlib.dll'))) {
    throw "В '$FrameworkOverride' нет mscorlib.dll — укажите -FrameworkOverride 'папка Managed'."
}

$manifestPath = Join-Path $PSScriptRoot 'manifest.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if (-not $Version) { $Version = $manifest.version_number }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Версия должна быть semver, например 1.0.0" }

Write-Host "Сборка $($manifest.name) $Version..." -ForegroundColor Cyan
$project = Join-Path $root 'mod\SoundReplacer.Mod.csproj'
dotnet build $project -c Release `
    -p:GameDir=$GameDir `
    -p:BepInExCore=$BepInExCore `
    -p:CopyToGame=false `
    -p:FrameworkPathOverride=$FrameworkOverride `
    -p:AutomaticallyUseReferenceAssemblyPackages=false
if ($LASTEXITCODE -ne 0) { throw "Сборка мода завершилась ошибкой." }

$dll = Join-Path $root 'mod\bin\Release\SoundReplacer.dll'
if (-not (Test-Path $dll)) { throw "Не найден $dll" }

# Thunderstore требует icon.png ровно 256x256
$icon = Join-Path $PSScriptRoot 'icon.png'
Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Image]::FromFile($icon)
try {
    if ($image.Width -ne 256 -or $image.Height -ne 256) {
        throw "icon.png должен быть 256x256, а он $($image.Width)x$($image.Height)."
    }
} finally {
    $image.Dispose()
}

$stage = Join-Path $root '.thunderstore-build'
$dist = Join-Path $root 'dist'
Remove-Item $stage, $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage, $dist | Out-Null

Copy-Item $dll $stage
Copy-Item (Join-Path $PSScriptRoot 'README.md') $stage
Copy-Item (Join-Path $PSScriptRoot 'CHANGELOG.md') $stage
Copy-Item $icon $stage

$manifest.version_number = $Version
$outManifest = Join-Path $stage 'manifest.json'
$json = $manifest | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($outManifest, $json, (New-Object Text.UTF8Encoding($false)))

$zip = Join-Path $dist ("{0}-{1}-{2}.zip" -f $Namespace, $manifest.name, $Version)
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force

Write-Host ""
Write-Host "Пакет готов: $zip" -ForegroundColor Green
Write-Host "Содержимое: SoundReplacer.dll, manifest.json, README.md, CHANGELOG.md, icon.png"
Write-Host ""
Write-Host "Загрузка на Thunderstore:" -ForegroundColor Cyan
Write-Host "  веб:  https://thunderstore.io/c/repo/create/  (команда $Namespace)"
Write-Host "  CLI:  dotnet tool install -g tcli"
Write-Host "        tcli publish --file `"$zip`" --token <API_TOKEN>"