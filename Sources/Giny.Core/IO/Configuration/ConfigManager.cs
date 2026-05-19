using Giny.Core.DesignPattern;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Giny.Core.IO.Configuration
{
    public class ConfigManager<T> : Singleton<ConfigManager<T>> where T : class, IConfigFile
    {
        public static T Instance
        {
            get;
            set;
        }

        public static void Load(string filepath)
        {
            if (File.Exists(filepath))
            {
                try
                {

                    Instance = Json.Deserialize<T>(File.ReadAllText(filepath));
                    Instance.OnLoaded();

                }
                catch
                {
                    Logger.Write("Unable to load configuration. Recreating it", Channels.Warning);
                    CreateConfig(filepath);
                }

            }
            else
            {
                CreateConfig(filepath);
            }

            ApplyProductionOverlay(filepath);
        }

        /// <summary>
        /// Quand DOTNET_ENVIRONMENT=Production, superpose les valeurs de
        /// "&lt;nom&gt;.Production.json" (ex. config.Production.json) par-dessus la
        /// configuration de base. Seules les clés présentes dans le fichier de
        /// production sont écrasées ; le reste garde les valeurs de base. Ce
        /// fichier n'est jamais versionné (il contient les secrets de prod).
        /// </summary>
        private static void ApplyProductionOverlay(string filepath)
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            if (!string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase))
                return;

            var productionPath = GetProductionPath(filepath);

            if (!File.Exists(productionPath))
            {
                Logger.Write($"DOTNET_ENVIRONMENT=Production mais '{productionPath}' est introuvable. " +
                    "La configuration de base est utilisée telle quelle.", Channels.Warning);
                return;
            }

            if (Instance == null)
                return;

            try
            {
                JsonConvert.PopulateObject(File.ReadAllText(productionPath), Instance);
                Logger.Write($"Configuration de production appliquée ('{productionPath}').");
            }
            catch (Exception ex)
            {
                Logger.Write($"Impossible d'appliquer '{productionPath}' : {ex.Message}", Channels.Critical);
            }
        }

        /// <summary>
        /// "config.json" -&gt; "config.Production.json".
        /// </summary>
        private static string GetProductionPath(string filepath)
        {
            var directory = Path.GetDirectoryName(filepath);
            var name = Path.GetFileNameWithoutExtension(filepath) + ".Production" + Path.GetExtension(filepath);

            return string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name);
        }

        private static void CreateConfig(string filepath)
        {
            var result = (IConfigFile)Activator.CreateInstance(typeof(T))!;

            result.OnCreated();

            Instance = (T)result;

            Save(filepath);


        }
        public static void Save(string filepath)
        {
            File.WriteAllText(filepath, Json.Serialize(Instance));
        }
    }
}
