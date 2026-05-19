using Giny.Core.IO.Interfaces;
using Giny.Core.Network.IPC;
using Giny.Protocol.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Giny.Protocol.IPC.Messages
{
    public class HandshakeMessage : IPCMessage
    {
        public const ushort Id = 6;

        public override ushort MessageId
        {
            get
            {
                return Id;
            }
        }
        public short serverId;

        /// <summary>
        /// Adresse publique annoncée par le World à l'Auth. C'est elle que
        /// l'Auth transmet aux clients (SelectedServerDataMessage) — jamais le
        /// Host de bind, qui peut valoir 0.0.0.0.
        /// </summary>
        public string host;

        public short port;

        public HandshakeMessage()
        {

        }

        public HandshakeMessage(short serverId, string host, short port)
        {
            this.serverId = serverId;
            this.host = host;
            this.port = port;
        }
        public override void Serialize(IDataWriter writer)
        {
            writer.WriteShort(serverId);
            writer.WriteUTF(host ?? string.Empty);
            writer.WriteShort(port);
        }

        public override void Deserialize(IDataReader reader)
        {
            this.serverId = reader.ReadShort();
            this.host = reader.ReadUTF();
            this.port = reader.ReadShort();
        }
    }
}
