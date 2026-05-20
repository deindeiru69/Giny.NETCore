using Giny.Core.DesignPattern;
using Giny.Protocol.Custom.Enums;
using Giny.Protocol.Enums;
using Giny.World.Managers.Entities.Characters;
using Giny.World.Managers.Entities.Npcs;
using Giny.World.Managers.Fights;
using Giny.World.Managers.Fights.Fighters;
using Giny.World.Managers.Formulas;
using Giny.World.Records.Quests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Giny.World.Managers.Quests
{
    public class QuestManager : Singleton<QuestManager>
    {
        public CharacterQuestRecord CreateCharacterQuest(QuestRecord record)
        {
            return new CharacterQuestRecord()
            {
                Objectives = record.Steps.First().Objectives.Select(x => x.GetCharacterQuestObjectiveRecord()).ToList(),
                QuestId = record.Id,
                StepId = record.StepIds.First(),
            };
        }

        public void OnNpcTalk(Character character, Npc npc)
        {
            var quests = character.GetActiveQuests();

            foreach (var quest in quests)
            {
                // GoToNpc / NpcTalkBack — comportement historique : on valide
                // dès que le joueur ouvre le dialogue avec le NPC ciblé.
                var talkObjective = quest.GetObjectives(QuestObjectiveTypeEnum.GoToNpc, QuestObjectiveTypeEnum.NpcTalkBack)
                    .FirstOrDefault(x => !x.Done && x.Record.Parameters.Param0 == npc.Template.Id);

                if (talkObjective != null && quest.Available(talkObjective))
                {
                    character.CompleteQuestObjective(quest, talkObjective);
                }

                // BringItemToNpc / GiveItemToNpc — quand le joueur parle au NPC,
                // si l'inventaire contient (au moins) la quantité requise de
                // l'item, on consomme et on valide. Sinon silencieux : le joueur
                // pourra repasser quand il aura l'item. Pas de feedback dialog
                // car le dialogue normal du NPC s'enchaîne ensuite.
                var itemObjectives = quest.GetObjectives(QuestObjectiveTypeEnum.BringItemToNpc, QuestObjectiveTypeEnum.GiveItemToNpc)
                    .Where(x => !x.Done && x.Record.Parameters.Param0 == npc.Template.Id && quest.Available(x))
                    .ToList();

                foreach (var itemObjective in itemObjectives)
                {
                    int itemId = itemObjective.Record.Parameters.Param1;
                    int requiredQty = Math.Max(1, itemObjective.Record.Parameters.Param2);

                    if (TryConsumeItem(character, itemId, requiredQty))
                    {
                        character.CompleteQuestObjective(quest, itemObjective);
                    }
                }
            }
        }

        /// <summary>
        /// Hook fin de combat — appelé une fois par CharacterFighter depuis
        /// CharacterFighter.OnFightEnding. Traque les objectives de combat :
        /// DefeatMonsterOneFight (kills d'un type dans CE combat) et
        /// DefeatMulti (kills cumulés cross-combats via objective.Counter).
        /// </summary>
        public void OnFightEnded(Character character, Fight fight, FightTeam winners)
        {
            // Validation perdants : on ne progresse les quêtes que si le
            // character était sur l'équipe gagnante (logique vanilla Dofus).
            var charFighter = fight.GetFighters<CharacterFighter>(false)
                .FirstOrDefault(cf => cf.Character == character);

            if (charFighter == null || charFighter.Team != winners)
                return;

            // Monstres tués = MonsterFighter sur l'équipe perdante avec !Alive.
            // Les survivants éventuels (rare en PvM Incarnam, mais possible en
            // combat à timer) ne sont pas comptés comme "vaincus".
            var killedMonstersByTemplate = fight.GetFighters<MonsterFighter>(false)
                .Where(m => m.Team != winners && !m.Alive)
                .GroupBy(m => m.Record.Id)
                .ToDictionary(g => (int)g.Key, g => g.Count());

            if (killedMonstersByTemplate.Count == 0)
                return;

            foreach (var quest in character.GetActiveQuests())
            {
                // DefeatMonsterOneFight : Param1 kills requis du monstre Param0
                // dans CE combat. Pas de Counter persistant ; si pas atteint
                // dans ce fight, l'objective recommence à 0 au prochain.
                foreach (var obj in quest.GetObjectives(QuestObjectiveTypeEnum.DefeatMonsterOneFight))
                {
                    if (obj.Done || !quest.Available(obj))
                        continue;

                    int targetMonster = obj.Record.Parameters.Param0;
                    int requiredCount = Math.Max(1, obj.Record.Parameters.Param1);

                    if (killedMonstersByTemplate.TryGetValue(targetMonster, out int killsThisFight)
                        && killsThisFight >= requiredCount)
                    {
                        character.CompleteQuestObjective(quest, obj);
                    }
                }

                // DefeatMulti : compteur cumulatif. À chaque combat où on tue
                // le monstre cible, on incrémente jusqu'à atteindre Param1.
                foreach (var obj in quest.GetObjectives(QuestObjectiveTypeEnum.DefeatMulti))
                {
                    if (obj.Done || !quest.Available(obj))
                        continue;

                    int targetMonster = obj.Record.Parameters.Param0;
                    int requiredCount = Math.Max(1, obj.Record.Parameters.Param1);

                    if (killedMonstersByTemplate.TryGetValue(targetMonster, out int killsThisFight)
                        && killsThisFight > 0)
                    {
                        obj.Counter += killsThisFight;

                        if (obj.Counter >= requiredCount)
                        {
                            character.CompleteQuestObjective(quest, obj);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Hook d'arrivée sur une map — appelé depuis Character.OnEnterMap.
        /// Traque les objectives DiscoverMap.
        /// </summary>
        /// <remarks>
        /// CAVEAT : la sémantique exacte de Parameters.Param0 pour DiscoverMap
        /// n'est pas confirmée. Sur les données D2O 2.68, un seul objective de
        /// ce type existe en Incarnam et son Param0=529467 — qui ne ressemble
        /// pas à un MapId standard (typiquement 9 chiffres, ex 154010883).
        /// Hypothèse : c'est quand même un MapId, juste plus court. Le handler
        /// teste Map.Id == Param0 ; si l'hypothèse est fausse, l'objective ne
        /// se complète jamais mais ne casse rien. À affiner avec des données
        /// in-game ou un dump étendu.
        /// </remarks>
        public void OnMapDiscovered(Character character, long mapId)
        {
            foreach (var quest in character.GetActiveQuests())
            {
                foreach (var obj in quest.GetObjectives(QuestObjectiveTypeEnum.DiscoverMap))
                {
                    if (obj.Done || !quest.Available(obj))
                        continue;

                    if (obj.Record.Parameters.Param0 == mapId)
                    {
                        character.CompleteQuestObjective(quest, obj);
                    }
                }
            }
        }

        /// <summary>
        /// Helper : retire `quantity` items d'id `itemId` de l'inventaire (non
        /// équipés). Renvoie true si l'inventaire en contenait assez. Si moins,
        /// rien n'est retiré (atomic) et renvoie false.
        /// </summary>
        private bool TryConsumeItem(Character character, int itemId, int quantity)
        {
            var stacks = character.Inventory.GetItems()
                .Where(i => i.GId == itemId
                    && i.PositionEnum == CharacterInventoryPositionEnum.INVENTORY_POSITION_NOT_EQUIPED)
                .ToList();

            int totalHeld = stacks.Sum(i => i.Quantity);
            if (totalHeld < quantity)
                return false;

            int remaining = quantity;
            foreach (var stack in stacks)
            {
                if (remaining <= 0)
                    break;

                int take = Math.Min(remaining, stack.Quantity);
                character.Inventory.RemoveItem(stack.UId, take);
                remaining -= take;
            }

            return true;
        }

        public void ApplyRewards(Character character, CharacterQuestRecord quest)
        {
            foreach (var reward in quest.StepRecord.Rewards)
            {

                foreach (var item in reward.ItemRewards)
                {
                    var characterItem = character.Inventory.AddItem(item.ItemId, item.Quantity);

                    if (characterItem != null)
                    {
                        character.NotifyItemGained(item.ItemId,item.Quantity);
                    }
                }

                foreach (short titleId in reward.TitlesReward)
                {
                    character.LearnTitle(titleId);
                }

                foreach (short emoteId in reward.EmoteReward)
                {
                    character.LearnEmote(emoteId);
                }

                if (reward.KamasRatio > 0)
                {
                    long value = AchievementsFormulas.Instance.GetKamasReward(reward.KamasScaleWithPlayerLevel,
                        reward.LevelMin, reward.KamasRatio, 1, character.SafeLevel);

                    character.AddKamas(value);
                }

                foreach (short spellId in reward.SpellsReward)
                {
                    character.LearnSpell(spellId, true);
                }

                if (reward.ExperienceRatio > 0)
                {
                    long value = AchievementsFormulas.Instance.GetExperienceReward(character.SafeLevel,
                         0, reward.LevelMin, reward.ExperienceRatio, 1);

                    //character.AddExperience(value);
                }


            }
        }
    }
}
