# Installeur « Serveur des Gato »

Génère un `.exe` distribuable aux amis : ils l'exécutent, suivent
l'assistant (en français), obtiennent un raccourci bureau, lancent le
launcher, créent un compte et jouent.

Le serveur est hébergé sur Internet (`deindeiruworld.duckdns.org`) —
**aucun VPN n'est nécessaire**.

## Contenu packagé

- Le launcher Giny (`Giny.Uplauncher`, publié self-contained — aucun
  runtime .NET à installer).
- Le client Dofus 2.68.0.0 complet (`Ressources/Dofus`).
- Le `config.xml` du client patché : `connection.host = JMBouftou:deindeiruworld.duckdns.org:5555`.
- Un `config.json` launcher écrit à l'installation (ClientPath -> client
  packagé, Hosts -> serveur cloud).

L'installation se fait **par utilisateur** (`%LocalAppData%\Programs\ServeurDesGato`),
sans élévation : le launcher et le client peuvent écrire dans leur dossier.

## Prérequis de build

- Windows 10/11 x64
- .NET SDK capable de cibler `net6.0-windows`
- Inno Setup 6 : https://jrsoftware.org/isdl.php

## Build

```powershell
cd Installer
.\build.ps1
# ou avec une version personnalisée :
.\build.ps1 -Version "1.0.1"
```

L'installeur est généré dans `Installer/output/`.

## Changer l'IP / le host serveur

```powershell
.\build.ps1 -ServerHost "JMBouftou:nouveau.duckdns.org:5555"
```

Le format est `JMBouftou:<hôte>:<port Auth>`. Penser aussi à mettre à jour
le host dans la section `[Code]` de `DeindeiruInstaller.iss` (le `config.json`
du launcher).

## SmartScreen

L'installeur n'étant pas signé, Windows SmartScreen affiche un
avertissement. Les utilisateurs cliquent « Informations complémentaires »
→ « Exécuter quand même ». C'est normal.

## Crédits

Icon by Icons8 (https://icons8.com)
