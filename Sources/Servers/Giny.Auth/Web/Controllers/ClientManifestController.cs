using Giny.Core.IO.Configuration;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Giny.Auth.Web.Controllers
{
    /// <summary>
    /// Expose un manifeste JSON des fichiers client (D2I + D2O) que le serveur
    /// veut voir synchronisés sur les postes joueurs. L'Uplauncher l'interroge
    /// au démarrage, compare avec ses fichiers locaux et télécharge le diff.
    ///
    /// MVP : scan + MD5 recalculés à chaque requête (~2-3s pour ~150 MB sur
    /// disque local). Suffit tant que le manifeste est appelé une fois par
    /// login. Cache via FileSystemWatcher en sous-mission de robustesse.
    /// </summary>
    [ApiController]
    [Route("client")]
    public class ClientManifestController : ControllerBase
    {
        // Sous-dossiers du client à exposer. Liste explicite pour éviter de
        // distribuer accidentellement le SWF entier ou les fonts (gros + non
        // patchés côté serveur). Ordonné pour reproductibilité du global hash.
        private static readonly string[] ExposedSubdirs = new[]
        {
            "data/common",
            "data/i18n",
        };

        // Fichiers de service (backup, tmp éditeurs) qu'on ne veut pas
        // distribuer aux clients même s'ils traînent dans le ClientPath.
        private static readonly string[] ExcludedExtensions = new[]
        {
            ".bak",
            ".tmp",
        };

        public record ManifestEntry(string path, string md5, long size);
        public record Manifest(string version, ManifestEntry[] files);

        [HttpGet("manifest")]
        public ActionResult<Manifest> GetManifest()
        {
            var clientPath = ConfigManager<AuthConfig>.Instance.ClientPath;

            if (string.IsNullOrWhiteSpace(clientPath) || !Directory.Exists(clientPath))
            {
                return StatusCode(503, new { error = $"ClientPath '{clientPath}' is not configured or does not exist on the server." });
            }

            var entries = new List<ManifestEntry>();

            foreach (var subdir in ExposedSubdirs)
            {
                var fullSubdir = Path.Combine(clientPath, subdir);

                if (!Directory.Exists(fullSubdir))
                    continue;

                var files = Directory.EnumerateFiles(fullSubdir, "*", SearchOption.AllDirectories)
                    .Where(f => !ExcludedExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

                foreach (var file in files)
                {
                    var relative = Path.GetRelativePath(clientPath, file).Replace('\\', '/');
                    var info = new FileInfo(file);

                    string md5;
                    using (var stream = System.IO.File.OpenRead(file))
                    using (var md5er = MD5.Create())
                    {
                        md5 = Convert.ToHexString(md5er.ComputeHash(stream)).ToLowerInvariant();
                    }

                    entries.Add(new ManifestEntry(relative, md5, info.Length));
                }
            }

            // Ordre stable par path pour que le client puisse comparer fichier
            // à fichier et pour que le global hash soit reproductible.
            entries.Sort((a, b) => string.CompareOrdinal(a.path, b.path));

            // Global hash : MD5 de la concaténation des MD5 individuels triés
            // par path. Permet à l'Uplauncher un quick-compare "rien n'a changé"
            // avant de descendre dans le diff par fichier.
            var concat = string.Concat(entries.Select(e => e.md5));
            string globalVersion;
            using (var md5er = MD5.Create())
            {
                globalVersion = Convert.ToHexString(md5er.ComputeHash(Encoding.UTF8.GetBytes(concat))).ToLowerInvariant();
            }

            return new Manifest(globalVersion, entries.ToArray());
        }
    }
}
