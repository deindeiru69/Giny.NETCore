# Déploiement Giny — VM cloud Deindeiru-Prod

Déploiement des serveurs **Auth** et **World** sur la VM Hetzner.

## Architecture

| Élément        | Valeur                                            |
|----------------|---------------------------------------------------|
| VM             | Deindeiru-Prod — Ubuntu 26.04 — `178.105.117.203` |
| Domaine        | `deindeiruworld.duckdns.org`                      |
| Auth (jeu)     | port TCP **5555**                                 |
| World (jeu)    | port TCP **5556**                                 |
| API Auth HTTP  | `127.0.0.1:9001` — **interne**, jamais exposée directement |
| Reverse proxy  | Caddy sur **443** (HTTPS) → `127.0.0.1:9001`      |
| MariaDB        | `localhost:3306`, base `deindeiruworld`, user `deindeiru` |
| Arborescence   | `/opt/deindeiru/{auth,world,logs,backups}`        |
| Services       | `deindeiru-auth`, `deindeiru-world`, `caddy` (systemd) |

L'API Auth (création de compte / login du launcher) est bindée en local sur
`127.0.0.1:9001` et publiée par **Caddy** en HTTPS sur `443` :
TLS automatique (Let's Encrypt) et rate limiting par IP. Le launcher s'y
connecte via `https://deindeiruworld.duckdns.org`.

Les serveurs sont publiés **self-contained** : aucun runtime .NET n'est requis
sur la VM. Le mode `PublishSingleFile` est **désactivé** : `MySql.Data` (driver
Oracle, version pinned ici) appelle `Assembly.CodeBase` au chargement, ce que
le bundle single-file de .NET 6 ne supporte pas — l'Auth crashait au démarrage
sur Linux. Le publish produit donc un dossier multi-fichiers classique (le
runtime .NET reste embarqué, comportement self-contained inchangé). Réactivation
possible une fois `MySql.Data` migré vers `MySqlConnector`.

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
6. **Reverse proxy** — installer Caddy (voir « Reverse proxy (Caddy) » ci-dessous).
7. **Pare-feu Hetzner** — ouvrir en TCP entrant : **80**, **443**, **5555**,
   **5556**. **Ne PAS ouvrir 9001** : l'API n'est jointe que par le proxy
   local. Le port **80** sert au challenge ACME (Let's Encrypt) et à la
   redirection HTTP→HTTPS.

## Reverse proxy (Caddy)

L'API Auth est exposée par **Caddy** : HTTPS automatique (Let's Encrypt) et
rate limiting par IP. L'API elle-même reste bindée sur `127.0.0.1:9001`
(`APIHost` = `127.0.0.1` dans `auth/config.Production.json`).

Prérequis : le domaine `deindeiruworld.duckdns.org` doit pointer (A record
DuckDNS) vers l'IP de la VM, et les ports **80**+**443** être ouverts.

1. **Installer Caddy** (dépôt officiel) :
   ```bash
   sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
   curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
     | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
   curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
     | sudo tee /etc/apt/sources.list.d/caddy-stable.list
   sudo apt update && sudo apt install -y caddy
   ```
2. **Ajouter le plugin de rate limiting** (Caddy standard ne sait pas
   rate-limiter) :
   ```bash
   sudo caddy add-package github.com/mholt/caddy-ratelimit
   ```
   > Après un `apt upgrade` de Caddy, ré-exécuter cette commande (la mise à
   > jour apt remplace le binaire et perd le plugin).
3. **Installer le Caddyfile** (livré dans `deploy/caddy/Caddyfile`) :
   ```bash
   sudo cp /tmp/deploy/caddy/Caddyfile /etc/caddy/Caddyfile
   sudo systemctl restart caddy
   ```
   Caddy obtient alors automatiquement le certificat TLS au premier
   démarrage.

**Rate limits appliqués :** `/account/auth` 5 req/min/IP,
`/account/register` 2 req/h/IP. Au-delà, Caddy répond `429 Too Many Requests`.

**Renouvellement du certificat :** entièrement automatique (Caddy renouvelle
~30 jours avant expiration). Aucune tâche cron, rien à faire.

**Tester l'API à travers le proxy :**
```bash
curl https://deindeiruworld.duckdns.org/version/launcher      # -> 1.0.0
ssh deindeiru@deindeiruworld.duckdns.org "tail -f /opt/deindeiru/logs/caddy.access.log"
```

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
| Crash au boot : `NotSupportedException` mentionnant `Assembly.CodeBase` | `PublishSingleFile` réactivé par erreur — `MySql.Data` ne le supporte pas. Repasser `deploy.ps1` en multi-fichiers (cf. en-tête du fichier). |
| Erreur de connexion MariaDB | Vérifier `SQLHost/SQLUser/SQLPassword/SQLDBName` dans `config.Production.json` ; base `deindeiruworld` importée. |
| Le client ne joint pas le World | `PublicHost` doit valoir `deindeiruworld.duckdns.org` dans `world/config.Production.json` ; port 5556 ouvert au pare-feu. |
| Le client reste bloqué après l'écran serveur | L'Auth annonce le World via `PublicHost` : vérifier que le World a bien fait son handshake IPC (`logs/auth.log`). |
| L'Auth plante à la connexion d'un client | `SWF/AuthPatch.swf` manquant dans `/opt/deindeiru/auth/`. |
| `scp`/`ssh` refusés | Clé `~/.ssh/id_ed25519` non autorisée sur la VM pour `deindeiru`. |
| Caddy ne récupère pas le certificat | Domaine DuckDNS pointant bien vers la VM ? Ports **80** et **443** ouverts ? `sudo journalctl -u caddy -e`. |
| `caddy: unknown directive rate_limit` | Plugin non installé : `sudo caddy add-package github.com/mholt/caddy-ratelimit` puis `sudo systemctl restart caddy`. |
| Launcher : « Trop de requêtes » / login en échec en boucle | Rate limit atteint (429) — attendre la fenêtre (1 min pour le login). |
| Le launcher ne joint pas l'API | `ApiBaseUrl` du launcher doit valoir `https://deindeiruworld.duckdns.org` ; tester `curl https://.../version/launcher`. |
