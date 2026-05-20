using System.Collections.Generic;

namespace Giny.Uplauncher
{
    /// <summary>
    /// DTO côté Uplauncher pour parser la réponse de GET /client/manifest
    /// (Auth API). Le contrôleur serveur renvoie un JSON camelCase ; Newtonsoft
    /// est case-insensitive en désérialisation, les propriétés PascalCase
    /// matchent donc directement.
    /// </summary>
    public class ManifestResponse
    {
        public string Version { get; set; } = string.Empty;

        public List<FileEntry> Files { get; set; } = new List<FileEntry>();
    }

    public class FileEntry
    {
        // Chemin relatif depuis ClientPath (ex. "data/i18n/i18n_fr.d2i"),
        // séparateurs forward-slash quel que soit l'OS du serveur.
        public string Path { get; set; } = string.Empty;

        public string Md5 { get; set; } = string.Empty;

        public long Size { get; set; }
    }

    /// <summary>
    /// Payload de l'event OnStatusUpdate(Downloading, ...) — porte la
    /// progression dans l'UI (StateText + barre de progression).
    /// </summary>
    public class UpdateProgress
    {
        public int Current { get; set; }

        public int Total { get; set; }

        public string CurrentFilePath { get; set; } = string.Empty;
    }
}
