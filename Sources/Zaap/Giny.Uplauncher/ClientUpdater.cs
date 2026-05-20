using Giny.Core.IO.Configuration;
using Giny.Core.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Giny.Uplauncher
{
    public enum UpdateStatusEnum
    {
        None,
        VersionCheck,
        Downloading,
        Ready,
        Error,
    }
    public class ClientUpdater
    {
        public event Action<UpdateStatusEnum, object?> OnStatusUpdate;

        private UplConfig Config => ConfigManager<UplConfig>.Instance;

        public void Update()
        {
            try
            {
                OnStatusUpdate?.Invoke(UpdateStatusEnum.VersionCheck, null);

                // 1. Fetch manifest depuis /client/manifest
                var manifest = AuthApi.GetClientManifest();
                if (manifest == null)
                {
                    OnStatusUpdate?.Invoke(UpdateStatusEnum.Error,
                        new Exception("Impossible de récupérer le manifeste client."));
                    return;
                }

                // 2. Quick-skip : même version globale -> aucun fichier à
                //    télécharger. Le hash global est dérivé de tous les MD5
                //    individuels triés par path côté serveur, donc tout match.
                if (manifest.Version == Config.LocalVersion)
                {
                    OnStatusUpdate?.Invoke(UpdateStatusEnum.Ready, null);
                    return;
                }

                // 3. Diff fichier-par-fichier. On compare le MD5 local au MD5
                //    serveur ; backup .bak + écrasement pour chaque diff.
                int total = manifest.Files.Count;
                int current = 0;
                int updatedCount = 0;

                foreach (var entry in manifest.Files)
                {
                    current++;

                    var localPath = Path.Combine(Config.ClientPath,
                        entry.Path.Replace('/', Path.DirectorySeparatorChar));

                    bool needDownload = !File.Exists(localPath)
                        || !string.Equals(ComputeMd5(localPath), entry.Md5, StringComparison.OrdinalIgnoreCase);

                    if (!needDownload)
                        continue;

                    OnStatusUpdate?.Invoke(UpdateStatusEnum.Downloading,
                        new UpdateProgress
                        {
                            Current = current,
                            Total = total,
                            CurrentFilePath = entry.Path,
                        });

                    // Backup .bak (décision design : on garde une seule
                    // génération de backup, écrasée à chaque sync).
                    if (File.Exists(localPath))
                    {
                        var backupPath = localPath + ".bak";
                        if (File.Exists(backupPath))
                            File.Delete(backupPath);
                        File.Copy(localPath, backupPath);
                    }
                    else
                    {
                        var dir = Path.GetDirectoryName(localPath);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);
                    }

                    AuthApi.DownloadClientFile(entry.Path, localPath);
                    updatedCount++;
                }

                // 4. Persiste la nouvelle version. Si le sync a échoué en
                //    cours, on n'arrive pas ici — LocalVersion reste à sa
                //    valeur antérieure et le sync sera retenté au prochain
                //    démarrage.
                Config.LocalVersion = manifest.Version;
                ConfigManager<UplConfig>.Save(UplConfig.Filepath);

                OnStatusUpdate?.Invoke(UpdateStatusEnum.Ready, updatedCount);
            }
            catch (Exception ex)
            {
                OnStatusUpdate?.Invoke(UpdateStatusEnum.Error, ex);
            }
        }

        private static string ComputeMd5(string path)
        {
            using var stream = File.OpenRead(path);
            using var md5 = MD5.Create();
            return Convert.ToHexString(md5.ComputeHash(stream)).ToLowerInvariant();
        }
    }
}
