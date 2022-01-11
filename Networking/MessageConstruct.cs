using GridTransporter.BoundrySystem;
using NLog;
using ProtoBuf;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridTransporter.Networking
{
    [ProtoContract]
    public class MessageConstruct
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        [ProtoMember(10)]
        public readonly MessageType Type;

        [ProtoMember(20)]
        public readonly string FromIP;

        [ProtoMember(21)]
        public int GamePort;

        [ProtoMember(40)]
        private readonly byte[] Data;

        public DateTime Timer;


        
        public MessageConstruct(MessageType Type, byte[] Data)
        {
            this.Type = Type;
            this.Data = Data;
            GetGamePort();
        }

        public MessageConstruct() { }

        private void GetGamePort()
        {
            GamePort = MyDedicatedServerOverrides.Port ?? MySandboxGame.ConfigDedicated.ServerPort;
        }


        public async Task Decompile()
        {
            switch (Type)
            {
                case MessageType.GridTransport:

                    GridTransport Transport = Utilities.NetworkUtility.Deserialize<GridTransport>(Data);
                    await Transport.SpawnAsync();
                    break;


                default:
                    Log.Info("Unkown message type!");
                    break;


            }
        }
    }


    public class MessageQueue
    {



    }
}
