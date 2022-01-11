using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Entity;
using VRage.Groups;

namespace GridTransporter.Utilities
{
    public class GridUtilities
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static MyCubeGrid OraganizeGridCollection(MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group GridGroup, out List<MyCubeGrid> AllGrids)
        {
            AllGrids = new List<MyCubeGrid>();
            MyCubeGrid BiggestGrid = null;
            int BlocksCount = 0;
            foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in GridGroup.Nodes)
            {

                MyCubeGrid grid = groupNodes.NodeData;

                if (grid.Physics == null)
                    continue;

                if (grid.BlocksCount > BlocksCount)
                {
                    BlocksCount = grid.BlocksCount;
                    BiggestGrid = grid;
                }

                AllGrids.Add(grid);
            }

            return BiggestGrid;
        }

        public static void CloseAllGrids(IEnumerable<MyCubeGrid> Grids, string reason = null)
        {
            StringBuilder Builder = new StringBuilder();

            Builder.Append("Closing the following grids: ");

            foreach (var grid in Grids)
            {
                Builder.Append(grid.DisplayName + ", ");
                grid.Close();
            }

            if (reason != null)
            {
                Builder.Append("Reason: " + reason);
            }

            Log.Info(Builder.ToString());
        }
    }

    public static class CharacterUtilities
    {

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();



        public static void ClearSavedCharacters(long IdentityID)
        {


            ClearCharacter(new List<long> { IdentityID });

        }

        public static bool ClearCharacter(List<long> Identities)
        {

            foreach (var Id in Identities)
            {
                try
                {
                    MyIdentity PlayerIdentity = MySession.Static.Players.TryGetIdentity(Id);
                    if (PlayerIdentity == null)
                        continue;

                    ClearSavedCharacters(PlayerIdentity);
                }
                catch (Exception ex)
                {
                    //Log.Error(ex);
                }

            }


            return true;
        }

        public static void ClearSavedCharacters(ulong SteamID)
        {

            MyIdentity IdentityID = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(SteamID));

            if (IdentityID == null)
                return;

            ClearSavedCharacters(IdentityID);

        }

        public static void ClearSavedCharacters(List<ulong> IdentityIDs)
        {
            foreach (ulong SteamID in IdentityIDs)
            {
                MyIdentity IdentityID = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(SteamID));

                if (IdentityID == null)
                    continue;

                ClearSavedCharacters(IdentityID);
                //Task<bool> P = Utility.InvokeAsync<List<long>, bool>(ClearCharacter, new List<long> { IdentityID });
                //P.Wait();
            }
        }



        private static void RemoveCharacter(MyCharacter Character)
        {
            if (Character.IsUsing is MyCryoChamber || Character.IsUsing is MyCockpit)
            {
                (Character.IsUsing as MyCockpit).RemovePilot();
            }

            Character.Close();
        }

        public static void ClearSavedCharacters(MyIdentity PlayerIdentity)
        {
            try
            {


                if (PlayerIdentity == null)
                    return;

                int RemovedAmount = 0;

                if (PlayerIdentity.Character != null)
                {
                    RemoveCharacter(PlayerIdentity.Character);
                    RemovedAmount++;
                }


                HashSet<long>.Enumerator enumerator = PlayerIdentity.SavedCharacters.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    MyCharacter entity;
                    if (MyEntities.TryGetEntityById(enumerator.Current, out entity, true) && (!entity.Closed || entity.MarkedForClose))
                    {
                        RemovedAmount++;
                        RemoveCharacter(entity);
                    }
                }

                enumerator.Dispose();

                Log.Info($"Cleared {RemovedAmount} characters for player {PlayerIdentity.DisplayName}");
                //PlayerIdentity.SetDead(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}
