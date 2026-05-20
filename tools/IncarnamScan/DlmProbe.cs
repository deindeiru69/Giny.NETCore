using Giny.IO.D2P;
using Giny.IO.DLM;
using Giny.IO.DLM.Elements;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace IncarnamScan;

// Mode `--dump-dlm <mapId>` : analyse structurelle d'un .dlm pour
// déterminer s'il contient des références NPCs (TemplateId d'un NPC
// Ankama). Hypothèse à invalider : "les positions NPCs natives sont
// extractibles depuis les .dlm de Maps.d2p".
static class DlmProbe
{
    public static void Run(string clientPath, int mapId)
    {
        Console.WriteLine($"Probing .dlm for MapId {mapId}");
        Console.WriteLine();

        var packageIdx = mapId % 10;
        var entryName = $"{packageIdx}/{mapId}.dlm";

        // Maps sont split en maps0..maps6.d2p, liés entre eux via le mécanisme
        // de D2PFile.Links. On charge maps0 et on demande l'entry — D2PFile
        // sait remonter les links si besoin.
        var mapsRoot = Path.Combine(clientPath, "content", "maps");
        var maps0 = Path.Combine(mapsRoot, "maps0.d2p");
        if (!File.Exists(maps0))
        {
            Console.WriteLine($"Cannot find maps0.d2p at {maps0}");
            return;
        }

        D2PFile? holder = null;
        D2PEntry? entry = null;
        // Essai sur chaque maps*.d2p
        for (int i = 0; i <= 6; i++)
        {
            var path = Path.Combine(mapsRoot, $"maps{i}.d2p");
            if (!File.Exists(path)) continue;

            var f = new D2PFile(path);
            var candidate = f.TryGetEntry(entryName);
            if (candidate != null)
            {
                holder = f;
                entry = candidate;
                Console.WriteLine($"Found entry in maps{i}.d2p");
                break;
            }
        }

        if (entry == null || holder == null)
        {
            Console.WriteLine($"Entry '{entryName}' not found in any maps*.d2p");
            return;
        }

        var compressed = holder.ReadFile(entry);
        Console.WriteLine($"Compressed bytes: {compressed.Length}");

        // Décompresse manuellement pour pouvoir compter la taille raw.
        byte[] decompressed;
        using (var ms = new MemoryStream(compressed, 2, compressed.Length - 2))  // skip 2-byte deflate header
        using (var df = new DeflateStream(ms, CompressionMode.Decompress))
        using (var dst = new MemoryStream())
        {
            df.CopyTo(dst);
            decompressed = dst.ToArray();
        }
        Console.WriteLine($"Decompressed bytes: {decompressed.Length}");

        // Parse via DlmMap.
        var map = new DlmMap(compressed);
        Console.WriteLine($"Parsed OK. MapVersion={map.MapVersion}, Id={map.Id}, SubAreaId={map.SubareaId}");
        Console.WriteLine($"  Layers count          : {map.Layers.Count}");
        Console.WriteLine($"  Cells count           : {map.Cells.Length}");
        Console.WriteLine($"  BackgroundFixtures    : {map.BackgroundFixtures.Count}");
        Console.WriteLine($"  ForegroundFixtures    : {map.ForegroundFixtures.Count}");

        // Compte les éléments par type dans les layers/cells.
        int graphicalCount = 0, soundCount = 0, otherCount = 0;
        var allElementIds = new HashSet<uint>();
        var allIdentifiers = new HashSet<uint>();

        foreach (var layer in map.Layers)
        {
            foreach (var dlmCell in layer.Cells)
            {
                foreach (var elem in dlmCell.Elements)
                {
                    allElementIds.Add(elem.ElementId);
                    switch (elem)
                    {
                        case GraphicalElement g:
                            graphicalCount++;
                            allIdentifiers.Add(g.Identifier);
                            break;
                        case SoundElement:
                            soundCount++;
                            break;
                        default:
                            otherCount++;
                            break;
                    }
                }
            }
        }

        Console.WriteLine($"  GraphicalElements     : {graphicalCount}");
        Console.WriteLine($"  SoundElements         : {soundCount}");
        Console.WriteLine($"  Other element types   : {otherCount}");
        Console.WriteLine($"  Unique ElementIds     : {allElementIds.Count}");
        Console.WriteLine($"  Unique Identifiers    : {allIdentifiers.Count}");

        // Round-trip test : re-serialize, decompress, compare byte-by-byte.
        // Si les tailles diffèrent ou si bytes diffèrent à un offset > 0,
        // il y a des données non-parsées (potentiellement NPCs cachés).
        Console.WriteLine();
        Console.WriteLine("=== Round-trip test ===");
        try
        {
            var reCompressed = map.Compress();
            byte[] reDecompressed;
            using (var ms = new MemoryStream(reCompressed, 2, reCompressed.Length - 2))
            using (var df = new DeflateStream(ms, CompressionMode.Decompress))
            using (var dst = new MemoryStream())
            {
                df.CopyTo(dst);
                reDecompressed = dst.ToArray();
            }
            Console.WriteLine($"Original decompressed   : {decompressed.Length} bytes");
            Console.WriteLine($"Re-serialized decompressed: {reDecompressed.Length} bytes");
            Console.WriteLine($"Size diff               : {decompressed.Length - reDecompressed.Length} bytes");

            // Premier offset où ils diffèrent.
            int minLen = Math.Min(decompressed.Length, reDecompressed.Length);
            int firstDiff = -1;
            for (int i = 0; i < minLen; i++)
            {
                if (decompressed[i] != reDecompressed[i])
                {
                    firstDiff = i;
                    break;
                }
            }
            if (firstDiff < 0 && decompressed.Length == reDecompressed.Length)
            {
                Console.WriteLine("=> Round-trip identique. Aucun byte non-parsé. Format complet exposé par Giny.IO.");
            }
            else if (firstDiff < 0 && decompressed.Length != reDecompressed.Length)
            {
                Console.WriteLine($"=> Bytes identiques jusqu'à {minLen} mais tailles différentes (+/- {Math.Abs(decompressed.Length - reDecompressed.Length)} bytes en queue).");
                // Dump des bytes en queue
                if (decompressed.Length > reDecompressed.Length)
                {
                    var tail = decompressed.Skip(reDecompressed.Length).Take(64).ToArray();
                    Console.WriteLine($"   Tail of original (first 64 bytes): {string.Join(" ", tail.Select(b => b.ToString("x2")))}");
                }
            }
            else
            {
                Console.WriteLine($"=> Différence à l'offset {firstDiff} (encoding minor diff probable)");
                Console.WriteLine($"   Original  : {string.Join(" ", decompressed.Skip(firstDiff).Take(32).Select(b => b.ToString("x2")))}");
                Console.WriteLine($"   Reserial. : {string.Join(" ", reDecompressed.Skip(firstDiff).Take(32).Select(b => b.ToString("x2")))}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Re-serialize FAILED: {ex.Message}");
        }

        // Recherche d'IDs connus de NPCs Incarnam dans tous les ElementId/Identifier.
        Console.WriteLine();
        Console.WriteLine("=== NPC TemplateId search ===");
        var npcsJsonPath = Path.Combine(Program.GetOutputDir(), "npcs.json");
        if (File.Exists(npcsJsonPath))
        {
            var npcIds = JsonConvert.DeserializeObject<List<JNpc>>(File.ReadAllText(npcsJsonPath))
                ?.Select(n => (uint)n.TemplateId).ToHashSet() ?? new HashSet<uint>();
            // On ajoute aussi 4823 (Ganymède) au cas où.
            npcIds.Add(4823);

            var foundInElementIds = allElementIds.Intersect(npcIds).ToList();
            var foundInIdentifiers = allIdentifiers.Intersect(npcIds).ToList();

            Console.WriteLine($"  NPC IDs cherchés     : {npcIds.Count}");
            Console.WriteLine($"  Match dans ElementId : {foundInElementIds.Count} ({string.Join(", ", foundInElementIds.Take(10))})");
            Console.WriteLine($"  Match dans Identifier: {foundInIdentifiers.Count} ({string.Join(", ", foundInIdentifiers.Take(10))})");
        }
        else
        {
            Console.WriteLine("  (npcs.json absent, skip cross-référence)");
        }

        // Dump raw bytes for visual inspection
        Console.WriteLine();
        Console.WriteLine("=== Raw hex tail (last 128 bytes of decompressed) ===");
        int tailStart = Math.Max(0, decompressed.Length - 128);
        var hex = decompressed.Skip(tailStart).ToArray();
        for (int i = 0; i < hex.Length; i += 16)
        {
            var line = hex.Skip(i).Take(16).ToArray();
            Console.WriteLine($"  {tailStart + i:x8}  {string.Join(" ", line.Select(b => b.ToString("x2")))}");
        }
    }

    private class JNpc
    {
        public int TemplateId { get; set; }
    }
}
