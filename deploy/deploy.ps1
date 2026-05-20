<#
.SYNOPSIS
    Déploie Giny Auth + World + Synchronizer sur la VM cloud Deindeiru-Prod.

.PARAMETER Target
    'auth', 'world', 'synchronizer' ou 'all' (défaut: all).

    Asymétrie volontaire : 'all' = auth + world UNIQUEMENT (services live,
    cadence de déploiement quotidienne). Le Synchronizer doit être demandé
    explicitement avec -Target synchronizer — sa cadence est rare (patch
    Dofus, changement de schéma DB) et il a sa propre vérif de config et
    son propre push opt-in du client Dofus.

.PARAMETER SkipBuild
    Sauter l'étape de build (réutiliser le binaire existant dans publish/)

.PARAMETER PushClient
    Pousser le sous-set Dofus client requis par le Synchronizer
    (Ressources/Dofus/data/common/ + data/i18n/, ~192 MB) vers
    /opt/deindeiru/client-data/ sur la VM. Opt-in : à demander
    explicitement à chaque update du client Dofus.

.EXAMPLE
    .\deploy.ps1                                    # auth + world (PAS synchronizer)
    .\deploy.ps1 -Target world                      # World seulement
    .\deploy.ps1 -Target synchronizer               # Synchronizer (binaire seul)
    .\deploy.ps1 -Target synchronizer -PushClient   # Synchronizer + client Dofus
    .\deploy.ps1 -SkipBuild                         # auth + world sans rebuild

.NOTES
    Prérequis : ssh/scp (OpenSSH), clé ~/.ssh/id_ed25519 autorisée sur la VM.
    Le dossier /opt/deindeiru doit déjà exister et appartenir à l'utilisateur
    'deindeiru' (fait par install-on-vm.sh, à lancer une fois avant).
    -PushClient utilise tar (bsdtar Windows 10+) + scp + tar -xzf distant.
#>

param(
    [ValidateSet("auth", "world", "synchronizer", "all")]
    [string]$Target = "all",
    [switch]$SkipBuild,
    [switch]$PushClient
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
        [string]$ExeName,
        # Liste des SWF que ce composant DOIT exposer dans son dossier SWF/
        # à l'exécution. Vérifiés dans le publish output avant scp pour qu'on
        # ne livre jamais un binaire orphelin (l'Auth crashe à la connexion
        # d'un client si AuthPatch.swf manque, le panneau Hero Mode ne
        # s'affiche pas si HeroPanel.swf manque).
        [string[]]$RequiredSwfs = @(),
        # Composant CLI (one-shot) plutôt que service systemd. Skip les appels
        # systemctl stop/start/status. Le build+scp+chmod+vérif config tournent
        # à l'identique. Utilisé pour le Synchronizer.
        [switch]$NoService
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

    # Vérifie que les RawPatch SWF attendus sont bien dans le publish. Les
    # csproj de Auth/World les incluent en <Content> avec
    # CopyToPublishDirectory=PreserveNewest depuis Ressources/SWFPatches/ :
    # si un fichier manque ici, c'est que la source canonique a disparu ou
    # n'a pas été déplacée — on refuse de livrer pour ne pas casser la VM.
    foreach ($swf in $RequiredSwfs) {
        $swfPath = Join-Path $publishDir "SWF\$swf"
        if (-not (Test-Path $swfPath)) {
            throw "RawPatch manquant : $swfPath (le publish n'a pas produit le SWF). Vérifier Ressources/SWFPatches/ et le <Content Include> du csproj."
        }
        Write-Host "    SWF/$swf  ✓" -ForegroundColor DarkGray
    }

    if (-not $NoService) {
        Write-Host "==> Arrêt de $ServiceName sur la VM..." -ForegroundColor Cyan
        # Échoue silencieusement au tout premier déploiement (service pas encore installé).
        ssh "$VM_USER@$VM_HOST" "sudo systemctl stop $ServiceName"
    }

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
        if ($NoService) {
            Write-Host "  Le binaire $Name est déployé mais ne peut pas être lancé sans config." -ForegroundColor Red
        } else {
            Write-Host "  $ServiceName n'a PAS été démarré." -ForegroundColor Red
        }
        Write-Host "  Sur la VM, créez-le depuis le template puis renseignez SQLPassword :" -ForegroundColor Yellow
        Write-Host "    cp $RemotePath/config.Production.example.json $RemotePath/config.Production.json" -ForegroundColor Yellow
        Write-Host "    nano $RemotePath/config.Production.json" -ForegroundColor Yellow
        Write-Host "  puis relancez .\deploy.ps1 -Target $Name" -ForegroundColor Yellow
        Write-Host ""
        return
    }

    if ($NoService) {
        Write-Host "==> $Name déployé. Lancement manuel attendu (CLI one-shot) :" -ForegroundColor Cyan
        Write-Host "    ssh $VM_USER@$VM_HOST" -ForegroundColor DarkGray
        Write-Host "    cd $RemotePath && DOTNET_ENVIRONMENT=Production ./$ExeName --dry-run" -ForegroundColor DarkGray
        return
    }

    Write-Host "==> Démarrage de $ServiceName..." -ForegroundColor Cyan
    ssh "$VM_USER@$VM_HOST" "sudo systemctl start $ServiceName"

    Start-Sleep -Seconds 2
    Write-Host "==> Statut :" -ForegroundColor Cyan
    ssh "$VM_USER@$VM_HOST" "sudo systemctl status $ServiceName --no-pager -l | head -20"
}

