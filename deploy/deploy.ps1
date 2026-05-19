<#
.SYNOPSIS
    Déploie Giny Auth + World sur la VM cloud Deindeiru-Prod.

.PARAMETER Target
    'auth', 'world' ou 'all' (défaut: all)

.PARAMETER SkipBuild
    Sauter l'étape de build (réutiliser le binaire existant dans publish/)

.EXAMPLE
    .\deploy.ps1                    # Build + deploy auth + world
    .\deploy.ps1 -Target world      # Deploy World seulement
    .\deploy.ps1 -SkipBuild         # Deploy sans rebuild

.NOTES
    Prérequis : ssh/scp (OpenSSH), clé ~/.ssh/id_ed25519 autorisée sur la VM.
    Le dossier /opt/deindeiru doit déjà exister et appartenir à l'utilisateur
    'deindeiru' (fait par install-on-vm.sh, à lancer une fois avant).
#>

param(
    [ValidateSet("auth", "world", "all")]
    [string]$Target = "all",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Config
$VM_USER   = "deindeiru"
$VM_HOST   = "deindeiruworld.duckdns.org"   # fallback IP : 178.105.117.203
$REPO_ROOT = Split-Path -Parent $PSScriptRoot

function Deploy-Component {
    param(
        [string]$Name,
        [string]$ProjectPath,
        [string]$RemotePath,
        [string]$ServiceName,
        [string]$ExeName
    )

    $publishDir = Join-Path $REPO_ROOT "publish\$Name"

    if (-not $SkipBuild) {
        Write-Host "==> Build $Name (linux-x64 self-contained)..." -ForegroundColor Cyan
        if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
        # PublishSingleFile désactivé : MySql.Data ancien (Oracle) ne supporte
        # pas le bundle single-file .NET 6 — bug connu avec Assembly.CodeBase
        # (lance une exception NotSupportedException au chargement du driver).
        # Migrer vers MySqlConnector à terme permettrait de réactiver
        # PublishSingleFile=true + IncludeNativeLibrariesForSelfExtract=true.
        # On garde --self-contained true : le runtime .NET reste embarqué,
        # donc la VM n'a toujours pas besoin de dotnet installé.
        dotnet publish $ProjectPath `
            -c Release -r linux-x64 --self-contained true `
            -o $publishDir
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish $Name a échoué" }
    }

    if (-not (Test-Path $publishDir)) {
        throw "publish/$Name introuvable — relancer sans -SkipBuild."
    }

    Write-Host "==> Arrêt de $ServiceName sur la VM..." -ForegroundColor Cyan
    # Échoue silencieusement au tout premier déploiement (service pas encore installé).
    ssh "$VM_USER@$VM_HOST" "sudo systemctl stop $ServiceName"

    Write-Host "==> Envoi de $Name vers $RemotePath..." -ForegroundColor Cyan
    ssh "$VM_USER@$VM_HOST" "mkdir -p $RemotePath"
    # scp ne globbe pas les chemins Windows : on énumère les éléments à envoyer.
    $items = Get-ChildItem -Path $publishDir | ForEach-Object { $_.FullName }
    scp -r @items "$VM_USER@${VM_HOST}:$RemotePath/"
    if ($LASTEXITCODE -ne 0) { throw "scp $Name a échoué" }

    Write-Host "==> Permission d'exécution sur le binaire..." -ForegroundColor Cyan
    # L'apphost Linux n'a pas le bit +x après un scp depuis Windows (NTFS
    # ne porte pas les permissions Unix).
    ssh "$VM_USER@$VM_HOST" "chmod +x $RemotePath/$ExeName"

    # Point de contrôle : la config de production doit exister sur la VM.
    $configState = ssh "$VM_USER@$VM_HOST" "test -f $RemotePath/config.Production.json && echo OK"
    if ("$configState".Trim() -ne "OK") {
        Write-Host ""
        Write-Host "  ERREUR : $RemotePath/config.Production.json est absent sur la VM." -ForegroundColor Red
        Write-Host "  $ServiceName n'a PAS été démarré." -ForegroundColor Red
        Write-Host "  Sur la VM, créez-le depuis le template puis renseignez SQLPassword :" -ForegroundColor Yellow
        Write-Host "    cp $RemotePath/config.Production.example.json $RemotePath/config.Production.json" -ForegroundColor Yellow
        Write-Host "    nano $RemotePath/config.Production.json" -ForegroundColor Yellow
        Write-Host "  puis relancez .\deploy.ps1 -Target $Name" -ForegroundColor Yellow
        Write-Host ""
        return
    }

    Write-Host "==> Démarrage de $ServiceName..." -ForegroundColor Cyan
    ssh "$VM_USER@$VM_HOST" "sudo systemctl start $ServiceName"

    Start-Sleep -Seconds 2
    Write-Host "==> Statut :" -ForegroundColor Cyan
    ssh "$VM_USER@$VM_HOST" "sudo systemctl status $ServiceName --no-pager -l | head -20"
}

Write-Host "Deploiement vers $VM_HOST" -ForegroundColor Green

if ($Target -in "auth", "all") {
    Deploy-Component -Name "auth" `
        -ProjectPath "$REPO_ROOT\Sources\Servers\Giny.Auth\Giny.Auth.csproj" `
        -RemotePath "/opt/deindeiru/auth" `
        -ServiceName "deindeiru-auth" `
        -ExeName "Giny.Auth"
}

if ($Target -in "world", "all") {
    Deploy-Component -Name "world" `
        -ProjectPath "$REPO_ROOT\Sources\Servers\Giny.World\Giny.World.csproj" `
        -RemotePath "/opt/deindeiru/world" `
        -ServiceName "deindeiru-world" `
        -ExeName "Giny.World"
}

Write-Host "Deploiement termine." -ForegroundColor Green
