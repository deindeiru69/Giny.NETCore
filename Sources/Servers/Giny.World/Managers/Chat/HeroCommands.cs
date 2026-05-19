using Giny.ORM;
using Giny.Protocol.Custom.Enums;
using Giny.World.Game.Heroes;
using Giny.World.Managers.Entities.Characters;
using Giny.World.Network;
using Giny.World.Records.Characters;
using System;
using System.Linq;
using System.Text;

namespace Giny.World.Managers.Chat
{
    /// <summary>
    /// Commandes chat pour piloter le HeroGroup d'un account. Préfixe Giny : ".".
    /// Le système chat de Giny n'accepte pas de sous-commandes natives (matching
    /// strict des arguments), d'où une commande par action plutôt qu'un seul
    /// ".hero <verb>".
    /// </summary>
    class HeroCommands
    {
        [ChatCommand("hero", ServerRoleEnum.Player)]
        public static void HeroCommand(WorldClient client)
        {
            if (client.HeroGroup == null)
            {
                client.Character.Reply("Aucun groupe de héros. Crée-en un avec .herocreate");
                return;
            }

            var group = client.HeroGroup;
            var sb = new StringBuilder();
            sb.AppendLine($"HeroGroup #{group.Record.Id} : {group.Members.Count}/{HeroGroup.MaxMembers} membre(s).");

            for (int i = 0; i < group.Members.Count; i++)
            {
                Character m = group.Members[i];
                bool isLeader = m.Id == group.Record.LeaderId;
                bool isActive = m.Id == client.Character.Id;
                string suffix = string.Empty;
                if (isLeader) suffix += " — leader";
                if (isActive) suffix += " — actif";
                sb.AppendLine($"  [{i}] {m.Name} (id {m.Id}, lvl {m.Level}, breed {m.Record.BreedId}){suffix}");
            }

            client.Character.Reply(sb.ToString());
        }

        [ChatCommand("herocreate", ServerRoleEnum.Player)]
        public static void HeroCreateCommand(WorldClient client)
        {
            if (client.HeroGroup != null)
            {
                client.Character.ReplyWarning(
                    $"Tu as déjà un groupe de héros (#{client.HeroGroup.Record.Id}).");
                return;
            }

            var record = HeroGroupRecord.Create(client.Account.Id, client.Character.Id);
            record.AddNow();

            var link = HeroGroupMemberRecord.Create(record.Id, client.Character.Id, 0);
            link.AddNow();

            client.HeroGroup = new HeroGroup(client, record);
            client.HeroGroup.Load();

            client.Character.Reply(
                $"HeroGroup créé (#{record.Id}). Leader : {client.Character.Name}.");

            HeroPanelManager.SendState(client);
        }

        /// <summary>
        /// Pousse l'état du groupe vers le panneau UI. Émise par le panneau à
        /// son ouverture ; aucune sortie chat.
        /// </summary>
        [ChatCommand("herostate", ServerRoleEnum.Player)]
        public static void HeroStateCommand(WorldClient client)
        {
            HeroPanelManager.SendState(client);
        }

        [ChatCommand("heroadd", ServerRoleEnum.Player)]
        public static void HeroAddCommand(WorldClient client, string name)
        {
            if (client.HeroGroup == null)
            {
                client.Character.ReplyWarning("Pas de groupe. Crée-en un avec .herocreate");
                return;
            }

            var group = client.HeroGroup;

            if (client.Character.Id != group.Record.LeaderId)
            {
                client.Character.ReplyWarning("Seul le leader peut ajouter un héros.");
                return;
            }

            if (group.Members.Count >= HeroGroup.MaxMembers)
            {
                client.Character.ReplyWarning(
                    $"Groupe plein ({HeroGroup.MaxMembers}).");
                return;
            }

            var target = CharacterRecord.GetCharacterRecords().FirstOrDefault(
                r => r.AccountId == client.Account.Id
                  && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                client.Character.ReplyError($"Aucun perso '{name}' sur ton compte.");
                return;
            }

            if (group.Members.Any(m => m.Id == target.Id))
            {
                client.Character.ReplyWarning($"{target.Name} est déjà dans le groupe.");
                return;
            }

            var character = new Character(client, target);
            group.AddMember(character);

            client.Character.Reply(
                $"{target.Name} ajouté au groupe (position {group.Members.Count - 1}).");

            HeroPanelManager.SendState(client);
        }

        [ChatCommand("heroswitch", ServerRoleEnum.Player)]
        public static void HeroSwitchCommand(WorldClient client, string name)
        {
            if (client.HeroGroup == null)
            {
                client.Character.ReplyWarning("Pas de groupe.");
                return;
            }

            var group = client.HeroGroup;

            if (client.Character.Fighting)
            {
                client.Character.ReplyWarning("Impossible de switcher en combat.");
                return;
            }

            if (client.Character.Busy)
            {
                client.Character.ReplyWarning("Impossible de switcher : action en cours.");
                return;
            }

            var target = group.Members.FirstOrDefault(
                m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                client.Character.ReplyError($"{name} n'est pas dans ton groupe.");
                return;
            }

            if (target == client.Character)
            {
                client.Character.ReplyWarning($"{target.Name} est déjà le perso actif.");
                return;
            }

            group.SwitchActive(target);
            client.Character.Reply($"Actif : {target.Name}.");

            HeroPanelManager.SendState(client);
        }

        [ChatCommand("heroleader", ServerRoleEnum.Player)]
        public static void HeroLeaderCommand(WorldClient client, string name)
        {
            if (client.HeroGroup == null)
            {
                client.Character.ReplyWarning("Pas de groupe.");
                return;
            }

            var group = client.HeroGroup;

            if (client.Character.Id != group.Record.LeaderId)
            {
                client.Character.ReplyWarning("Seul le leader peut transmettre le rôle.");
                return;
            }

            var target = group.Members.FirstOrDefault(
                m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                client.Character.ReplyError($"{name} n'est pas dans ton groupe.");
                return;
            }

            group.SetLeader(target);
            client.Character.Reply($"Leader transmis à {target.Name}.");

            HeroPanelManager.SendState(client);
        }

        [ChatCommand("heroremove", ServerRoleEnum.Player)]
        public static void HeroRemoveCommand(WorldClient client, string name)
        {
            if (client.HeroGroup == null)
            {
                client.Character.ReplyWarning("Pas de groupe.");
                return;
            }

            var group = client.HeroGroup;

            if (client.Character.Id != group.Record.LeaderId)
            {
                client.Character.ReplyWarning("Seul le leader peut retirer un héros.");
                return;
            }

            if (client.Character.Fighting)
            {
                client.Character.ReplyWarning("Impossible de retirer un héros en combat.");
                return;
            }

            var target = group.Members.FirstOrDefault(
                m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                client.Character.ReplyError($"{name} n'est pas dans ton groupe.");
                return;
            }

            try
            {
                group.RemoveMember(target);
            }
            catch (InvalidOperationException ex)
            {
                client.Character.ReplyWarning(ex.Message);
                return;
            }

            client.Character.Reply($"{target.Name} retiré du groupe.");

            HeroPanelManager.SendState(client);
        }
    }
}
