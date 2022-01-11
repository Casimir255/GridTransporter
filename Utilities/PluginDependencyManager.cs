using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Torch.API.Plugins;
using Torch.Managers;
using VRage.Game;

namespace GridTransporter.Utilities
{
    public class PluginDependencyManager
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static readonly Guid GridBackupGUID = new Guid("75e99032-f0eb-4c0d-8710-999808ed970c");
        private static ITorchPlugin GridBackupPlugin;
        private static MethodInfo GridBackupInvoker;

        public static void InitPluginDependencyManager(PluginManager Plugins)
        {

            GetGridBackupPlugin(Plugins);
            //GetNexusAPIPlugin(Plugins);
        }

        private static void GetGridBackupPlugin(PluginManager Plugins)
        {
            if (GetPluginInstance(Plugins, GridBackupGUID, out ITorchPlugin Plugin))
            {
                GridBackupPlugin = Plugin;
                GridBackupInvoker = Plugin.GetType().GetMethod("BackupGridsManuallyWithBuilders", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new Type[2] { typeof(List<MyObjectBuilder_CubeGrid>), typeof(long) }, null);
            }
        }

        public static void GridBackupInvoke(List<MyObjectBuilder_CubeGrid> GridObjectBuilders, long OnwerIdentity)
        {
            if (!(GridBackupPlugin is null) && !(GridBackupInvoker is null))
            {
                try
                {
                    Log.Info("Running GridBackup! Target Player: " + OnwerIdentity);
                    GridBackupInvoker.Invoke(GridBackupPlugin, new object[] { GridObjectBuilders, OnwerIdentity });
                }
                catch (Exception e)
                {
                    Log.Fatal(e, "GridBackup Error! ");
                }
            }
            else
            {
                Log.Warn("Skipping GridBackup! (Plugin isnt installed). It is highly encouraged to use Gridbackup Plugin!");
            }
        }


        private static bool GetPluginInstance(PluginManager Plugins, Guid PluginGUID, out ITorchPlugin InstalledPlugin)
        {
            InstalledPlugin = null;
            if (Plugins.Plugins.TryGetValue(PluginGUID, out InstalledPlugin))
            {
                Log.Warn("Plugin: " + InstalledPlugin.Name + " " + InstalledPlugin.Version + " is installed!");
                return true;
            }
            return false;
        }

    }
}
