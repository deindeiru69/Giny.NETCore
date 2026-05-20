using Giny.IO.D2I;
using Giny.IO.D2O;
using Giny.IO.D2OClasses;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IncarnamSqlGen;

// Generates an idempotent SQL script that populates npc_spawns + npc_actions
// + npc_replies for Incarnam NPCs, using positions reconstructed from Doflex
// (tools/IncarnamScan/output/npc-positions-wiki.json) and dialog message IDs
// read from the D2O Npcs.d2o.
//
// Run :  dotnet run -- [clientPath]
// Output: tools/IncarnamScan/output/incarnam-npcs.sql
//
// Enum encoding : columns Action / Direction / ActionIdentifier are MEDIUMTEXT
// in MySQL but Giny's DatabaseWriter writes them as numeric (Convert.ToInt64
// over the Enum value). DatabaseReader.Enum.Parse accepts either name or
// numeric string, so generated SQL uses the numeric form to match the writer.
//   NpcActionsEnum.TALK = 3
//   DirectionsEnum.DIRECTION_SOUTH = 2
//   GenericActionEnum.None = 0
//
// PK ID encoding : npc_spawns / npc_actions / npc_replies primary keys are
// NOT auto-increment ; the ORM picks "MAX(Id) + 1" client-side. To stay safe
// from collisions we allocate Ids in the 200_000+ range (well above any
// realistic legacy id).
class Program
{
    // Exclusions : already in the prod DB, intact and functional. We must
    // NEITHER insert nor delete them.
    static readonly HashSet<int> PreservedTemplateIds = new() { 2897, 2892 };
    //   2897 = Ganymède (NpcSpawnId 77)
    //   2892 = Lykhen Lesurviven (NpcSpawnId 43)

    // Skip totally — these are not dialogue NPCs (teleporter object).
    static readonly HashSet<int> SkippedTemplateIds = new() { 4398 };

    // Override the resolved mapId to the spawn fallback, because the Doflex
    // page reports a non-Incarnam mapId for these.
    static readonly Dictionary<int, int> MapIdOverrides = new()
    {
        { 2205, 154010883 }, // Mériana — Doflex mapped 122683905 (off-zone)
        { 2270, 154010883 }, // Flamme du Dark Vlad — same
    };

    const int SPAWN_FALLBACK_MAP_ID = 154010883;
    const short DEFAULT_CELL_ID = 280;
    const int DEFAULT_DIRECTION = 2; // DIRECTION_SOUTH
    const int TALK_ACTION = 3;       // NpcActionsEnum.TALK
    const int GENERIC_ACTION_NONE = 0;

    // High Id range : safe from collisions with legacy spawns (max observed
    // 77 = Ganymède). 200_000 leaves room for natural growth.
    const long ID_BASE = 200_000;

