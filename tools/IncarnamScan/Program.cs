using Giny.IO.D2I;
using Giny.IO.D2O;
using Giny.IO.D2OClasses;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IncarnamScan;

class Program
{
    // Mapping local des QuestObjectiveType pour ne pas dépendre de Giny.Protocol.
    // Source : Sources/Giny.Protocol/Custom/Enums/QuestObjectiveTypeEnum.cs
    private static readonly Dictionary<uint, string> ObjectiveTypeNames = new()
    {
        { 0, "None" },
        { 1, "GoToNpc" },
        { 2, "BringItemToNpc" },
        { 3, "GiveItemToNpc" },
        { 4, "DiscoverMap" },
        { 5, "DiscoverSubarea" },
        { 6, "DefeatMonsterOneFight" },
        { 7, "DefeatMonsters" },
        { 8, "UseItem" },
        { 9, "NpcTalkBack" },
        { 10, "Escort" },
        { 11, "DuelSpecificPlayer" },
        { 12, "BringSoulsToNpc" },
        { 13, "DefeatOne" },
        { 14, "DefeatMulti" },
        { 15, "WinKromaster" },
        { 17, "CraftItem" },
    };

    // Handlers Giny réellement traqués au runtime (cf. QuestManager.OnNpcTalk).
    // Tous les autres types ont un case ToString() pour l'affichage mais pas
    // de hook runtime qui complète l'objective.
    private static readonly HashSet<uint> ImplementedObjectiveTypes = new() { 1, 9 };

    static int Main(string[] args)
    {
        // Mode probe (exploratoire) : `dotnet run -- --probe [clientPath]`
        if (args.Length > 0 && args[0] == "--probe")
        {
            var probePath = args.Length > 1 ? args[1] : ResolveClientPath();
            Probe.Run(probePath);
            return 0;
        }

        string clientPath = args.Length > 0 ? args[0] : ResolveClientPath();
        string outputDir = Path.Combine(ResolveProjectDir(), "output");
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"ClientPath : {clientPath}");
        Console.WriteLine($"OutputDir  : {outputDir}");
        Console.WriteLine();

        D2OManager.Initialize(Path.Combine(clientPath, "data", "common"));
        D2IManager.Initialize(Path.Combine(clientPath, "data", "i18n"));

        // ============ Phase A — SubAreas Incarnam ============
        // Approche : Incarnam est une Area (AreaId=45 en 2.68), pas un nom de
        // SubArea — les sous-zones s'appellent "Lac", "Forêt", "Temple Céleste"
        // etc. On résout l'AreaId via le D2I de Areas.d2o puis on filtre les
        // SubAreas par AreaId.
        Console.WriteLine("=== Phase A : SubAreas Incarnam ===");

