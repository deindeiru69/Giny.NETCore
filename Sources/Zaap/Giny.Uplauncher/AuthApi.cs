using Giny.Core.IO.Configuration;
using Giny.Core.Network;
using Giny.Zaap.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Giny.Uplauncher
{

    public class AuthApi
    {
        private static UplConfig Config => ConfigManager<UplConfig>.Instance;

        private static HttpClient HttpClient = new HttpClient();
        public static string GetRemoteVersion()
        {
            return Http.Get($"{Config.GetSelectedHost().GetApiUri()}/version/launcher");
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
