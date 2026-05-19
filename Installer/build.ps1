# Build de l'installeur "Serveur des Gato" (architecture cloud).
#
# Étapes :
#  1. publie le launcher Giny.Uplauncher en self-contained single-file ;
#  2. copie le client Dofus 2.68.0.0 complet ;
#  3. patche connection.host du config.xml du client (-> serveur cloud) ;
#  4. compile l'installeur via Inno Setup (ISCC).
#
# Sortie : Installer\output\ServeurDesGato-Setup-v<version>.exe

param(
    [string]$Version = "1.0.0",
    [string]$ServerHost = "JMBouftou:deindeiruworld.duckdns.org:5555"
)

$ErrorActionPreference = "Stop"

$root          = Split-Path -Parent $PSScriptRoot
$staging       = Join-Path $PSScriptRoot "payload"
$launcherProj  = Join-Path $root "Sources\Zaap\Giny.Uplauncher\Giny.Uplauncher.csproj"
$clientSource  = Join-Path $root "Ressources\Dofus"

# --- 1. Nettoyage du staging ---
Write-Host "==> Nettoyage du staging..." -ForegroundColor Cyan
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force -Path $staging | Out-Null

# --- 2. Publication du launcher (self-contained single-file) ---
Write-Host "==> Publication du launcher..." -ForegroundColor Cyan
if (-not (Test-Path $launcherProj)) { throw "Projet launcher introuvable : $launcherProj" }
dotnet publish $launcherProj `
    -c Release -r win-x64 --self-contained true `
    /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    -o "$staging\launcher"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish a échoué" }

# Les .pdb (symboles de debug) ne sont pas distribués.
Get-ChildItem -Path "$staging\launcher" -Filter *.pdb -Recurse | Remove-Item -Force

# --- 3. Copie du client Dofus 2.68.0.0 complet ---
Write-Host "==> Copie du client Dofus..." -ForegroundColor Cyan
if (-not (Test-Path $clientSource)) { throw "Client introuvable : $clientSource" }
New-Item -ItemType Directory -Force -Path "$staging\client-dofus" | Out-Null
Copy-Item -Recurse -Force "$clientSource\*" "$staging\client-dofus\"

# --- 4. Patch du config.xml du client ---
# Format Dofus 2.68 : <entry key="connection.host">JMBouftou:HOTE:PORT</entry>
# (élément XML, pas attribut). Le port est celui de l'Auth (5555).
Write-Host "==> Patch de connection.host -> $ServerHost ..." -ForegroundColor Cyan
$configs = Get-ChildItem -Path "$staging\client-dofus" -Filter "config.xml" -Recurse
if ($configs.Count -eq 0) { throw "Aucun config.xml trouvé dans le client" }
foreach ($cfg in $configs) {
    $content = Get-Content $cfg.FullName -Raw
    $patched = $content -replace '(<entry key="connection\.host">)[^<]*(</entry>)', "`${1}$ServerHost`${2}"
    Set-Content -Path $cfg.FullName -Value $patched -NoNewline
    Write-Host "    patché : $($cfg.FullName)"
}

# --- 5. Compilation Inno Setup ---
Write-Host "==> Compilation de l'installeur (Inno Setup)..." -ForegroundColor Cyan
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    throw "Inno Setup 6 introuvable ($iscc). Installer depuis https://jrsoftware.org/isdl.php"
}
& $iscc "/DAppVersion=$Version" "$PSScriptRoot\DeindeiruInstaller.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC a échoué" }

Write-Host ""
Write-Host "==> Terminé. Installeur dans : $PSScriptRoot\output\" -ForegroundColor Green
