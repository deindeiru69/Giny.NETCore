using Giny.Core;
using Giny.Core.IO;
using Giny.Core.IO.Configuration;
using Giny.IO;
using Giny.IO.D2I;
using Giny.IO.D2O;
using Giny.IO.D2OClasses;
using Giny.IO.D2P;
using Giny.ORM;
using Giny.ORM.Interfaces;
using Giny.ORM.IO;
using Giny.World.Managers.Entities.Look;
using Giny.World.Records;
using Giny.World.Records.Achievements;
using Giny.World.Records.Breeds;
using Giny.World.Records.Challenges;
using Giny.World.Records.Characters;
using Giny.World.Records.Effects;
using Giny.World.Records.Items;
using Giny.World.Records.Jobs;
using Giny.World.Records.Maps;
using Giny.World.Records.Monsters;
using Giny.World.Records.Npcs;
using Giny.World.Records.Quests;
using Giny.World.Records.Spells;
using Giny.World.Records.Tinsel;
using MySql.Data.MySqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Giny.DatabaseSynchronizer
{
    class Program
    {
        // Liste des records D2O à drop+recreate quand SyncD2O = true.
        // Ordonné comme dans la version pré-refacto pour ne pas changer l'ordre
        // d'exécution (FK / dépendances implicites lors du drop).
        private static readonly Type[] D2O_RECORDS = new[]
        {
            typeof(RecipeRecord),
            typeof(SubareaRecord),
            typeof(AreaRecord),
            typeof(ItemSetRecord),
            typeof(BreedRecord),
            typeof(ExperienceRecord),
            typeof(HeadRecord),
            typeof(EffectRecord),
            typeof(MapScrollActionRecord),
            typeof(SpellRecord),
            typeof(SpellVariantRecord),
            typeof(ItemRecord),
            typeof(QuestStepRecord),
            typeof(QuestStepRewardRecord),
            typeof(QuestObjectiveRecord),
            typeof(QuestRecord),
            typeof(SpellStateRecord),
            typeof(WeaponRecord),
            typeof(MapReferenceRecord),
            typeof(LivingObjectRecord),
            typeof(EmoteRecord),
            typeof(SpellLevelRecord),
            typeof(OrnamentRecord),
            typeof(TitleRecord),
            typeof(MonsterRecord),
            typeof(SkillRecord),
            typeof(DungeonRecord),
            typeof(MapPositionRecord),
            typeof(NpcRecord),
            typeof(SpellBombRecord),
            typeof(ChallengeRecord),
            typeof(AchievementRewardRecord),
            typeof(AchievementRecord),
            typeof(AchievementObjectiveRecord),
        };

        private static readonly Type[] MAP_RECORDS = new[] { typeof(MapRecord) };

        static int Main(string[] args)
        {
            var knownArgs = new HashSet<string> { "--help", "-h", "--dry-run", "--yes", "-y" };
            foreach (var arg in args)
            {
                if (!knownArgs.Contains(arg))
                {
                    Console.Error.WriteLine($"Unknown argument: {arg}");
                    PrintHelp();
                    return 2;
                }
            }

            bool showHelp = args.Contains("--help") || args.Contains("-h");
            bool dryRun = args.Contains("--dry-run");
            bool autoYes = args.Contains("--yes") || args.Contains("-y");

            if (showHelp)
            {
                PrintHelp();
                return 0;
            }

            Logger.DrawLogo();

            // Garde-fou pré-load : si l'opérateur a demandé l'overlay Production
            // mais n'a pas copié le template, on échoue tôt avec un message qui
            // pointe sur la procédure exacte. Sinon ConfigManager auto-créerait
            // un config.json de défauts et le code planterait plus tard sur des
            // valeurs vides moins claires à diagnostiquer.
            var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            if (string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase)
                && !File.Exists("config.Production.json"))
            {
                Console.Error.WriteLine("Missing config.Production.json. Copy config.Production.example.json to config.Production.json and edit SQLPassword.");
                return 1;
            }

            // 1. Config
            ConfigManager<SyncConfig>.Load("config.json");
            SyncConfig config = ConfigManager<SyncConfig>.Instance;

            if (string.IsNullOrWhiteSpace(config.ClientPath))
            {
                Console.Error.WriteLine("ClientPath is empty in config. Set it in config.json or config.Production.json.");
                return 1;
            }

            // 2. D2I (lecture client, non destructif)
            D2IManager.Initialize(Path.Combine(config.ClientPath, ClientConstants.i18nPath));

            // 3. DB connexion (lecture, pas encore de drop)
            DatabaseManager.Instance.Initialize(
                Assembly.GetAssembly(typeof(BreedRecord)),
                config.SQLHost, config.SQLDBName, config.SQLUser, config.SQLPassword, config.SQLPort);

            try
            {
                DatabaseManager.Instance.UseProvider();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unable to connect to {config.SQLUser}@{config.SQLHost}:{config.SQLPort}/{config.SQLDBName} : {ex.Message}");
                return 1;
            }

            // 4. Récap (toujours affiché, dry-run ou pas)
            PrintRecap(config, dryRun);

            if (dryRun)
            {
                Logger.Write("Dry-run : no action taken.", Channels.Info);
                return 0;
            }

            // 5. Confirmation interactive (sauf --yes)
            if (!autoYes)
            {
                Console.Write("Type 'YES' (uppercase) to proceed: ");
                var input = Console.ReadLine();
                if (input != "YES")
                {
                    Logger.Write("Aborted by user.", Channels.Info);
                    return 0;
                }
            }

            // 6. Exécution effective
            Logger.Write("Starting synchronization...", Channels.Info);

            if (config.SyncD2O)
            {
                foreach (var recordType in D2O_RECORDS)
                {
                    DatabaseManager.Instance.DropTableIfExists(recordType);
                }
            }

            if (config.SyncMaps)
            {
                foreach (var recordType in MAP_RECORDS)
                {
                    DatabaseManager.Instance.DropTableIfExists(recordType);
                }
            }

            DatabaseManager.Instance.CreateAllTablesIfNotExists();

            D2OSynchronizer.Synchronize();

            MapSynchronizer.Synchronize();

            Logger.WriteColor1("Build finished.");
            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Giny.DatabaseSynchronizer — drops & rebuilds World DB tables from the Dofus client D2O files.");
            Console.WriteLine();
            Console.WriteLine("Usage: Giny.DatabaseSynchronizer [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --dry-run        Show the recap (target DB, ClientPath, tables to drop with row counts) and exit.");
            Console.WriteLine("                   No DB writes, no D2O reads beyond what's needed for the recap.");
            Console.WriteLine("  --yes, -y        Bypass the interactive 'Type YES to proceed' confirmation prompt.");
            Console.WriteLine("                   Intended for CI / automation. NEVER use without prior dry-run inspection.");
            Console.WriteLine("                   Ignored if --dry-run is also passed.");
            Console.WriteLine("  --help, -h       Show this help and exit.");
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine("  Reads config.json next to the executable. If DOTNET_ENVIRONMENT=Production,");
            Console.WriteLine("  overlays config.Production.json on top (production secrets, never versioned).");
            Console.WriteLine("  See config.Production.example.json for the schema.");
            Console.WriteLine();
            Console.WriteLine("WARNING: with SyncD2O=true, this drops & recreates 34 tables of the target DB.");
            Console.WriteLine("Run --dry-run first against any DB you do not want to lose.");
        }

        private static void PrintRecap(SyncConfig config, bool dryRun)
        {
            Logger.Write("=== Synchronizer recap ===", Channels.Info);
            Logger.Write($"  Target DB        : {config.SQLUser}@{config.SQLHost}:{config.SQLPort}/{config.SQLDBName}", Channels.Info);
            Logger.Write($"  ClientPath       : {config.ClientPath}", Channels.Info);
            Logger.Write($"  SyncD2O          : {config.SyncD2O}", Channels.Info);
            Logger.Write($"  SyncMaps         : {config.SyncMaps}", Channels.Info);
            Logger.Write($"  Mode             : {(dryRun ? "DRY-RUN" : "EXECUTE")}", Channels.Info);

            if (config.SyncD2O)
            {
                Logger.Write($"Tables that will be DROPPED + recreated (D2O, {D2O_RECORDS.Length}):", Channels.Info);
                foreach (var recordType in D2O_RECORDS)
                {
                    var tableName = TableManager.Instance.GetDefinition(recordType).TableAttribute.TableName;
                    var count = TryCountTable(tableName);
                    var countDisplay = count.HasValue ? count.Value.ToString("N0") + " rows" : "table not found";
                    Logger.Write($"    {tableName,-35} ({countDisplay})", Channels.Info);
                }
            }

            if (config.SyncMaps)
            {
                Logger.Write($"Tables that will be DROPPED + recreated (Maps, {MAP_RECORDS.Length}):", Channels.Info);
                foreach (var recordType in MAP_RECORDS)
                {
                    var tableName = TableManager.Instance.GetDefinition(recordType).TableAttribute.TableName;
                    var count = TryCountTable(tableName);
                    var countDisplay = count.HasValue ? count.Value.ToString("N0") + " rows" : "table not found";
                    Logger.Write($"    {tableName,-35} ({countDisplay})", Channels.Info);
                }
            }

            if (!config.SyncD2O && !config.SyncMaps)
            {
                Logger.Write("  Both SyncD2O and SyncMaps are false — nothing to do.", Channels.Warning);
            }
        }

        private static long? TryCountTable(string tableName)
        {
            try
            {
                using (var cmd = new MySqlCommand($"SELECT COUNT(*) FROM `{tableName}`", DatabaseManager.Instance.UseProvider()))
                {
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
