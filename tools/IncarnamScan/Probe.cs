using Giny.IO.D2I;
using Giny.IO.D2O;
using Giny.IO.D2OClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IncarnamScan;

// Exploratoire — ne fait pas partie du flow standard. Lancer avec :
//   dotnet run -- --probe
static class Probe
{
    public static void Run(string clientPath)
    {
        D2OManager.Initialize(Path.Combine(clientPath, "data", "common"));
        D2IManager.Initialize(Path.Combine(clientPath, "data", "i18n"));

        var subAreas = D2OManager.GetObjects("SubAreas.d2o").OfType<SubArea>().ToList();
        Console.WriteLine($"Total SubAreas: {subAreas.Count}");
        Console.WriteLine();

        // Liste les SubAreas par AreaId petit (zones de bas niveau probables)
        Console.WriteLine("=== SubAreas with low AreaId (probable starter zones) ===");
        foreach (var sa in subAreas.Where(s => s.AreaId <= 5).OrderBy(s => s.AreaId).ThenBy(s => s.Id))
        {
            var name = D2IManager.GetText((int)sa.NameId);
            Console.WriteLine($"  SubArea {sa.Id,3} (AreaId={sa.AreaId,2}, Level={sa.Level,2}) : \"{name}\"  ({sa.MapIds?.Count ?? 0} maps, {sa.Npcs?.Count ?? 0} npcs, {sa.Quests?.Count ?? 0} quests)");
        }

        // Cherche par substring élargie
        Console.WriteLine();
        Console.WriteLine("=== Search by various substrings ===");
        foreach (var needle in new[] { "ncarnam", "Incar", "tutoriel", "tutorial", "départ", "start", "Routes" })
        {
            var matches = subAreas
                .Where(s =>
                {
                    var n = D2IManager.GetText((int)s.NameId);
                    return n.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();
            Console.WriteLine($"  \"{needle}\" : {matches.Count} matches");
            foreach (var m in matches.Take(5))
            {
                Console.WriteLine($"    SubArea {m.Id} : \"{D2IManager.GetText((int)m.NameId)}\"");
            }
        }

        // Spawn map 154010883 — résout sa SubArea + AreaId pour cibler la zone
        Console.WriteLine();
        Console.WriteLine("=== Resolve spawn map 154010883 ===");
        var mapPositions = D2OManager.GetObjects("MapPositions.d2o").OfType<MapPosition>().ToList();
        var spawn = mapPositions.FirstOrDefault(mp => (long)mp.Id_ == 154010883);
        if (spawn != null)
        {
            var sa = subAreas.FirstOrDefault(s => s.Id == spawn.SubAreaId);
            Console.WriteLine($"  MapId 154010883 → SubAreaId={spawn.SubAreaId}");
            if (sa != null)
            {
                Console.WriteLine($"  SubArea {sa.Id} : \"{D2IManager.GetText((int)sa.NameId)}\" (AreaId={sa.AreaId})");
                Console.WriteLine($"  Maps in this SubArea: {sa.MapIds?.Count ?? 0}");
                Console.WriteLine($"  NPCs: {sa.Npcs?.Count ?? 0}, Quests: {sa.Quests?.Count ?? 0}");
            }
            // Lister toutes les SubAreas de la même Area
            Console.WriteLine();
            Console.WriteLine($"=== All SubAreas with AreaId={sa?.AreaId} ===");
            foreach (var sib in subAreas.Where(s => s.AreaId == sa?.AreaId).OrderBy(s => s.Id))
            {
                Console.WriteLine($"  SubArea {sib.Id,3} : \"{D2IManager.GetText((int)sib.NameId)}\"  ({sib.MapIds?.Count ?? 0} maps)");
            }
        }
        else
        {
            Console.WriteLine("  MapId 154010883 NOT FOUND in MapPositions.d2o");
        }
    }
}
