# Déploiement Giny — VM cloud Deindeiru-Prod

Déploiement des serveurs **Auth** et **World** sur la VM Hetzner.

## Architecture

| Élément        | Valeur                                            |
|----------------|---------------------------------------------------|
| VM             | Deindeiru-Prod — Ubuntu 26.04 — `178.105.117.203` |
| Domaine        | `deindeiruworld.duckdns.org`                      |
| Auth           | port TCP **5555**                                 |
| World          | port TCP **5556**                                 |
| API Auth       | port TCP **9001** (le launcher s'y connecte)      |
| MariaDB        | `localhost:3306`, base `deindeiruworld`, user `deindeiru` |
| Arborescence   | `/opt/deindeiru/{auth,world,logs,backups}`        |
| Services       | `deindeiru-auth`, `deindeiru-world` (systemd)     |

Les serveurs sont publiés **self-contained** : aucun runtime .NET n'est requis
sur la VM.

### Configuration

Chaque serveur charge `config.json` (valeurs DEV par défaut) puis, quand
`DOTNET_ENVIRONMENT=Production` (défini par les unités systemd), superpose
`config.Production.json` s'il existe. Seules les clés présentes dans le
fichier de production sont écrasées.

- `config.Production.json` — **jamais versionné** (contient `SQLPassword`).
  Créé sur la VM à partir du template.
- `config.Production.example.json` — template versionné, livré dans le publish.

Le World annonce sa `PublicHost` (`deindeiruworld.duckdns.org`) à l'Auth via
le handshake IPC ; c'est cette adresse que l'Auth transmet aux clients. Le
champ `Host` ne sert qu'au bind local (`0.0.0.0` en production).

## Premier déploiement

Prérequis sur la VM : MariaDB installé, base `deindeiruworld` créée et
schéma importé, utilisateur `deindeiru` créé. Clé SSH `~/.ssh/id_ed25519`
(Windows) autorisée pour `deindeiru` sur la VM.

1. **Installer les services** — copier `deploy/` sur la VM et lancer l'install :
   ```powershell
   scp -r deploy deindeiru@deindeiruworld.duckdns.org:/tmp/
   ssh deindeiru@deindeiruworld.duckdns.org "bash /tmp/deploy/install-on-vm.sh"
   ```
2. **Envoyer les binaires** (depuis Windows) :
   ```powershell
   cd deploy
   .\deploy.ps1
   ```
   Les services ne démarrent pas encore (`config.Production.json` absent —
   le script l'indique explicitement).
3. **Créer les configs de production** sur la VM :
   ```bash
   cp /opt/deindeiru/auth/config.Production.example.json  /opt/deindeiru/auth/config.Production.json
   cp /opt/deindeiru/world/config.Production.example.json /opt/deindeiru/world/config.Production.json
   nano /opt/deindeiru/auth/config.Production.json     # renseigner SQLPassword
   nano /opt/deindeiru/world/config.Production.json    # renseigner SQLPassword
   ```
4. **Patch SWF de l'Auth** — déposer `AuthPatch.swf` dans `/opt/deindeiru/auth/SWF/`
   (l'Auth en a besoin dès la connexion d'un client).
5. **Relancer le déploiement** — démarre les services :
   ```powershell
   .\deploy.ps1
   ```
6. **Pare-feu Hetzner** — ouvrir en TCP entrant : **5555**, **5556**, **9001**.

## Déployer une mise à jour

```powershell
cd deploy
.\deploy.ps1                 # auth + world
.\deploy.ps1 -Target world   # World seulement
.\deploy.ps1 -SkipBuild      # redéploie le publish/ existant sans rebuild
```

`deploy.ps1` arrête le service, envoie les binaires, vérifie la présence de
`config.Production.json`, puis redémarre le service et affiche son statut.

## Exploitation

Voir les logs en direct :
```bash
ssh deindeiru@deindeiruworld.duckdns.org "tail -f /opt/deindeiru/logs/world.log"
ssh deindeiru@deindeiruworld.duckdns.org "tail -f /opt/deindeiru/logs/world.error.log"
```

Redémarrer un service :
```bash
ssh deindeiru@deindeiruworld.duckdns.org "sudo systemctl restart deindeiru-world"
```

Statut :
```bash
ssh deindeiru@deindeiruworld.duckdns.org "sudo systemctl status deindeiru-auth deindeiru-world --no-pager"
```

## Sauvegarde / restauration BDD

Sauvegarde :
```bash
mysqldump -u deindeiru -p deindeiruworld > /opt/deindeiru/backups/deindeiruworld-$(date +%F).sql
```

Restauration :
```bash
sudo systemctl stop deindeiru-world deindeiru-auth
mysql -u deindeiru -p deindeiruworld < /opt/deindeiru/backups/deindeiruworld-AAAA-MM-JJ.sql
sudo systemctl start deindeiru-auth && sleep 3 && sudo systemctl start deindeiru-world
```

## Dépannage

| Symptôme | Piste |
|----------|-------|
| `deploy.ps1` : « config.Production.json absent » | Créer le fichier depuis le `.example.json` sur la VM, renseigner `SQLPassword`. |
| Le service ne démarre pas | `systemctl status deindeiru-world`, puis `logs/world.error.log`. |
| Erreur de connexion MariaDB | Vérifier `SQLHost/SQLUser/SQLPassword/SQLDBName` dans `config.Production.json` ; base `deindeiruworld` importée. |
| Le client ne joint pas le World | `PublicHost` doit valoir `deindeiruworld.duckdns.org` dans `world/config.Production.json` ; port 5556 ouvert au pare-feu. |
| Le client reste bloqué après l'écran serveur | L'Auth annonce le World via `PublicHost` : vérifier que le World a bien fait son handshake IPC (`logs/auth.log`). |
| L'Auth plante à la connexion d'un client | `SWF/AuthPatch.swf` manquant dans `/opt/deindeiru/auth/`. |
| `scp`/`ssh` refusés | Clé `~/.ssh/id_ed25519` non autorisée sur la VM pour `deindeiru`. |