        var allAreas = D2OManager.GetObjects("Areas.d2o").OfType<Area>().ToList();
        var incarnamAreas = allAreas
            .Where(a =>
            {
                var name = D2IManager.GetText((int)a.NameId);
                return name.Contains("Incarnam", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        Console.WriteLine($"Areas matching \"Incarnam\" : {incarnamAreas.Count}");
        foreach (var a in incarnamAreas)
        {
            Console.WriteLine($"  Area {a.Id} : \"{D2IManager.GetText((int)a.NameId)}\"  (SuperAreaId={a.SuperAreaId})");
        }
        var incarnamAreaIds = incarnamAreas.Select(a => a.Id).ToHashSet();

        var allSubAreas = D2OManager.GetObjects("SubAreas.d2o").OfType<SubArea>().ToList();
        Console.WriteLine($"Total SubAreas in D2O : {allSubAreas.Count}");

        var incarnamSubAreas = allSubAreas
            .Where(sa => incarnamAreaIds.Contains(sa.AreaId))
            .OrderBy(sa => sa.Id)
            .ToList();

        Console.WriteLine($"SubAreas in Incarnam areas : {incarnamSubAreas.Count}");
        foreach (var sa in incarnamSubAreas)
        {
            var name = D2IManager.GetText((int)sa.NameId);
            Console.WriteLine($"  SubArea {sa.Id} (AreaId={sa.AreaId}, Level={sa.Level}) : \"{name}\"  ({sa.MapIds?.Count ?? 0} maps, {sa.Npcs?.Count ?? 0} npcs, {sa.Quests?.Count ?? 0} quests)");
        }

        var subAreasOut = incarnamSubAreas.Select(sa => new
        {
            Id = sa.Id,
            AreaId = sa.AreaId,
            Name = D2IManager.GetText((int)sa.NameId),
            Level = sa.Level,
            MapCount = sa.MapIds?.Count ?? 0,
            NpcEntries = sa.Npcs?.Count ?? 0,
            QuestEntries = sa.Quests?.Count ?? 0,
            AssociatedZaapMapId = sa.AssociatedZaapMapId,
        }).ToList();
        File.WriteAllText(Path.Combine(outputDir, "subareas.json"),
            JsonConvert.SerializeObject(subAreasOut, Formatting.Indented));

        // ============ Phase B — Maps ============
        Console.WriteLine();
        Console.WriteLine("=== Phase B : Maps Incarnam ===");
        var incarnamMapIds = incarnamSubAreas
            .SelectMany(sa => sa.MapIds ?? new List<double>())
            .Select(id => (long)id)
            .Distinct()
            .ToHashSet();

        var mapPositions = D2OManager.GetObjects("MapPositions.d2o").OfType<MapPosition>().ToList();
        var incarnamMaps = mapPositions
            .Where(mp => incarnamMapIds.Contains((long)mp.Id_))
            .Select(mp =>
            {
                var subArea = incarnamSubAreas.FirstOrDefault(sa => sa.Id == mp.SubAreaId);
                return new
                {
                    MapId = (long)mp.Id_,
                    PosX = mp.PosX,
                    PosY = mp.PosY,
                    SubAreaId = mp.SubAreaId,
                    SubAreaName = subArea != null ? D2IManager.GetText((int)subArea.NameId) : "",
                    Outdoor = mp.Outdoor,
                    IsTransition = mp.IsTransition,
                };
            })
            .OrderBy(m => m.SubAreaId)
            .ThenBy(m => m.PosY)
            .ThenBy(m => m.PosX)
            .ToList();

        Console.WriteLine($"Maps in Incarnam SubAreas : {incarnamMaps.Count}");
        File.WriteAllText(Path.Combine(outputDir, "maps.json"),
            JsonConvert.SerializeObject(incarnamMaps, Formatting.Indented));

        // ============ Phase C — NPCs ============
        // SubArea.Npcs est List<List<double>>. Les NPCs d'une SubArea sont
        // référencés par leur TemplateId dans la première position (heuristique
        // standard Ankama). On extrait tous les uniques.
        Console.WriteLine();
        Console.WriteLine("=== Phase C : NPCs Incarnam ===");
        var incarnamNpcTemplateIds = incarnamSubAreas
            .SelectMany(sa => sa.Npcs ?? new List<List<double>>())
            .Where(inner => inner != null && inner.Count > 0)
            .Select(inner => (int)inner[0])
            .Distinct()
            .ToHashSet();

        var allNpcs = D2OManager.GetObjects("Npcs.d2o").OfType<Npc>().ToList();
        var incarnamNpcs = allNpcs
            .Where(n => incarnamNpcTemplateIds.Contains(n.Id))
            .Select(n => new
            {
                TemplateId = n.Id,
                Name = D2IManager.GetText((int)n.NameId),
                Look = n.Look,
                DialogMessagesCount = n.DialogMessages?.Count ?? 0,
                ActionsAuthorized = n.Actions?.Select(a => (int)a).ToList(),
            })
            .OrderBy(n => n.TemplateId)
            .ToList();

        Console.WriteLine($"NPCs referenced by Incarnam SubAreas : {incarnamNpcs.Count}");
        File.WriteAllText(Path.Combine(outputDir, "npcs.json"),
            JsonConvert.SerializeObject(incarnamNpcs, Formatting.Indented));

        // ============ Phase D — Quests ============
        Console.WriteLine();
        Console.WriteLine("=== Phase D : Quests Incarnam ===");
        var incarnamQuestIds = incarnamSubAreas
            .SelectMany(sa => sa.Quests ?? new List<List<double>>())
            .Where(inner => inner != null && inner.Count > 0)
            .Select(inner => (int)inner[0])
            .Distinct()
            .ToHashSet();

        Console.WriteLine($"Quest IDs referenced by Incarnam SubAreas : {incarnamQuestIds.Count}");

        var allQuests = D2OManager.GetObjects("Quests.d2o").OfType<Quest>().ToList();
        var allSteps = D2OManager.GetObjects("QuestSteps.d2o").OfType<QuestStep>()
            .ToDictionary(qs => (uint)qs.Id_);
        var allObjectives = D2OManager.GetObjects("QuestObjectives.d2o").OfType<QuestObjective>()
            .ToDictionary(qo => (uint)qo.Id);

        var incarnamQuests = allQuests
            .Where(q => incarnamQuestIds.Contains(q.Id))
            .Select(q => new
            {
                QuestId = q.Id,
                Name = D2IManager.GetText((int)q.NameId),
                LevelMin = (int)q.LevelMin,
                LevelMax = (int)q.LevelMax,
                CategoryId = (int)q.CategoryId,
                Followable = q.Followable,
                StartCriterion = q.StartCriterion ?? "",
                Steps = (q.StepIds ?? new List<uint>())
                    .Select(sid =>
                    {
                        if (!allSteps.TryGetValue(sid, out var step))
                            return null;
                        return (object)new
                        {
                            StepId = (int)sid,
                            Name = D2IManager.GetText((int)step.NameId),
                            DescriptionText = step.DescriptionId > 0
                                ? D2IManager.GetText((int)step.DescriptionId)
                                : "",
                            OptimalLevel = (int)step.OptimalLevel,
                            Objectives = (step.ObjectiveIds ?? new List<uint>())
                                .Select(oid =>
                                {
                                    if (!allObjectives.TryGetValue(oid, out var obj))
                                        return null;
                                    var typeName = ObjectiveTypeNames.GetValueOrDefault(obj.TypeId, $"Unknown({obj.TypeId})");
                                    return (object)new
                                    {
                                        ObjectiveId = obj.Id,
                                        TypeId = (int)obj.TypeId,
                                        TypeName = typeName,
                                        Param0 = obj.Parameters?.Parameter0 ?? 0,
                                        Param1 = obj.Parameters?.Parameter1 ?? 0,
                                        Param2 = obj.Parameters?.Parameter2 ?? 0,
                                        Param3 = obj.Parameters?.Parameter3 ?? 0,
                                        MapId = (long)obj.MapId,
                                        HandlerImplemented = ImplementedObjectiveTypes.Contains(obj.TypeId),
                                    };
                                })
                                .Where(o => o != null)
                                .ToList(),
                        };
                    })
                    .Where(s => s != null)
                    .ToList(),
            })
            .OrderBy(q => q.LevelMin)
            .ThenBy(q => q.QuestId)
            .ToList();

        Console.WriteLine($"Quests resolved : {incarnamQuests.Count}");
        File.WriteAllText(Path.Combine(outputDir, "quests.json"),
            JsonConvert.SerializeObject(incarnamQuests, Formatting.Indented));

        // ============ Phase E — Summary ============
        Console.WriteLine();
        Console.WriteLine("=== Phase E : Summary ===");

        var objectiveTypeCounts = new Dictionary<uint, int>();
        foreach (var q in incarnamQuests)
        {
            foreach (var stepObj in q.Steps)
            {
                if (stepObj is null) continue;
                dynamic step = stepObj;
                foreach (var objAnon in step.Objectives)
                {
                    if (objAnon is null) continue;
                    dynamic o = objAnon;
                    uint typeId = (uint)(int)o.TypeId;
                    objectiveTypeCounts.TryGetValue(typeId, out int existing);
                    objectiveTypeCounts[typeId] = existing + 1;
                }
            }
        }

        var summary = new
        {
            GeneratedAt = DateTime.UtcNow.ToString("u"),
            ClientPath = clientPath,
            SubAreaCount = incarnamSubAreas.Count,
            MapCount = incarnamMaps.Count,
            NpcCount = incarnamNpcs.Count,
            QuestCount = incarnamQuests.Count,
            ObjectiveTypeCounts = objectiveTypeCounts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new
                {
                    TypeId = (int)kv.Key,
                    TypeName = ObjectiveTypeNames.GetValueOrDefault(kv.Key, $"Unknown({kv.Key})"),
                    Count = kv.Value,
                    HandlerImplemented = ImplementedObjectiveTypes.Contains(kv.Key),
                })
                .ToList(),
        };
        File.WriteAllText(Path.Combine(outputDir, "summary.json"),
            JsonConvert.SerializeObject(summary, Formatting.Indented));

        Console.WriteLine($"SubAreas Incarnam : {summary.SubAreaCount}");
        Console.WriteLine($"Maps              : {summary.MapCount}");
        Console.WriteLine($"NPCs              : {summary.NpcCount}");
        Console.WriteLine($"Quests            : {summary.QuestCount}");
        Console.WriteLine();
        Console.WriteLine("Objective type frequencies (handlers implemented marked *):");
        foreach (var entry in summary.ObjectiveTypeCounts)
        {
            var marker = entry.HandlerImplemented ? "*" : " ";
            Console.WriteLine($"  {marker} {entry.TypeName,-25} (id={entry.TypeId,2})  : {entry.Count}");
        }

        Console.WriteLine();
        Console.WriteLine("Output files :");
        foreach (var file in new[] { "subareas.json", "maps.json", "npcs.json", "quests.json", "summary.json" })
        {
            var path = Path.Combine(outputDir, file);
            var size = new FileInfo(path).Length;
            Console.WriteLine($"  {file,-20}  {size,8} bytes");
        }

        return 0;
    }

    static string ResolveProjectDir()
    {
        // AppContext.BaseDirectory = <repo>/tools/IncarnamScan/bin/Debug/net6.0/
        // 3 ups → <repo>/tools/IncarnamScan/
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    }

    static string ResolveClientPath()
    {
        // <repo>/tools/IncarnamScan/ → 2 ups → <repo>/ → Ressources/Dofus
        var repoRoot = Path.GetFullPath(Path.Combine(ResolveProjectDir(), "..", ".."));
        var candidate = Path.Combine(repoRoot, "Ressources", "Dofus");
        if (!Directory.Exists(Path.Combine(candidate, "data", "common")))
        {
            throw new InvalidOperationException(
                $"Dofus client not found at {candidate}. Pass ClientPath as first CLI arg, or place client at <repo>/Ressources/Dofus/.");
        }
        return candidate;
    }
}
