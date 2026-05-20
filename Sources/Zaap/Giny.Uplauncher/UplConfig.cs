using Giny.Core.IO.Configuration;
using Giny.Zaap.Accounts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Giny.Uplauncher
{
    public class UplConfig : IConfigFile, IAccountProvider
    {
        public const string Filepath = "config.json";
        public List<WebAccount> Accounts
        {
            get;
            set;
        } = new List<WebAccount>();

        private int AccountIndex
        {
            get;
            set;
        } = 0;

        public string Wallpaper
        {
            get;
            set;
        } = "https://i.imgur.com/9eMnv7A.jpeg";

        public string LocalVersion
        {
            get;
            set;
        } = "1.0.0";

        public bool StartAllInstances
        {
            get;
            set;
        }

        // Default cohérent avec le layout de l'installeur Inno Setup
        // (DeindeiruInstaller.iss l.43) : {app}\Giny.Uplauncher.exe à la racine
        // et {app}\client\ pour le client Dofus. L'installeur écrit ce path
        // explicitement dans config.json à l'install ; ce default sert quand
        // config.json est absent (dev local, install corrompu, run manuel).
        public string ClientPath
        {
            get;
            set;
        } = Path.Combine(AppContext.BaseDirectory, "client");


        // Hôte par défaut : serveur cloud Deindeiru-Prod (VM Hetzner +
        // reverse proxy Caddy en HTTPS pour l'API). Premier lancement / fresh
        // install : l'utilisateur n'a rien à configurer, ça pointe direct
        // sur la prod. L'installeur Inno Setup réécrit aussi config.json
        // avec ces valeurs (CurStepChanged), donc les deux chemins convergent.
        public List<AuthHost> Hosts = new List<AuthHost>()
        {
            new AuthHost
            {
                Ip = "deindeiruworld.duckdns.org",
                Port = 5555,
                ApiPort = 9001,
                ApiBaseUrl = "https://deindeiruworld.duckdns.org",
            },
        };
        public int HostIndex
        {
            get;
            set;
        }


        public WebAccount GetSelectedAccount()
        {
            if (Accounts.Count == 0)
            {
                return null;
            }
            if (AccountIndex > Accounts.Count - 1)
            {
                AccountIndex = 0;
            }

            return Accounts[AccountIndex];
        }

        public AuthHost GetSelectedHost()
        {
            if (Hosts.Count == 0)
            {
                return null;
            }
            // Bug historique : la borne était Accounts.Count - 1 (copié-collé
            // de GetSelectedAccount). En pratique, HostIndex pouvait pointer
            // hors-bornes dès qu'on avait plus de hosts que de comptes.
            if (HostIndex > Hosts.Count - 1)
            {
                HostIndex = 0;
            }

            return Hosts[HostIndex];
        }

        public void SelectHost(AuthHost host)
        {
            HostIndex = Hosts.IndexOf(host);
        }


        public void SelectAccount(WebAccount acc)
        {
            AccountIndex = Accounts.IndexOf(acc);
        }

        public int GetIndex(WebAccount acc)
        {
            return Accounts.IndexOf(acc);
        }
        public void OnCreated()
        {

        }

        public void OnLoaded()
        {

        }

        public WebAccount GetAccount(int instanceId)
        {
            return Accounts[instanceId];
        }
    }

    public class AuthHost
    {
        public string Ip
        {
            get;
            set;
        }

        public int Port
        {
            get;
            set;
        }

        public int ApiPort
        {
            get;
            set;
        }

        /// <summary>
        /// URL de base de l'API Auth. Renseignée en production
        /// (ex. "https://deindeiruworld.duckdns.org" — via le reverse proxy
        /// Caddy), elle prime alors sur Ip:ApiPort. Laissée vide en
        /// développement : on retombe sur http://Ip:ApiPort en local.
        /// </summary>
        public string ApiBaseUrl
        {
            get;
            set;
        }

        public string GetApiUri()
        {
            if (!string.IsNullOrWhiteSpace(ApiBaseUrl))
                return ApiBaseUrl.TrimEnd('/');

            return $"http://{Ip}:{ApiPort}";
        }
        public string GetClientUri()
        {
            return $"JMBouftou:{Ip}:{Port}";
        }

    }
}
