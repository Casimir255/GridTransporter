using GridTransporter.Configs;
using GridTransporter.Networking;
using GridTransporter.Utilities;
using NLog;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using VRageMath;

namespace GridTransporter.BoundrySystem
{
    public class BoundaryTask
    {
        /*
         *  We will use this class to check each grids position and determine if they are inside a jump zone
         * 
         * 
         */


        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static Settings Configs { get { return Main.Config; } }

        private static Dictionary<long, int> TimerList = new Dictionary<long, int>();
        private static readonly Timer GridTimer = new Timer(1000);
        private static bool ScanReady = true;

        public BoundaryTask()
        {
            GridTimer.Elapsed += GridTimer_Elapsed;
            GridTimer.Start();
        }

        private void GridTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!MySession.Static.Ready || MySandboxGame.IsPaused)
                return;

            if (!ScanReady)
            {
                //Previous scan has yet to complete
                return;
            }


            ScanReady = false;
            ScanGrids();
            ScanReady = true;
        }

        private void ScanGrids()
        {
            foreach (var group in MyCubeGridGroups.Static.Physical.Groups.ToArray())
            {
                if (group.Nodes.Count == 0)
                    continue;



                MyCubeGrid BiggestGrid = GridUtilities.OraganizeGridCollection(group, out List<MyCubeGrid> GridList);
                if (BiggestGrid == null || !BiggestGrid.InScene || BiggestGrid.MarkedForClose || BiggestGrid.IsStatic)
                {
                    continue;
                }



                Vector3D Pos = BiggestGrid.PositionComp.GetPosition();
                if (!CheckRegion(Pos, out JumpRegion Region))
                {
                    RemoveEntityFromList(BiggestGrid.EntityId);
                    continue;
                }


                /* Check to make sure target server is online */
                if (!IsTargetServerOnline(Region, out int GamePort))
                {
                    string NonConnected = "Large gravitational disturbances have rendered the gate inoperable. Please exit the zone!";
                    DisplayMessage(GridList, NonConnected);
                    continue;
                }


                if (ReadyForJump(BiggestGrid.EntityId, Region))
                {
                    //Grid is ready to be jumped (timer has elapsed)


                    new GridTransport(GridList, Region, GamePort);

                   
                }
                else
                {
                    //Display Timer
                    DisplayCountdown(GridList, Region, TimerList[BiggestGrid.EntityId]);

                }
            }
        }

        public bool ReadyForJump(long GridID, JumpRegion Region)
        {

            if (TimerList.ContainsKey(GridID))
            {

                if (TimerList[GridID] > Region.Timer)
                {
                    //Hey, Grid is ready for jump
                    return true;
                }
                else
                {
                    TimerList[GridID] += 1;
                    return false;
                }

            }
            else
            {
                TimerList.Add(GridID, 0);
                return false;
            }
        }

        public void DisplayCountdown(IEnumerable<MyCubeGrid> Grids, JumpRegion Region,  int TimeLeft)
        {
            int Count = Region.Timer - TimeLeft;

            string Countdown = "Jumping to " + Region.Name + " in " + Count;
            DisplayMessage(Grids, Countdown);


        }

        private void DisplayMessage(IEnumerable<MyCubeGrid> Grids, string Message)
        {

            foreach (MyCubeGrid grid in Grids)
            {
                foreach (MySlimBlock item in grid.GetBlocks())
                {
                    MyCockpit c = item?.FatBlock as MyCockpit;
                    if (c?.Pilot != null)
                    {
                        //Log.Warn("Found pilot: " + c.Pilot.DisplayName);

                        MyCharacter Character = c.Pilot;
                        //Character.Close();
                        MyIdentity PilotIdentity = Character.GetIdentity();

                        if (PilotIdentity != null && !Character.IsBot && MySession.Static.Players.IsPlayerOnline(PilotIdentity.IdentityId))
                        {
                            MyMultiplayer.Static.SendChatMessage(Message, Sandbox.Game.Gui.ChatChannel.Private, PilotIdentity.IdentityId, "JumpGate");
                        }
                    }
                }
            }


        }

        public static void RemoveEntityFromList(long GridID)
        {
            if (TimerList.ContainsKey(GridID))
            {
                TimerList.Remove(GridID);
            }
        }


        public static bool CheckRegion(Vector3D ShipPosition, out JumpRegion jump)
        {
            for (int i = 0; i < Configs.JumpRegionGrid.Count(); i++)
            {
                Vector3D RegionCenter = new Vector3D(Configs.JumpRegionGrid[i].X, Configs.JumpRegionGrid[i].Y, Configs.JumpRegionGrid[i].Z);

                double Distance = Vector3D.Distance(ShipPosition, RegionCenter);

                //Multiplied by 1000
                if (Distance < Configs.JumpRegionGrid[i].Radius * 1000)
                {

                    jump = Configs.JumpRegionGrid[i];
                    return true;
                }
            }


            jump = null;
            return false;
        }

        public static bool IsTargetServerOnline(JumpRegion Target, out int GamePort)
        {
            GamePort = 0;
            if (Target == null || Target.Client == null)
                return false;

            GamePort = Target.Client.GetGamePort();
            return Target.Client.IsConnected();
        }

        public void Close()
        {
            GridTimer.Stop();
        }
    }
}