    static int Main(string[] args)
    {
        string clientPath = args.Length > 0 ? args[0] : ResolveClientPath();
        string projectDir = ResolveProjectDir();
        string outputDir = Path.GetFullPath(Path.Combine(projectDir, "..", "IncarnamScan", "output"));
        if (!Directory.Exists(outputDir))
            throw new DirectoryNotFoundException($"IncarnamScan output dir not found : {outputDir}");

        string positionsJson = Path.Combine(outputDir, "npc-positions-wiki.json");
        string outSqlPath = Path.Combine(outputDir, "incarnam-npcs.sql");

        Console.WriteLine($"ClientPath  : {clientPath}");
        Console.WriteLine($"Positions   : {positionsJson}");
        Console.WriteLine($"Output SQL  : {outSqlPath}");
        Console.WriteLine();

        D2OManager.Initialize(Path.Combine(clientPath, "data", "common"));
        D2IManager.Initialize(Path.Combine(clientPath, "data", "i18n"));

        var npcs = D2OManager.GetObjects("Npcs.d2o").OfType<Npc>().ToDictionary(n => n.Id);
        Console.WriteLine($"Loaded {npcs.Count} NPCs from Npcs.d2o");

        var positions = JsonConvert.DeserializeObject<List<PositionEntry>>(File.ReadAllText(positionsJson))
            ?? throw new InvalidOperationException("Failed to deserialize positions JSON");
        Console.WriteLine($"Loaded {positions.Count} positions from npc-positions-wiki.json");

        var (farewellMsgId, farewellText) = FindFarewellMessageId();
        if (farewellMsgId.HasValue)
            Console.WriteLine($"Farewell message : id={farewellMsgId} text=\"{farewellText}\"");
        else
            Console.WriteLine("Farewell message : not found — replies will be skipped");
        Console.WriteLine();

        // Apply rules to filter the work set
        var workSet = new List<WorkItem>();
        var notes = new List<string>();
        foreach (var p in positions)
        {
            if (PreservedTemplateIds.Contains(p.TemplateId))
            {
                notes.Add($"-- preserved (already in DB)  : [{p.TemplateId}] {p.Name}");
                continue;
            }
            if (SkippedTemplateIds.Contains(p.TemplateId))
            {
                notes.Add($"-- skipped (not a dialogue)   : [{p.TemplateId}] {p.Name}");
                continue;
            }
            int mapId = p.MapId;
            if (MapIdOverrides.TryGetValue(p.TemplateId, out var overrideMapId))
            {
                notes.Add($"-- map override               : [{p.TemplateId}] {p.Name} : {mapId} → {overrideMapId} (Doflex coord was off-zone)");
                mapId = overrideMapId;
            }
            else if (mapId <= 0)
            {
                notes.Add($"-- null map → spawn fallback  : [{p.TemplateId}] {p.Name}");
                mapId = SPAWN_FALLBACK_MAP_ID;
            }

            int? firstMessageId = null;
            if (npcs.TryGetValue(p.TemplateId, out var d2oNpc) && d2oNpc.DialogMessages != null && d2oNpc.DialogMessages.Count > 0)
            {
                var firstActionMessages = d2oNpc.DialogMessages[0];
                if (firstActionMessages != null && firstActionMessages.Count > 0)
                    firstMessageId = firstActionMessages[0];
            }
            if (firstMessageId == null)
                notes.Add($"-- no dialog messages         : [{p.TemplateId}] {p.Name} (npc_actions row will still be created with empty Param1)");

            workSet.Add(new WorkItem
            {
                TemplateId = p.TemplateId,
                Name = p.Name,
                MapId = mapId,
                Source = p.Source ?? "unknown",
                SubArea = p.SubArea,
                Coords = p.Coords,
                FirstMessageId = firstMessageId,
            });
        }

        Console.WriteLine($"WorkSet : {workSet.Count} NPCs to insert");
        Console.WriteLine();

        // Compose the SQL
        var sb = new StringBuilder();
        WriteHeader(sb, workSet, farewellMsgId, farewellText, notes);
        WriteDeleteSection(sb, workSet);
        WriteInsertSection(sb, workSet, farewellMsgId);
        WriteFooter(sb, workSet, farewellMsgId);

        File.WriteAllText(outSqlPath, sb.ToString());
        Console.WriteLine($"Wrote {sb.Length} bytes to {outSqlPath}");

        int spawnCount = workSet.Count;
        int actionCount = workSet.Count(w => w.FirstMessageId.HasValue);
        int replyCount = farewellMsgId.HasValue ? actionCount : 0;
        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine($"npc_spawns  : {spawnCount}");
        Console.WriteLine($"npc_actions : {actionCount}");
        Console.WriteLine($"npc_replies : {replyCount}");
        Console.WriteLine($"Excluded    : {PreservedTemplateIds.Count} (preserved)");
        Console.WriteLine($"Skipped     : {SkippedTemplateIds.Count} (teleporter)");
        Console.WriteLine($"Overridden  : {MapIdOverrides.Count} (map override)");
        return 0;
    }

