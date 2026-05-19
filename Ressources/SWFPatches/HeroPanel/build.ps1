# Compile HeroPanel.swf — RawPatch UI "Mode Héros".
#
# Le SWF est compilé en mode AIR (airglobal.swc fournit toutes les API flash.*)
# car il est chargé dans le runtime AIR du client Dofus.
#
# Deux étapes :
#  1. compc -> hoststubs.swc : les stubs des interfaces du client (Frame,
#     Message, MessageHandler, Prioritizable). Ils sont compilés dans un SWC
#     référencé EN EXTERNE par mxmlc : aucune de ces définitions n'est
#     embarquée dans HeroPanel.swf. À l'exécution, "HeroFrame implements Frame"
#     se résout donc forcément vers la VRAIE interface du client (domaine
#     parent), ce qui garantit que Worker.addFrame(frame:Frame) l'accepte.
#     (Embarquer les stubs via -source-path risquerait, selon le runtime, de
#     créer une interface Frame distincte de celle du client.)
#  2. mxmlc -> HeroPanel.swf : le patch lui-même. airglobal.swc et
#     hoststubs.swc sont tous deux en external-library-path.
#
# Sortie : HeroPanel.swf, recopié dans le dossier SWF du serveur World.

$ErrorActionPreference = "Stop"

function FullPath($p) { return [System.IO.Path]::GetFullPath($p) }

$root  = $PSScriptRoot
$sdk   = FullPath (Join-Path $root "..\..\flexsdk")
$mxmlc = Join-Path $sdk "bin\mxmlc.bat"
$compc = Join-Path $sdk "bin\compc.bat"

$airglobal = Join-Path $sdk  "frameworks\libs\air\airglobal.swc"
$stubs     = Join-Path $root "stubs"
$src       = Join-Path $root "src"
$mainAs    = Join-Path $src  "Main.as"
$hostStubs = Join-Path $root "hoststubs.swc"
$worldSwf  = FullPath (Join-Path $root "..\..\..\Sources\Servers\Giny.World\bin\Debug\net6.0\SWF")
$output    = Join-Path $root "HeroPanel.swf"

if (Test-Path $hostStubs) { Remove-Item $hostStubs -Force }
if (Test-Path $output)    { Remove-Item $output -Force }

# --- 1. Stubs des types du client -> hoststubs.swc (reference externe) ---
Write-Host "[1/2] compc hoststubs.swc ..."
& $compc `
  "-load-config=" `
  "-external-library-path+=$airglobal" `
  "-source-path+=$stubs" `
  "-include-classes" `
  "com.ankamagames.jerakine.messages.Frame" `
  "com.ankamagames.jerakine.messages.Message" `
  "com.ankamagames.jerakine.messages.MessageHandler" `
  "com.ankamagames.jerakine.utils.misc.Prioritizable" `
  "-output=$hostStubs"

if (-not (Test-Path $hostStubs)) {
    Write-Error "Compilation echouee : hoststubs.swc absent."
    exit 1
}

# --- 2. Le patch -> HeroPanel.swf ---
# -load-config= (vide) : aucune config SDK par defaut (tokens {airHome}/
# {playerglobalHome} non resolus dans ce SDK "bin").
# -swf-version=40 : version chargeable sans souci par le runtime AIR du client.
Write-Host "[2/2] mxmlc HeroPanel.swf ..."
& $mxmlc `
  "-load-config=" `
  "-external-library-path+=$airglobal" `
  "-external-library-path+=$hostStubs" `
  "-source-path+=$src" `
  "-swf-version=40" `
  "-warnings=false" `
  "-output=$output" `
  "$mainAs"

if (-not (Test-Path $output)) {
    Write-Error "Compilation echouee : HeroPanel.swf absent."
    exit 1
}

if (-not (Test-Path $worldSwf)) {
    New-Item -ItemType Directory -Force -Path $worldSwf | Out-Null
}
Copy-Item $output (Join-Path $worldSwf "HeroPanel.swf") -Force

Write-Host "OK -> $output"
Write-Host "OK -> $(Join-Path $worldSwf 'HeroPanel.swf')"
