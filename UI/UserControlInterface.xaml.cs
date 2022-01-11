using GridTransporter.Configs;
using GridTransporter.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GridTransporter.UI
{
    /// <summary>
    /// Interaction logic for UserControlInterface.xaml
    /// </summary>
    public partial class UserControlInterface : UserControl
    {
        private Settings Configs { get { return Main.Config; } }

        public UserControlInterface()
        {
            DataContext = Configs;
            InitializeComponent();
        }

        private void DeleteButtonClick(object sender, RoutedEventArgs e)
        {
            //int SelectedIndex = JumpRegionGrid.SelectedIndex;
           // Configs.JumpRegionGrid.RemoveAt(SelectedIndex);
            //Configs.RefreshModel();
        }

        private void AddNewServerClick(object sender, RoutedEventArgs e)
        {
            TargetClient NewClient = new TargetClient();
            NewClient.ServerName = "NewServer";
            NewClient.ServerIP = "127.0.0.1";
            NewClient.GamePort = "27016";
            NewClient.GridTransportPort = "3000";
            NewClient.ServerPassword = "OoogaBooga";
            NewClient.ServerID = Configs.ServerDestinations.Count;

            Configs.ServerDestinations.Add(NewClient);

            //JumpRegion Region = new JumpRegion();
           // Configs.JumpRegionGrid.Add(Region);
            Configs.RefreshModel();
        }

        private void AddNewRegionClick(object sender, RoutedEventArgs e)
        {
            JumpRegion Region = new JumpRegion();
            Region.Name = "NewRegion";
            Region.Radius = 1;


            Configs.JumpRegionGrid.Add(Region);

            //JumpRegion Region = new JumpRegion();
            // Configs.JumpRegionGrid.Add(Region);
            Configs.RefreshModel();
        }

        private void DataGrid_LostFocus(object sender, RoutedEventArgs e)
        {
            Configs.RefreshModel();
        }
    }
}
