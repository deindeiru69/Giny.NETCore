using Giny.Core.Network.Messages;
using Giny.Protocol.Messages;
using Giny.World.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Giny.World.Managers.Fights;
using System.Threading.Tasks;
using Giny.World.Managers.Fights.Fighters;
using Giny.Protocol.Enums;
using Giny.World.Records.Spells;
using Giny.World.Managers.Spells;
using Giny.World.Managers.Hardcore;

namespace Giny.World.Handlers.Fights
{
    class FightsHandler
    {
        [MessageHandler]
        public static void HandleGameActionFightCastRequestMessage(GameActionFightCastOnTargetRequestMessage message, WorldClient client)
        {
            if (!client.Character.Fighting)
            {
                return;
            }

            // Hero Mode (Phase 4.2) — l'auteur de l'action = le fighter contrôlé
            // (héros dont c'est le tour), sinon le perso actif.
            var fighter = client.GetActiveFighter();
            if (fighter == null)
            {
                return;
            }

            Fighter target = fighter.Fight.GetFighter<Fighter>(x => x.Id == (long)message.targetId);

            if (target != null)
            {
                fighter.CastSpell(message.spellId, target.Cell.Id);
            }
        }
        [MessageHandler]
        public static void HandleGameActionFightCastRequestMessage(GameActionFightCastRequestMessage message, WorldClient client)
        {
            if (!client.Character.Fighting)
            {
                return;
            }

            client.GetActiveFighter()?.CastSpell(message.spellId, message.cellId);
        }
        [MessageHandler]
        public static void HandleGameContextQuitMessage(GameContextQuitMessage message, WorldClient client)
        {

            if (client.Character.Fighting)
            {
                if (!client.Character.Fighter.CanQuitFight())
                {
                    client.Character.TextInformation(TextInformationTypeEnum.TEXT_INFORMATION_ERROR, 288);
                    return;
                }

                client.Character.Fighter.Leave(true);

            }
        }
        [MessageHandler]
        public static void HandleGameFightPlacementPositionRequestMessage(GameFightPlacementPositionRequestMessage message, WorldClient client)
        {
            if (client.Character.Fighting && !client.Character.Fighter.Fight.Started && !client.Character.Fighter.IsReady && client.Character.Fighter.Fight.IsCellFree(client.Character.Map.GetCell(message.cellId)))
            {
                client.Character.Fighter.ModifyPlacement((short)message.cellId);
            }
        }
        [MessageHandler]
        public static void HandleGameFightPlacementSwapPositionsRequestMessage(GameFightPlacementSwapPositionsRequestMessage message, WorldClient client)
        {
            if (client.Character.Fighting && !client.Character.Fighter.IsReady && !client.Character.Fighter.Fight.Started)
            {
                var target = client.Character.Fighter.Fight.GetFighter<Fighter>(x => x.Id == message.requestedId);

                if (target.Cell.Id == message.cellId)
                {
                    client.Character.Fighter.SwapPlacementPosition(target);
                }
            }
        }

        [MessageHandler]
        public static void HandleGameFightJoinRequestMessage(GameFightJoinRequestMessage message, WorldClient client)
        {
            Fight fight = client.Character.Map.Instance.GetFight(message.fightId);

            if (fight != null && !client.Character.Fighting)
            {
                fight.Join(client.Character, message.fighterId);
            }
        }
        [MessageHandler]
        public static void HandleGameFightReady(GameFightReadyMessage message, WorldClient client)
        {
            if (!client.Character.Fighting)
                return;

            client.Character.Fighter.ToggleReady(message.isReady);

            // Hero Mode (Phase 4.2) — le "prêt" du joueur vaut pour tous ses
            // héros engagés dans le même combat (ils ne peuvent pas être
            // ready'd individuellement par le client).
            if (client.HeroGroup != null)
            {
                var fight = client.Character.Fighter.Fight;

                foreach (var hero in client.HeroGroup.Members)
                {
                    if (hero == client.Character)
                        continue;

                    var heroFighter = hero.Fighter;

                    if (heroFighter != null
                        && heroFighter.Fight == fight
                        && heroFighter.IsReady != message.isReady)
                    {
                        heroFighter.ToggleReady(message.isReady);
                    }
                }
            }
        }
        [MessageHandler]
        public static void HandleGameGameContextKickMessage(GameContextKickMessage message, WorldClient client)
        {
            if (client.Character.Fighting)
            {
                Fighter target = client.Character.Fighter.Fight.GetFighter<Fighter>(x => x.Id == message.targetId);

                if (target != null)
                {
                    target.Kick(client.Character.Fighter);
                }
            }
        }
        [MessageHandler]
        public static void HandleShowCellRequestMessage(ShowCellRequestMessage message, WorldClient client)
        {
            if (client.Character.Fighting)
            {
                var fighter = client.GetActiveFighter();
                if (fighter != null)
                {
                    fighter.Team.ShowCell(fighter, message.cellId);
                }
            }
        }
        [MessageHandler]
        public static void HandleFightOptionToggle(GameFightOptionToggleMessage message, WorldClient client)
        {
            if (client.Character.Fighting)
                client.Character.Fighter.Team.Options.ToggleOption((FightOptionsEnum)message.option);
        }


        [MessageHandler]
        public static void HandleGameActionAcknowledgementMessage(GameActionAcknowledgementMessage message, WorldClient client)
        {
            if (!message.valid || !client.Character.Fighting)
                return;

            var fighter = client.GetActiveFighter() as CharacterFighter;

            if (fighter != null && fighter.IsFighterTurn)
            {
                fighter.Fight.SequenceManager.AcknowledgeAction(fighter, message.actionId);
            }
        }
        [MessageHandler]
        public static void HandleGameFightTurnFinishMessage(GameFightTurnFinishMessage message, WorldClient client)
        {
            if (!client.Character.Fighting)
                return;

            client.GetActiveFighter()?.PassTurn();
        }
        [MessageHandler]
        public static void HandleGameFightTurnReadyMessage(GameFightTurnReadyMessage message, WorldClient client)
        {
            if (!client.Character.Fighting)
                return;

            (client.GetActiveFighter() as CharacterFighter)?.ToggleTurnReady(message.isReady);
        }
    }
}
