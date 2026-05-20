using Giny.Core.IO;
using Giny.Core.IO.Configuration;
using Giny.Core.Network;
using Giny.Zaap.Accounts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Giny.Uplauncher
{

    public class AuthApi
    {
        private static UplConfig Config => ConfigManager<UplConfig>.Instance;

        // Timeout généreux : les D2I peuvent atteindre 50 MB et certains
        // joueurs ont des connexions modestes (ADSL ou cellulaire). 5 min
        // couvre largement le cas pessimiste sans bloquer indéfiniment si
        // le serveur cesse de répondre.
        private static HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        public static string GetRemoteVersion()
        {
            return Http.Get($"{Config.GetSelectedHost().GetApiUri()}/version/launcher");
        }

        /// <summary>
        /// Récupère le manifeste des fichiers client (data/i18n/ + data/common/)
        /// depuis l'Auth API. Renvoie null si le réseau échoue ou si le JSON
        /// est mal formé — le caller traite ça comme une erreur de sync.
        /// </summary>
        public static ManifestResponse? GetClientManifest()
        {
            try
            {
                var json = Http.Get($"{Config.GetSelectedHost().GetApiUri()}/client/manifest");
                return Json.Deserialize<ManifestResponse>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Télécharge un fichier client (servi par Caddy file_server sous
        /// /client/&lt;path&gt;) et l'écrit en streaming sur disque. Streaming
        /// pour éviter de charger les .d2i (jusqu'à 50 MB) en mémoire.
        /// Le caller s'occupe du backup .bak et de la création du parent dir.
        /// </summary>
        public static void DownloadClientFile(string relativePath, string localPath)
        {
            var url = $"{Config.GetSelectedHost().GetApiUri()}/client/{relativePath}";
            using var src = HttpClient.GetStreamAsync(url).GetAwaiter().GetResult();
            using var dst = File.Create(localPath);
            src.CopyTo(dst);
        }
        public static async Task<WebAccount?> Authentificate(string username, string password)
        {
            dynamic request = new
            {
                Username = username,
                Password = password,
            };
            WebAccount? result = await Http.PostAsync<WebAccount?>($"{Config.GetSelectedHost().GetApiUri()}/account/auth", HttpClient, request);

            if (result != null)
            {
                result.Password = password;
            }

            return result;
        }

        public static async Task<RegisterResponse> Register(string username, string password)
        {
            dynamic request = new
            {
                Username = username,
                Password = password,
            };
            RegisterResponse? result = await Http.PostAsync<RegisterResponse?>(
                $"{Config.GetSelectedHost().GetApiUri()}/account/register", HttpClient, request);

            // Réponse vide / erreur réseau : on remonte un échec générique.
            if (result == null)
                return new RegisterResponse { Success = false, Message = "Aucune réponse du serveur." };

            // Conserve le password localement pour les sessions ultérieures
            // (mêmes mécaniques que Authentificate).
            if (result.Success && result.Account != null)
                result.Account.Password = password;

            return result;
        }
    }
}