function Push-DofusClient {
    Write-Host ""
    Write-Host "==> Push client Dofus (data/common + data/i18n, ~192 MB)..." -ForegroundColor Cyan

    $clientSrc = Join-Path $REPO_ROOT "Ressources\Dofus"
    $commonSrc = Join-Path $clientSrc "data\common"
    $i18nSrc   = Join-Path $clientSrc "data\i18n"

    if (-not (Test-Path $commonSrc)) { throw "Source client introuvable : $commonSrc" }
    if (-not (Test-Path $i18nSrc))   { throw "Source client introuvable : $i18nSrc" }

    $tarPath = Join-Path $env:TEMP "deindeiru-client-data.tar.gz"
    if (Test-Path $tarPath) { Remove-Item -Force $tarPath }

    try {
        Write-Host "    Archiving data/common + data/i18n -> $tarPath" -ForegroundColor DarkGray
        # bsdtar Windows accepte -C pour chdir. On archive avec des chemins
        # relatifs (data/common, data/i18n) pour qu'à l'extraction sur la VM
        # tout atterrisse sous /opt/deindeiru/client-data/data/{common,i18n}/.
        tar -czf $tarPath -C $clientSrc data/common data/i18n
        if ($LASTEXITCODE -ne 0) { throw "tar a échoué (code $LASTEXITCODE)" }

        $tarSize = (Get-Item $tarPath).Length / 1MB
        Write-Host ("    Archive : {0:N1} MB" -f $tarSize) -ForegroundColor DarkGray

        Write-Host "    Upload  : scp -> /tmp/deindeiru-client-data.tar.gz" -ForegroundColor DarkGray
        scp $tarPath "${VM_USER}@${VM_HOST}:/tmp/deindeiru-client-data.tar.gz"
        if ($LASTEXITCODE -ne 0) { throw "scp a échoué (code $LASTEXITCODE)" }

        Write-Host "    Extract : tar -xzf sur la VM dans /opt/deindeiru/client-data/" -ForegroundColor DarkGray
        ssh "$VM_USER@$VM_HOST" "mkdir -p /opt/deindeiru/client-data && tar -xzf /tmp/deindeiru-client-data.tar.gz -C /opt/deindeiru/client-data && rm -f /tmp/deindeiru-client-data.tar.gz"
        if ($LASTEXITCODE -ne 0) { throw "extraction distante a échoué (code $LASTEXITCODE)" }

        Write-Host "==> Client Dofus poussé." -ForegroundColor Green
    }
    finally {
        # Cleanup local toujours, même si une étape distante a planté.
        if (Test-Path $tarPath) { Remove-Item -Force $tarPath }
    }
}

Write-Host "Deploiement vers $VM_HOST" -ForegroundColor Green

if ($Target -in "auth", "all") {
    Deploy-Component -Name "auth" `
        -ProjectPath "$REPO_ROOT\Sources\Servers\Giny.Auth\Giny.Auth.csproj" `
        -RemotePath "/opt/deindeiru/auth" `
        -ServiceName "deindeiru-auth" `
        -ExeName "Giny.Auth" `
        -RequiredSwfs @("AuthPatch.swf")
}

if ($Target -in "world", "all") {
    Deploy-Component -Name "world" `
        -ProjectPath "$REPO_ROOT\Sources\Servers\Giny.World\Giny.World.csproj" `
        -RemotePath "/opt/deindeiru/world" `
        -ServiceName "deindeiru-world" `
        -ExeName "Giny.World" `
        -RequiredSwfs @("HeroPanel.swf")
}

# Synchronizer hors de 'all' volontairement : opération admin rare (patch Dofus
# ou rebuild de schéma), à demander explicitement pour éviter d'embarquer ~30s
# de publish+scp dans chaque deploy quotidien d'Auth/World.
if ($Target -eq "synchronizer") {
    Deploy-Component -Name "synchronizer" `
        -ProjectPath "$REPO_ROOT\Sources\Sync\Giny.DatabaseSynchronizer\Giny.DatabaseSynchronizer.csproj" `
        -RemotePath "/opt/deindeiru/synchronizer" `
        -ServiceName "" `
        -ExeName "Giny.DatabaseSynchronizer" `
        -NoService
}

if ($PushClient) {
    Push-DofusClient
}

Write-Host "Deploiement termine." -ForegroundColor Green
