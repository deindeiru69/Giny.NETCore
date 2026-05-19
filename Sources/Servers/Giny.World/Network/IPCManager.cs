using Giny.Core;
using Giny.Core.DesignPattern;
using Giny.Core.IO.Configuration;
using Giny.Core.Network.IPC;
using Giny.Protocol.Enums;
using Giny.Protocol.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Giny.World.Network
{
    public class IPCManager : Singleton<IPCManager>
    {
        public bool Connected
        {
            get;
            set;
        }
        private IPCClient Client
        {
            get;
            set;
        }

        public void ConnectToAuth()
        {
            var config = ConfigManager<WorldConfig>.Instance;

            Client = new IPCClient();
            Client.Connect(config.IPCHost, config.IPCPort);
        }

        public void SendRequest<T>(IPCMessage message, IPCRequestManager.RequestCallbackDelegate<T> sucess, IPCRequestManager.RequestCallbackErrorDelegate error) where T : IPCMessage
        {
            IPCRequestManager.SendRequest(Client, message, false, sucess, error);
        }
        public void Send(IPCMessage message)
        {
            Client.Send(message);
        }
        public void OnLostConnection()
        {
            Connected = false;
            Client.Disconnect();
            Client = null;


            Logger.Write("Lost connection to IPC Server. Trying again in 2s", Channels.Warning);

            Thread.Sleep(2000);

            ConnectToAuth();
        }

        public void OnConnected()
        {
            var config = ConfigManager<WorldConfig>.Instance;

            Logger.Write("Connected to IPCServer");
            // On annonce PublicHost (le nom de domaine / IP que les clients
            // utiliseront pour joindre le World), pas Host qui ne sert qu'au
            // bind local et peut valoir 0.0.0.0.
            Client.Send(new HandshakeMessage(config.ServerId, config.PublicHost, (short)config.Port));
            Connected = true;

            if (!WorldServer.Instance.Started)
            {
                WorldServer.Instance.Start(config.Host, config.Port);
            }
            else
            {
                WorldServer.Instance.SendServerStatusToAuth();
            }

        }

        public void OnFailToConnect()
        {
            Logger.Write("Unable to connect to IPC Server. Trying again in 2s", Channels.Warning);
            Thread.Sleep(2000);
            ConnectToAuth();
        }
    }
}
