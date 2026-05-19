using Giny.Core;
using Giny.Protocol.Messages;
using Giny.World.Managers.Experiences;
using Giny.World.Network;
using System;
using System.Linq;
using System.Text;

namespace Giny.World.Game.Heroes
{
    /// <summary>
    /// Hero Mode (Phase 5) — pont serveur ↔ panneau UI (RawPatch HeroPanel.swf).
    ///
    /// Le panneau client communique en deux sens :
    ///  - client → serveur : commandes chat ".hero*" (cf. HeroCommands) ;
    ///  - serveur → client : un TextInformationMessage "sentinelle" dont le
    ///    msgType vaut <see cref="HeroStateMsgType"/> et dont parameters[0]
    ///    contient l'état du groupe sérialisé en JSON. La Frame réseau du patch
    ///    intercepte ce message (et le consomme, donc aucun bruit dans le chat).
    /// </summary>
    public static class HeroPanelManager
    {
        /// <summary>
        /// msgType réservé au panneau héros. Doit être &lt;= 127 : le client lit
        /// ce champ avec un readByte() SIGNÉ (TextInformationMessage._msgTypeFunc),
        /// donc toute valeur &gt; 127 serait reçue négative et la Frame du patch ne
        /// pourrait pas la reconnaître. 99 est hors de la plage des types
        /// d'information chat standard (0..7).
        /// </summary>
        public const byte HeroStateMsgType = 99;

        /// <summary>
        /// Pousse l'état courant du HeroGroup vers le panneau du client.
        /// Sans effet si le client n'a pas de perso en jeu.
        /// </summary>
        public static void SendState(WorldClient client)
        {
            if (client == null || client.Character == null)
                return;

            string json;

            try
            {
                json = BuildStateJson(client);
            }
            catch (Exception ex)
            {
                Logger.Write("[HeroPanel] BuildStateJson a levé une exception : " + ex, Channels.Warning);
                return;
            }

            client.Send(new TextInformationMessage(HeroStateMsgType, 0, new[] { json }));
        }

        private static string BuildStateJson(WorldClient client)
        {
            var group = client.HeroGroup;
            var active = client.Character;

            var sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"hg\":").Append(group != null ? group.Record.Id : -1);
            sb.Append(",\"leader\":").Append(group != null ? group.Record.LeaderId : active.Id);
            sb.Append(",\"active\":").Append(active.Id);
            sb.Append(",\"fight\":").Append(active.Fighting ? "true" : "false");
            sb.Append(",\"max\":").Append(HeroGroup.MaxMembers);

            // --- Membres du groupe ---
            sb.Append(",\"members\":[");
            if (group != null)
            {
                for (int i = 0; i < group.Members.Count; i++)
                {
                    var m = group.Members[i];
                    if (i > 0) sb.Append(',');
                    AppendCharacter(sb, m.Id, m.Name, m.Level, m.Record.BreedId);
                }
            }
            sb.Append(']');

            // --- Persos du compte ajoutables (hors groupe, hors perso actif) ---
            sb.Append(",\"avail\":[");
            bool first = true;
            foreach (var record in client.Characters)
            {
                if (record.Id == active.Id)
                    continue;

                if (group != null && group.Members.Any(m => m.Id == record.Id))
                    continue;

                if (!first) sb.Append(',');
                first = false;

                short level = ExperienceManager.Instance.GetCharacterLevel(record.Experience);
                AppendCharacter(sb, record.Id, record.Name, level, record.BreedId);
            }
            sb.Append(']');

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendCharacter(StringBuilder sb, long id, string name, short level, byte breedId)
        {
            sb.Append("{\"id\":").Append(id);
            sb.Append(",\"n\":\"").Append(Escape(name)).Append('"');
            sb.Append(",\"lvl\":").Append(level);
            sb.Append(",\"breed\":").Append(breedId);
            sb.Append('}');
        }

        /// <summary>
        /// Échappe une chaîne pour insertion dans un littéral JSON. Les noms de
        /// perso Dofus sont déjà restreints, mais on couvre quote/backslash par
        /// sécurité.
        /// </summary>
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
