using GridTransporter.BoundrySystem;
using GridTransporter.Configs;
using GridTransporter.Utilities;
using NLog;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using WatsonTcp;

namespace GridTransporter.Networking
{
    public class Networking
    {
        //This can all be simplified, but for now it works


        private static readonly Logger Log = LogManager.GetCurrentClassLogger();


        /* Network Settings */
        private static WatsonTcpServer LocalServer;

        private static Settings Configs { get { return Main.Config; } }

        //private Timer HeartBeatTimer = new Timer(HeartBeatTimerMS);

        private static ConcurrentQueue<MessageQueue> QueuedMessages = new ConcurrentQueue<MessageQueue>();
        private static Timer HeartbeatTimer = new Timer(5000);








        public Networking()
        {


            //Initilize Server Listener


            foreach (var Region in Configs.JumpRegionGrid)
            {
                if (Configs.ServerDestinations.Count <= Region.ServerID)
                    continue;

                Region.Client = Configs.ServerDestinations[Region.ServerID];
                Log.Info($"Registered {Region.Name} to {Region.Client.ServerName}");
            }




            HeartbeatTimer.Elapsed += QueueTimer_Elapsed;
            HeartbeatTimer.Start();

            StartServerListener();
        }
        

        private void QueueTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                foreach (var ServerDest in Configs.ServerDestinations)
                {
                    ServerDest.Connect();
                }

            }catch(Exception ex)
            {
                //Log.Error(ex);
            }
        }

        private void StartServerListener()
        {


            LocalServer = new WatsonTcpServer(null, Configs.ListenerPort);


            LocalServer.Events.StreamReceived += Events_StreamReceived;
            LocalServer.Events.ClientDisconnected += Events_ClientDisconnected;
            LocalServer.Events.ClientConnected += Events_ClientConnected1;

            LocalServer.Start();

            Log.Info($"Registered Listener on: *:{Configs.ListenerPort}");
        }

        private void Events_ClientConnected1(object sender, ConnectionEventArgs e)
        {
            //Log.Info("A Client has been Connected!");
        }

        private void Events_ClientDisconnected(object sender, DisconnectionEventArgs e)
        {
            //Log.Info("A Client has been disconnected!");
        }

        private void Events_StreamReceived(object sender, StreamReceivedEventArgs e)
        {
            long bytesRemaining = e.ContentLength;
            int bytesRead = 0;
            byte[] buffer = new byte[65536];

            using (MemoryStream ms = new MemoryStream())
            {
                while (bytesRemaining > 0)
                {
                    bytesRead = e.DataStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                    }
                }

                ServerMessageRecieved(ms.ToArray());
            }
        }

      

        private async void ServerMessageRecieved(byte[] Data)
        {
            //Log.Info("Data Recieved!");


            try
            {
                MessageConstruct Construct = Utilities.NetworkUtility.Deserialize<MessageConstruct>(Data);
                await Construct.Decompile();


                //Log.Info(Construct.Type);

            }catch(Exception ex)
            {
                Log.Error(ex);
            }



        }


        public void Close()
        {

            HeartbeatTimer.Stop();

            foreach (var ServerDest in Configs.ServerDestinations)
            {
                ServerDest.Disconnect();
            }

            LocalServer.Events.StreamReceived -= Events_StreamReceived;
            LocalServer.Events.ClientDisconnected -= Events_ClientDisconnected;
            LocalServer.Events.ClientConnected -= Events_ClientConnected1;


            LocalServer.Dispose();
        }




        public static Task PublishMessage<T>(MessageType type, T Data, TargetClient target)
        {
            //Log.Info($"Sending: data to {target.ServerName}");


            try
            {
                byte[] Serialized = Utilities.NetworkUtility.Serialize(Data);

                MessageConstruct Construct = new MessageConstruct(type, Serialized);
                return target.SendData(Utilities.NetworkUtility.Serialize(Construct));

            }catch(Exception ex)
            {
                Log.Error(ex);
                return Task.CompletedTask;
            }
        }
    }
}
