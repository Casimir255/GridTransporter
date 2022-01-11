using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WatsonTcp;
using System.IO;

namespace GridTransporter.Networking
{
    public class TargetClient
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private WatsonTcpClient ClientTCP;
        private bool _Setup = false;



        public int ServerID { get; set; }




        public string ServerName { get; set; }
        public string ServerIP { get; set; }
        public string GamePort { get; set; }

        private short _GamePort = 0;
        


        public string GridTransportPort { get; set; }
        public string ServerPassword { get; set; }

        private bool Connected { get { return ClientTCP.Connected; } }



        private void Setup()
        {
            if (!Int16.TryParse(GridTransportPort, out short result))
                return;


            Int16.TryParse(GamePort, out _GamePort);
               

            ClientTCP = new WatsonTcpClient(ServerIP, result);

            //ClientTCP.Events.StreamReceived += Events_StreamReceived; ;
            ClientTCP.Events.ServerConnected += Events_ServerConnected;
            ClientTCP.Events.ServerDisconnected += Events_ServerDisconnected;
            ClientTCP.Events.StreamReceived += Events_StreamReceived;


            _Setup = true;
        }

        private void Events_StreamReceived(object sender, StreamReceivedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void Events_ServerDisconnected(object sender, DisconnectionEventArgs e)
        {
            Log.Warn($"Client {ServerName} Disconnected!");
            //throw new NotImplementedException();
        }

        private void Events_ServerConnected(object sender, ConnectionEventArgs e)
        {
            Log.Warn($"Client {ServerName} Connected!");
            //throw new NotImplementedException();
        }

        public void Connect()
        {
            if (_Setup == false)
                Setup();


            if (!Connected)
            {
                //Log.Info($"Attempting to connect to {ServerName}! {ServerIP}:{GridTransportPort}");

                try
                {
                    ClientTCP.Connect();
                }
                catch(Exception ex)
                {
                    //Do nothing
                    //Log.Error(ex);
                }
            }
        }

        public short GetGamePort()
        {
            return _GamePort;
        }

        public void Disconnect()
        {
            if (Connected)
                ClientTCP.Disconnect();


            ClientTCP.Events.ServerConnected -= Events_ServerConnected;
            ClientTCP.Events.ServerDisconnected -= Events_ServerDisconnected;

            ClientTCP.Dispose();
        }

        public bool IsConnected()
        {
            return Connected;
        }


        public Task<bool> SendData(byte[] Data)
        {
            return ClientTCP.SendAsync(Data);
        }
    }
}
