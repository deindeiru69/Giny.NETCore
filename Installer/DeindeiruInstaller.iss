; Installeur "Serveur des Gato" — Inno Setup 6, francais uniquement.
; Architecture cloud : le serveur est sur Internet (deindeiruworld.duckdns.org),
; plus aucun VPN. L'installeur packe le launcher + le client Dofus 2.68 complet.
;
; Installation par utilisateur (aucune élévation) : le launcher écrit son
; config.json et le client Dofus écrit dans son dossier au runtime.
;
; Compilation : voir build.ps1.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{C8F4A2E1-7D3B-4F9A-A1E6-2B5C8D0F3A47}
AppName=Serveur des Gato
AppVersion={#AppVersion}
AppPublisher=Damien
DefaultDirName={localappdata}\Programs\ServeurDesGato
DefaultGroupName=Serveur des Gato
OutputBaseFilename=ServeurDesGato-Setup-v{#AppVersion}
OutputDir=output
PrivilegesRequired=lowest
Compression=lzma2/ultra64
SolidCompression=yes
DiskSpanning=yes
DiskSliceSize=2100000000
LZMAUseSeparateProcess=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=Assets\giny.ico
UninstallDisplayIcon={app}\Giny.Uplauncher.exe
DisableDirPage=auto
DisableProgramGroupPage=yes
ShowLanguageDialog=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le bureau"; GroupDescription: "Raccourcis :"; Flags: checkedonce

[Files]
Source: "payload\launcher\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "payload\client-dofus\*"; DestDir: "{app}\client"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Serveur des Gato"; Filename: "{app}\Giny.Uplauncher.exe"; IconFilename: "{app}\Giny.Uplauncher.exe"
Name: "{autodesktop}\Serveur des Gato"; Filename: "{app}\Giny.Uplauncher.exe"; IconFilename: "{app}\Giny.Uplauncher.exe"; Tasks: desktopicon
Name: "{group}\Désinstaller Serveur des Gato"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\Giny.Uplauncher.exe"; Description: "Lancer Serveur des Gato maintenant"; Flags: nowait postinstall skipifsilent

[Code]
// Écrit le config.json du launcher après installation : ClientPath pointe
// vers le client packagé, Hosts pointe vers le serveur cloud. Non réécrit
// s'il existe déjà (réinstallation : on préserve les comptes enregistrés).
procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigFile, ClientDir, Json: String;
begin
  if CurStep = ssPostInstall then
  begin
    ConfigFile := ExpandConstant('{app}\config.json');
    if not FileExists(ConfigFile) then
    begin
      ClientDir := ExpandConstant('{app}\client');
      StringChangeEx(ClientDir, '\', '\\', True);
      Json :=
        '{' + #13#10 +
        '  "Accounts": [],' + #13#10 +
        '  "Wallpaper": "https://i.imgur.com/9eMnv7A.jpeg",' + #13#10 +
        '  "LocalVersion": "1.0.0",' + #13#10 +
        '  "StartAllInstances": false,' + #13#10 +
        '  "ClientPath": "' + ClientDir + '",' + #13#10 +
        '  "Hosts": [ { "Ip": "deindeiruworld.duckdns.org", "Port": 5555, "ApiPort": 443, "ApiBaseUrl": "https://deindeiruworld.duckdns.org" } ],' + #13#10 +
        '  "HostIndex": 0' + #13#10 +
        '}';
      SaveStringToFile(ConfigFile, Json, False);
    end;
  end;
end;
