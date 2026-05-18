using Giny.Core.Network.Messages;
using Giny.Core.Time;
using Giny.Protocol.Custom.Enums;
using Giny.Protocol.Messages;
using Giny.Protocol.Types;
using Giny.World.Managers.Entities.Characters;
using Giny.World.Managers.Entities.Look;
using Giny.World.Managers.Maps;
using Giny.World.Network;
using Giny.World.Records;
using Giny.World.Records.Maps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Giny.World.Managers.Entities
{
    public abstract class Entity
    {
        public abstract long Id
        {
            get;
        }
        public abstract string Name
        {
            get;
        }
        public MapPoint Point
        {
            get
            {
                return new MapPoint((short)CellId);
            }
        }
        public abstract short CellId
        {
            get;
            set;
        }
        public MapRecord Map
        {
            get;
            set;
        }

        /// <summary>
        /// Visibilité conditionnelle : si true, l'entité n'est broadcastée qu'au
        /// HiddenOwner. Reste dans m_entities pour la cohérence serveur (FoV,
        /// pathfinding, etc.) mais invisible côté autres clients.
        /// Hors Hero Mode : false par défaut → comportement identique à l'origine.
        /// </summary>
        public bool IsHidden
        {
            get;
            set;
        }

        /// <summary>
        /// Seul client autorisé à voir l'entité quand IsHidden==true. Null +
        /// IsHidden==true ⇒ invisible pour tous.
        /// </summary>
        public WorldClient HiddenOwner
        {
            get;
            set;
        }

        public abstract DirectionsEnum Direction
        {
            get;
            set;
        }

        public abstract ServerEntityLook Look
        {
            get;
            set;
        }

        public Entity(MapRecord map)
        {
            this.Map = map;
        }

        public abstract GameRolePlayActorInformations GetActorInformations(Character target);

        /// <summary>
        /// Visibilité de l'entité pour un viewer donné. Hors Hero Mode toutes les
        /// entités sont visibles. Avec IsHidden==true, seul HiddenOwner voit.
        /// </summary>
        public virtual bool IsVisibleTo(Character viewer)
        {
            if (!IsHidden)
                return true;

            return HiddenOwner != null && HiddenOwner == viewer.Client;
        }

        public void SendMap(NetworkMessage message)
        {
            if (Map != null && Map.Instance != null)
                Map.Instance.Send(message);
        }
        public void RefreshLookOnMap()
        {
            SendMap(new GameContextRefreshEntityLookMessage(Id, Look.ToEntityLook()));
        }

        public void RefreshActorOnMap()
        {
            foreach (var character in Map.Instance.GetEntities<Character>())
            {
                SendMap(new GameRolePlayShowActorMessage(GetActorInformations(character)));
            }
        }
        public void Say(string msg)
        {
            SendMap(new EntityTalkMessage(Id, 4, new string[] { msg }));
        }
        public virtual void DisplaySmiley(short smileyId)
        {
            SendMap(new ChatSmileyMessage(Id, smileyId, 0));
        }
        public static DirectionsEnum RandomDirection(Random random)
        {
            Array values = Enum.GetValues(typeof(DirectionsEnum));
            return (DirectionsEnum)values.GetValue(random.Next(values.Length));
        }
        public static DirectionsEnum RandomDirection4D(Random random)
        {
            DirectionsEnum[] values = new DirectionsEnum[] { DirectionsEnum.DIRECTION_SOUTH_WEST, DirectionsEnum.DIRECTION_SOUTH_EAST ,
            DirectionsEnum.DIRECTION_NORTH_WEST, DirectionsEnum.DIRECTION_NORTH_EAST};

            return (DirectionsEnum)values.GetValue(random.Next(values.Length));
        }
        public CellRecord GetCell()
        {
            return Map.GetCell(CellId);
        }
    }
}