    static (int? id, string? text) FindFarewellMessageId()
    {
        // We look for any i18n_fr key whose value is exactly "Au revoir.",
        // "Au revoir !", "Au revoir" (in this priority order). Returns the
        // shortest matching MessageId so we pick a generic system text rather
        // than NPC-specific dialog.
        var candidates = new[] { "Au revoir.", "Au revoir !", "Au revoir", "Au revoir...", "Adieu." };
        foreach (var c in candidates)
        {
            var hit = D2IManager.GetAllText("fr")
                .Where(e => string.Equals(e.Text, c, StringComparison.Ordinal))
                .OrderBy(e => e.Key)
                .FirstOrDefault();
            if (hit != null && hit.Key > 0)
                return (hit.Key, hit.Text);
        }
        return (null, null);
    }

    static void WriteHeader(StringBuilder sb, List<WorkItem> workSet, int? farewellMsgId, string? farewellText, List<string> notes)
    {
        sb.AppendLine("-- ============================================================");
        sb.AppendLine("-- Incarnam NPC population (npc_spawns + npc_actions + npc_replies)");
        sb.AppendLine($"-- Generated  : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"-- Generator  : tools/IncarnamSqlGen/Program.cs");
        sb.AppendLine($"-- Inputs     : tools/IncarnamScan/output/npc-positions-wiki.json");
        sb.AppendLine($"--              + Ressources/Dofus/data/common/Npcs.d2o (dialog message IDs)");
        sb.AppendLine($"-- Idempotent : DELETE step removes any prior Incarnam rows for the");
        sb.AppendLine($"--              templates listed below (Preserved IDs are skipped).");
        sb.AppendLine($"--");
        sb.AppendLine($"-- Counts     : {workSet.Count} npc_spawns");
        sb.AppendLine($"--              {workSet.Count(w => w.FirstMessageId.HasValue)} npc_actions");
        sb.AppendLine($"--              {(farewellMsgId.HasValue ? workSet.Count(w => w.FirstMessageId.HasValue) : 0)} npc_replies");
        sb.AppendLine($"--");
        if (farewellMsgId.HasValue)
            sb.AppendLine($"-- Farewell MessageId : {farewellMsgId} (\"{farewellText}\")");
        else
            sb.AppendLine($"-- Farewell MessageId : not found in i18n_fr — replies skipped");
        sb.AppendLine($"--");
        sb.AppendLine($"-- Enum values are written numerically (DatabaseWriter convention,");
        sb.AppendLine($"-- cf. Sources/Giny.ORM/IO/DatabaseWriter.cs:203-210). Reader accepts");
        sb.AppendLine($"-- both name and numeric, so backward-compatible.");
        sb.AppendLine($"--   NpcActionsEnum.TALK            = 3");
        sb.AppendLine($"--   DirectionsEnum.DIRECTION_SOUTH = 2");
        sb.AppendLine($"--   GenericActionEnum.None         = 0");
        sb.AppendLine($"--");
        foreach (var n in notes) sb.AppendLine(n);
        sb.AppendLine("-- ============================================================");
        sb.AppendLine();
        sb.AppendLine("START TRANSACTION;");
        sb.AppendLine();
    }

    static void WriteDeleteSection(StringBuilder sb, List<WorkItem> workSet)
    {
        sb.AppendLine("-- ============================================================");
        sb.AppendLine("-- Section A : idempotent DELETE (children before parents,");
        sb.AppendLine("--             preserved templates are never touched)");
        sb.AppendLine("-- ============================================================");
        sb.AppendLine();

        var templateList = string.Join(", ", workSet.Select(w => w.TemplateId).OrderBy(x => x));
        var preservedList = string.Join(", ", PreservedTemplateIds.OrderBy(x => x));

        sb.AppendLine("CREATE TEMPORARY TABLE _incarnam_spawn_ids (Id BIGINT NOT NULL PRIMARY KEY);");
        sb.AppendLine();
        sb.AppendLine("INSERT INTO _incarnam_spawn_ids (Id)");
        sb.AppendLine("SELECT Id FROM npc_spawns");
        sb.AppendLine($"WHERE TemplateId IN ({templateList})");
        sb.AppendLine($"  AND TemplateId NOT IN ({preservedList});");
        sb.AppendLine();
        sb.AppendLine("DELETE FROM npc_replies WHERE NpcSpawnId IN (SELECT Id FROM _incarnam_spawn_ids);");
        sb.AppendLine("DELETE FROM npc_actions WHERE NpcSpawnId IN (SELECT Id FROM _incarnam_spawn_ids);");
        sb.AppendLine("DELETE FROM npc_spawns  WHERE Id          IN (SELECT Id FROM _incarnam_spawn_ids);");
        sb.AppendLine();
        sb.AppendLine("DROP TEMPORARY TABLE _incarnam_spawn_ids;");
        sb.AppendLine();
    }

    static void WriteInsertSection(StringBuilder sb, List<WorkItem> workSet, int? farewellMsgId)
    {
        sb.AppendLine("-- ============================================================");
        sb.AppendLine("-- Section B : INSERTs (Id, NpcSpawnId, ReplyId all in 200_000+ range)");
        sb.AppendLine("-- ============================================================");
        sb.AppendLine();

        // Allocate fixed IDs so the script is fully deterministic and re-runs
        // produce the same row IDs (assuming the DELETE step succeeds).
        long spawnId = ID_BASE + 1;
        long actionId = ID_BASE + 1;
        long replyRowId = ID_BASE + 1;
        int replyId = (int)(ID_BASE + 1); // public ReplyId, distinct from PK Id

        foreach (var w in workSet)
        {
            sb.AppendLine($"-- [{w.TemplateId}] {Escape(w.Name)}  (source={w.Source}, subArea={w.SubArea ?? "n/a"}, coords={w.Coords ?? "n/a"})");
            sb.AppendLine($"INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES " +
                          $"({spawnId}, {w.TemplateId}, {w.MapId}, {DEFAULT_CELL_ID}, {DEFAULT_DIRECTION});");

            if (w.FirstMessageId.HasValue)
            {
                sb.AppendLine($"INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES " +
                              $"({actionId}, {spawnId}, {TALK_ACTION}, '{w.FirstMessageId.Value}');");

                if (farewellMsgId.HasValue)
                {
                    sb.AppendLine($"INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES " +
                                  $"({replyRowId}, {replyId}, {spawnId}, {farewellMsgId.Value}, {GENERIC_ACTION_NONE});");
                    replyRowId++;
                    replyId++;
                }
                actionId++;
            }
            else
            {
                sb.AppendLine($"-- (no dialogMessages in D2O for {w.TemplateId}; no npc_actions row)");
            }

            spawnId++;
            sb.AppendLine();
        }
    }

    static void WriteFooter(StringBuilder sb, List<WorkItem> workSet, int? farewellMsgId)
    {
        sb.AppendLine("-- ============================================================");
        sb.AppendLine("COMMIT;");
        sb.AppendLine();
        sb.AppendLine("-- Done. Restart the world server (or call NpcSpawnRecord.Initialize)");
        sb.AppendLine("-- to load the new rows into memory.");
    }

    static string Escape(string s) => s.Replace("'", "''").Replace("--", "- -");

    static string ResolveProjectDir() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

    static string ResolveClientPath()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(ResolveProjectDir(), "..", ".."));
        var candidate = Path.Combine(repoRoot, "Ressources", "Dofus");
        if (!Directory.Exists(Path.Combine(candidate, "data", "common")))
            throw new InvalidOperationException(
                $"Dofus client not found at {candidate}. Pass ClientPath as first CLI arg, or place client at <repo>/Ressources/Dofus/.");
        return candidate;
    }

    class PositionEntry
    {
        [JsonProperty("templateId")] public int TemplateId { get; set; }
        [JsonProperty("name")]       public string Name { get; set; } = "";
        [JsonProperty("mapId")]      public int MapId { get; set; }
        [JsonProperty("source")]     public string? Source { get; set; }
        [JsonProperty("subArea")]    public string? SubArea { get; set; }
        [JsonProperty("coords")]     public string? Coords { get; set; }
    }

    class WorkItem
    {
        public int TemplateId;
        public string Name = "";
        public int MapId;
        public string Source = "";
        public string? SubArea;
        public string? Coords;
        public int? FirstMessageId;
    }
}
