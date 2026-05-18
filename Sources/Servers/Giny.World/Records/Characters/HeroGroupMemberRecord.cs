using Giny.Core.DesignPattern;
using Giny.Core.Pool;
using Giny.ORM.Attributes;
using Giny.ORM.Interfaces;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Giny.World.Records.Characters
{
    /// <summary>
    /// Lien (groupe ↔ character). L'ORM Giny ne supporte qu'un seul [Primary] ; on
    /// ajoute donc un Id technique + UNIQUE (GroupId, CharacterId) côté SQL pour
    /// préserver l'intégrité.
    /// </summary>
    [Table("hero_group_members")]
    public class HeroGroupMemberRecord : IRecord
    {
        [Container]
        private static readonly ConcurrentDictionary<long, HeroGroupMemberRecord> Members
            = new ConcurrentDictionary<long, HeroGroupMemberRecord>();

        private static UniqueLongIdProvider idProvider;

        [Primary]
        public long Id
        {
            get;
            set;
        }

        [Update]
        public long GroupId
        {
            get;
            set;
        }

        [Update]
        public long CharacterId
        {
            get;
            set;
        }

        [Update]
        public byte JoinOrder
        {
            get;
            set;
        }

        [StartupInvoke(StartupInvokePriority.Last)]
        public static void Initialize()
        {
            long lastId = Members.Count > 0 ? Members.Keys.OrderByDescending(x => x).First() : 0;
            idProvider = new UniqueLongIdProvider(lastId);
        }

        public static long NextId()
        {
            return idProvider.Pop();
        }

        public static List<HeroGroupMemberRecord> GetByGroupId(long groupId)
        {
            return Members.Values
                .Where(x => x.GroupId == groupId)
                .OrderBy(x => x.JoinOrder)
                .ToList();
        }

        public static HeroGroupMemberRecord Create(long groupId, long characterId, byte joinOrder)
        {
            return new HeroGroupMemberRecord()
            {
                Id = NextId(),
                GroupId = groupId,
                CharacterId = characterId,
                JoinOrder = joinOrder,
            };
        }
    }
}
