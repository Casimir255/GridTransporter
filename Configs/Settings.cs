using GridTransporter.Networking;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Torch;

namespace GridTransporter.Configs
{
    public class Settings : ViewModel
    {

        private bool _EnablePlugin = true;
        public bool EnablePlugin { get => _EnablePlugin; set => SetValue(ref _EnablePlugin, value); }


        private int _ThisServerID = 1;
        public int ThisServerID { get => _ThisServerID; set => SetValue(ref _ThisServerID, value); }


        //Port on which this server listens for responses
        private int _ListenerPort = 18076;
        public int ListenerPort { get => _ListenerPort; set => SetValue(ref _ListenerPort, value); }


        //Authenication string for gamepanels
        private string _Password = "DefaultPassword";
        public string Password { get => _Password; set => SetValue(ref _Password, value); }


        private ObservableCollection<TargetClient> _ServerDestinations = new ObservableCollection<TargetClient>();
        public ObservableCollection<TargetClient> ServerDestinations { get => _ServerDestinations; set => SetValue(ref _ServerDestinations, value); }



        private ObservableCollection<JumpRegion> _JumpRegionGrid = new ObservableCollection<JumpRegion>();
        public ObservableCollection<JumpRegion> JumpRegionGrid { get => _JumpRegionGrid; set => SetValue(ref _JumpRegionGrid, value); }

    }


    public class JumpRegion
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public int ServerID { get; set; }


        [XmlIgnoreAttribute]
        public TargetClient Client { get; set; }

        public string Name { get; set; } = "NewRegion";

        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
        public double Z { get; set; } = 0;

        public double Radius { get; set; } = .5;

        public string ScriptName { get; set; }

        public int Timer { get; set; } = 5;

        public double ToX { get; set; } = 0;
        public double ToY { get; set; } = 0;
        public double ToZ { get; set; } = 0;



       


    }



}
