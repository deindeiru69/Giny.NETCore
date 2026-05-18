using Giny.Core.DesignPattern;
using Giny.Core.Pool;
using Giny.ORM.Attributes;
using Giny.ORM.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Giny.World.Records.Characters
{
    /// <summary>
    /// Une row par groupe de héros. Un account possède au plus un groupe (logique
    /// applicative, pas contrainte SQL). Le leader est l'un des Character listés
    /// dans hero_group_members.
    /// </summary>
    [Table("hero_groups")]
    public class HeroGroupRecord : IRecord
    {
        [Container]
        private static readonly ConcurrentDictionary<long, HeroGroupRecord> Groups
            = new ConcurrentDictionary<long, HeroGroupRecord>();

        private static UniqueLongIdProvider idProvider;

        [Primary]
        public long Id
        {
            get;
            set;
        }

        [Update]
        public int AccountId
        {
            get;
            set;
        }

        [Update]
        public long LeaderId
        {
            get;
            set;
        }

        /// <summary>
        /// Renseigné à la création (pas marqué [Update] : jamais modifié après INSERT).
        /// La colonne SQL a DEFAULT CURRENT_TIMESTAMP, utilisée seulement si une row
        /// est insérée hors ORM (script manuel).
        /// </summary>
        public DateTime CreatedAt
        {
            get;
            set;
        }

        [StartupInvoke(StartupInvokePriority.Last)]
        public static void Initialize()
        {
            long lastId = Groups.Count > 0 ? Groups.Keys.OrderByDescending(x => x).First() : 0;
            idProvider = new UniqueLongIdProvider(lastId);
        }

        public static long NextId()
        {
            return idProvider.Pop();
        }

        public static HeroGroupRecord GetByAccountId(int accountId)
        {
            return Groups.Values.FirstOrDefault(x => x.AccountId == accountId);
        }

        public static HeroGroupRecord Create(int accountId, long leaderId)
        {
            return new HeroGroupRecord()
            {
                Id = NextId(),
                AccountId = accountId,
                LeaderId = leaderId,
                CreatedAt = DateTime.UtcNow,
            };
        }
    }
}
