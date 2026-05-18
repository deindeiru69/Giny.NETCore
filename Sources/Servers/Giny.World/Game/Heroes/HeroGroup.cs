using Giny.ORM;
using Giny.World.Managers.Entities.Characters;
using Giny.World.Managers.Parties;
using Giny.World.Network;
using Giny.World.Records.Characters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Giny.World.Game.Heroes
{
    /// <summary>
    /// Représentation in-memory d'un groupe de héros, lié à un WorldClient.
    /// Détient le HeroGroupRecord (DB) + la liste des Character réellement chargés.
    ///
    /// Phase 1 : les Character ne sont PAS encore instanciés ici ; la liste Members
    /// reste vide. Seule la liaison record + WorldClient est posée.
    /// La Phase 2 ajoutera Load(), SetActive(), broadcast filtering, etc.
    /// </summary>
    public class HeroGroup
    {
        public const int MaxMembers = 8;

        public HeroGroupRecord Record
        {
            get;
        }

        public WorldClient Client
        {
            get;
        }

        /// <summary>
        /// Liste des Character réellement instanciés en mémoire.
        /// Phase 1 : reste vide. Phase 2 : Load() peuplera depuis les CharacterRecord
        /// des HeroGroupMemberRecord.
        /// </summary>
        public List<Character> Members
        {
            get;
        } = new List<Character>();

        /// <summary>
        /// Référence en mémoire des CharacterRecord liés à ce groupe (chargés au login,
        /// avant instanciation des Character). Phase 2 utilisera cette liste pour
        /// matérialiser les Character.
        /// </summary>
        public List<CharacterRecord> MemberRecords
        {
            get;
        } = new List<CharacterRecord>();

        public Character Leader
            => Members.FirstOrDefault(c => c.Id == Record.LeaderId);

        /// <summary>
        /// Party "système" regroupant les héros pour l'affichage UI (panneau de
        /// groupe). Null tant qu'elle n'a pas été créée (EnsureParty) ou si le
        /// groupe n'a qu'un seul membre.
        /// </summary>
        public Party HeroParty
        {
            get;
            private set;
        }

        public HeroGroup(WorldClient client, HeroGroupRecord record)
        {
            Client = client;
            Record = record;
        }

        /// <summary>
        /// Crée (une seule fois) la Party système qui regroupe tous les héros,
        /// pour qu'ils apparaissent dans le panneau de groupe du client.
        /// Idempotent : ne fait rien si la Party existe déjà ou si le groupe
        /// n'a qu'un membre. À appeler quand le perso actif est en jeu.
        /// </summary>
        public void EnsureParty()
        {
            if (HeroParty != null)
                return;

            if (Members.Count < 2)
                return;

            var leader = Leader ?? Members[0];

            var party = PartyManager.Instance.CreateParty(leader);
            party.IsHeroParty = true;

            // Leader d'abord, puis les autres héros — ajout direct, sans
            // mécanisme d'invitation.
            party.AddMember(leader);

            foreach (var hero in Members)
            {
                if (hero != leader)
                {
                    party.AddMember(hero);
                }
            }

            HeroParty = party;
        }

        /// <summary>
        /// Matérialise les Character du groupe depuis la DB. Pour chaque
        /// HeroGroupMemberRecord :
        ///  - récupère le CharacterRecord (silencieusement ignoré s'il a été
        ///    supprimé entre-temps),
        ///  - réutilise Client.Character si la row matche le perso actif (pas
        ///    de duplication),
        ///  - sinon instancie un nouveau Character — qui reste hors map / hors
        ///    réseau tant qu'on ne l'attache pas explicitement (le constructeur
        ///    se contente d'allocations + lectures DB d'inventaire/banque).
        /// </summary>
        public void Load()
        {
            MemberRecords.Clear();
            Members.Clear();

            foreach (var link in HeroGroupMemberRecord.GetByGroupId(Record.Id))
            {
                var record = CharacterRecord.GetCharacterRecord(link.CharacterId);

                if (record == null)
                    continue;

                MemberRecords.Add(record);

                Character character;
                if (Client.Character != null && Client.Character.Id == record.Id)
                {
                    character = Client.Character;
                }
                else
                {
                    character = new Character(Client, record);
                }

                Members.Add(character);
            }
        }

        /// <summary>
        /// Ajoute un Character au groupe : persiste un HeroGroupMemberRecord et
        /// pousse le Character dans Members. Le character ne doit pas déjà être
        /// présent ; la capacité MaxMembers doit être respectée.
        /// </summary>
        public void AddMember(Character character)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            if (Members.Count >= MaxMembers)
                throw new InvalidOperationException($"HeroGroup is full ({MaxMembers}).");

            if (Members.Any(m => m.Id == character.Id))
                throw new InvalidOperationException(
                    $"Character {character.Id} is already a member of HeroGroup {Record.Id}.");

            byte joinOrder = (byte)Members.Count;
            var link = HeroGroupMemberRecord.Create(Record.Id, character.Id, joinOrder);
            link.AddNow();

            Members.Add(character);
            MemberRecords.Add(character.Record);
        }

        /// <summary>
        /// Matérialise les héros (non-actifs) sur la map du perso actif.
        /// Chaque héros est marqué IsHidden=true, HiddenOwner=Client : seul ce
        /// client le voit grâce au filtre Entity.IsVisibleTo / MapInstance.
        /// Idempotent (AddEntity ne ré-ajoute pas).
        /// </summary>
        public void MaterializeOnMap()
        {
            var active = Client.Character;

            if (active == null || active.Map == null || active.Map.Instance == null)
                return;

            var map = active.Map;

            foreach (var hero in Members)
            {
                if (hero == active)
                    continue;

                // Si le héros traîne sur une autre map (reboot / state stale),
                // on le retire avant la pose sur la nouvelle.
                if (hero.Map != null && hero.Map.Instance != null && hero.Map.Id != map.Id)
                {
                    hero.Map.Instance.RemoveEntity(hero.Id);
                }

                hero.Map = map;
                hero.CellId = active.CellId;
                hero.IsHidden = true;
                hero.HiddenOwner = Client;

                map.Instance.AddEntity(hero);
            }
        }

        /// <summary>
        /// Retire tous les héros non-actifs de leur map courante (avant un
        /// teleport ou une déconnexion du perso actif).
        /// </summary>
        public void DematerializeFromMap()
        {
            var active = Client.Character;

            foreach (var hero in Members)
            {
                if (hero == active)
                    continue;

                if (hero.Map != null && hero.Map.Instance != null)
                {
                    hero.Map.Instance.RemoveEntity(hero.Id);
                }
            }
        }

        /// <summary>
        /// Switch le perso actif (Client.Character) vers un autre membre du
        /// groupe — hors combat. Le nouveau actif devient visible sur la map,
        /// l'ancien passe en mode héros caché.
        ///
        /// Caller doit avoir vérifié : pas en combat, target différent du
        /// perso actif courant, target présent dans Members.
        /// </summary>
        public void SwitchActive(Character target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var oldActive = Client.Character;

            if (target == oldActive)
                return;

            if (!Members.Contains(target))
                throw new InvalidOperationException(
                    $"Character {target.Id} is not a member of HeroGroup {Record.Id}.");

            var map = oldActive.Map;
            var cell = oldActive.CellId;

            if (map == null || map.Instance == null)
                throw new InvalidOperationException("Active character has no map context.");

            // 1. Retirer tous les héros hidden de la map (target inclus).
            DematerializeFromMap();

            // 2. Retirer le perso actif courant (broadcast disappear aux autres
            //    clients qui le voyaient).
            map.Instance.RemoveEntity(oldActive.Id);

            // 3. L'ancien actif devient un héros hidden.
            oldActive.IsHidden = true;
            oldActive.HiddenOwner = Client;

            // 4. Le nouveau actif perd son statut hidden et reprend la position
            //    de l'ancien (même cellule, même map).
            target.IsHidden = false;
            target.HiddenOwner = null;
            target.Map = map;
            target.CellId = cell;
            target.Record.MapId = map.Id;
            target.Record.CellId = cell;

            // 5. Swap côté client.
            Client.Character = target;

            // 6. Reconstruire l'UI : inventaire, sorts, stats, raccourcis, etc.
            //    OnCharacterLoadingComplete (à la fin de RebuildSessionUI) ne
            //    fait pas l'AddEntity ; on l'enchaîne explicitement via
            //    OnEnterMap, qui broadcast l'apparition + matérialise les héros
            //    (oldActive sera maintenant traité comme un hero hidden).
            target.RebuildSessionUI();
            target.OnEnterMap();
        }

        /// <summary>
        /// Change le leader. Le nouveau leader doit déjà être dans Members
        /// (== avoir été matérialisé). Persiste via UpdateLater().
        /// </summary>
        public void SetLeader(Character character)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            if (!Members.Contains(character))
                throw new InvalidOperationException(
                    $"Character {character.Id} is not a member of HeroGroup {Record.Id}.");

            Record.LeaderId = character.Id;
            Record.UpdateLater();
        }
    }
}
