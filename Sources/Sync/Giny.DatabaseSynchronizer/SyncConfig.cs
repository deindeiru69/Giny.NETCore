using Giny.Core.IO.Configuration;

namespace Giny.DatabaseSynchronizer
{
    public class SyncConfig : IConfigFile
    {
        public string SQLHost { get; set; } = "127.0.0.1";

        public int SQLPort { get; set; } = 3306;

        public string SQLUser { get; set; } = "root";

        public string SQLPassword { get; set; } = "";

        public string SQLDBName { get; set; } = "giny_world";

        // Path racine du client Dofus (doit contenir data/common/ et data/i18n/).
        // Remplace l'ancienne ClientConstants.ClientPath hardcodée.
        public string ClientPath { get; set; } = "";

        // Drops + recreates les 34 tables D2O puis ré-importe depuis les .d2o du client.
        // DESTRUCTIF. Le template config.Production.example.json le laisse à true pour
        // refléter le workflow nominal mais le prompt interactif protège quand même.
        public bool SyncD2O { get; set; } = true;

        // Drops + recreates la table maps. DESTRUCTIF et très long (rebuild complet
        // des maps depuis maps0.d2p). Laissé à false par défaut, à activer seulement
        // pour un rebuild complet.
        public bool SyncMaps { get; set; } = false;

        public void OnCreated() { }

        public void OnLoaded() { }
    }
}
