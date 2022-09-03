using GridTransporter.Configs;
using GridTransporter.Networking;
using GridTransporter.Utilities;
using NLog;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components;
using VRageMath;

namespace GridTransporter.BoundrySystem
{

    [ProtoContract]
    public class GridTransport
    {
        //Laziest piece of shit code ive ever written

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private List<MyObjectBuilder_CubeGrid> GridObjects = new List<MyObjectBuilder_CubeGrid>();

        private static MethodInfo OnSetPlayerDeadSuccess = typeof(MyPlayerCollection).GetMethod("OnSetPlayerDeadSuccess", BindingFlags.Static | BindingFlags.NonPublic);


        [ProtoMember(7)]
        private List<GridItem> Grids = new List<GridItem>();
        [ProtoMember(8)]
        private List<PlayerItem> Players = new List<PlayerItem>();

        [ProtoMember(10)]
        private Vector3D SpawnPosition;
        [ProtoMember(11)]
        private long BigOwner;
        [ProtoMember(12)]
        private bool IsNPCGrid;
        [ProtoMember(13)]
        private Vector3 Velocity;
        [ProtoMember(14)]
        private Vector3 AnglularVelocity;


        [ProtoMember(15)]
        private string GridName;


        private JumpRegion Target;

        private readonly string PlayerDirectIP;


        public GridTransport(List<MyCubeGrid> gridGroups, JumpRegion Target, int GamePort)
        {
            SpawnPosition = new Vector3D(Target.X);
            PlayerDirectIP = Target.Client.ServerIP + ":" + GamePort;
            this.Target = Target;
            PrepareGrids(gridGroups);
        }

        public GridTransport() { }


        public async Task SpawnAsync()
        {
            /*  Starts grid spawning task. This is async as we will be waiting for the game to confirm the remaps.
             *  We first need to deserialize and prep the grid.
             *  
             */

            if (!Deserialize())
                return;

            //Fixes any issues with grids spawning and being connected with landing gears. (Figherts in hangar)
            RelockAllGear();



            //Validates player identities. If we have new players that have never been to this recieving player we need to create their identity and apply it on the grid before spawn.
            //If this player has been to this server, we need to grab their exsisting ID and replace it with the one they previously held in the sending server.
            //We will need to await for this to be completed before we start transferring owner ship of blocks
            await NetworkUtility.InvokeAsync(() => ValidateCharacters());

            TransferNewOwnership();

            /* Remap Pilots to their new found identities */

            foreach (var p in Players)
                Log.Warn($"PlayerInTransfer: {p.PlayerName} : {p.IdentityID}");


            foreach (var grid in GridObjects)
            {
                foreach (var block in grid.CubeBlocks.OfType<MyObjectBuilder_Cockpit>())
                {
                    if (block.Pilot != null)
                    {

                        //OwningPlayerIdentityId: Should be the old identity from the previous server
                        PlayerItem owner = Players.FirstOrDefault(x => x.IdentityID == block.Pilot.OwningPlayerIdentityId);

                        //Log.Warn($"{block.Pilot.DisplayName}:{block.Pilot.OwningPlayerIdentityId} -> {owner.NewIdentity} - {block.ComponentContainer.Components.Count()} components");

                        MyObjectBuilder_HierarchyComponentBase hierarchy = (MyObjectBuilder_HierarchyComponentBase)block.ComponentContainer.Components.FirstOrDefault(x => x.Component is MyObjectBuilder_HierarchyComponentBase).Component;
                        MyObjectBuilder_Character character = (MyObjectBuilder_Character)hierarchy?.Children.First(x => x is MyObjectBuilder_Character);


                        if (character == null)
                        {
                            Log.Fatal("Unable to find character component base in cockpit components!");
                            return;
                        }

                        character.OwningPlayerIdentityId = owner.NewIdentity;

                        //Log.Info($"Succesfully updated character component in cockpit with new Identity!");


                        if (owner == null)
                        {
                            block.Pilot = null;
                            continue;
                        }

                        //Not sticking with new identity
                        block.Pilot.OwningPlayerIdentityId = owner.NewIdentity;
                        //Log.Warn($"{owner.NewIdentity} -> {block.Pilot.OwningPlayerIdentityId}");
                    }
                }

            }


            //Remaps entities so the numbers dont collide with anything previously in the game
            //MyEntities.RemapObjectBuilderCollection(GridObjects);


            //Possible grid backup if grid is owned by majority
            if (BigOwner != 0)
            {
                PluginDependencyManager.GridBackupInvoke(GridObjects, BigOwner);
            }



            ParallelSpawner Spawner = new ParallelSpawner(GridObjects, AfterGridSpawn);

            //Following warning is fine. This is a fire and forget
            Spawner.Start(SpawnPosition);
            return;
        }


        private bool ValidateCharacters()
        {
            List<PlayerItem> AllPlayers = Players;


            foreach (PlayerItem P in AllPlayers)
            {
                //Attempts to find an exsisting ID
                long FoundID = MySession.Static.Players.TryGetIdentityId(P.SteamID);
                long newIdentitiy;

                //If we have found an acceptable ID replace it with the found one
                if (FoundID != 0)
                {
                    var id = MySession.Static.Players.TryGetIdentity(FoundID);
                    Log.Warn($"Found an identity with the specifiedID {FoundID}! {id.DisplayName}");

                    newIdentitiy = FoundID;
                    CharacterUtilities.ClearSavedCharacters(FoundID);
                }
                else
                {
                    //If we dont have an ID on this server with this STEAMID then create one
                    MyIdentity ID = CreateIdentityForPlayer(P);
                    Log.Info($"{P.PlayerName} does not exsist on this server! Succesfully created {ID.IdentityId}!");
                    newIdentitiy = ID.IdentityId;
                }


                //Set new identity so we can adjust this later
                P.NewIdentity = newIdentitiy;

                //If this ID that we are replacing is the bigowner of the grid, we need to update that aswell so we can perform gridback for this player. If they arent... then they SOL
                if (BigOwner == P.IdentityID)
                {
                    BigOwner = newIdentitiy;
                }

                Log.Info($"Attempting to clear {P.PlayerName} saved characters in this server!");

            }

            return true;
        }

        private void TransferNewOwnership()
        {
            PlayerItem Item = Players.FirstOrDefault();

            if (Item == null)
                return;

            //Transfers all grids to first player in the transfer list
            foreach (var grid in GridObjects)
            {
                foreach (var block in grid.CubeBlocks)
                {
                    block.BuiltBy = Item.NewIdentity;
                    block.Owner = Item.NewIdentity;
                }
            }
        }

        private bool Deserialize()
        {
            try
            {

                Log.Info("Recieved Grids Count: " + Grids.Count);
                if (Grids.Count == 0)
                    return false;


                foreach (GridItem Item in Grids)
                {
                    MyObjectBuilder_CubeGrid Grid = EntityBase.DeserializeOB<MyObjectBuilder_CubeGrid>(Item.GridObject);
                    GridObjects.Add(Grid);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }

        private void AfterGridSpawn(HashSet<MyCubeGrid> SpawnedGrids)
        {
            //Event after the grid spawn
            SpawnCharactersInGrid(SpawnedGrids);
            ReApplyVeloctiy(SpawnedGrids);

        }


        private void ReApplyVeloctiy(HashSet<MyCubeGrid> SpawnedGrids)
        {
            foreach (var Grid in SpawnedGrids)
            {
                Grid.Physics.LinearVelocity = Velocity;
                Grid.Physics.AngularVelocity = AnglularVelocity;
            }
        }


        private void SpawnCharactersInGrid(HashSet<MyCubeGrid> SpawnedGrids)
        {
            //Log.Warn("Players: " + Players.Count);
            if (Players != null && Players.Count > 0)
            {
                //Attempt to get block where character was
                foreach (MyCubeGrid CubeGird in SpawnedGrids)
                {
                    foreach (var item in CubeGird.GetFatBlocks())
                    {

                        if (!(item is MyCockpit))
                            continue;


                        MyCockpit c = item as MyCockpit;


                        if (c == null || c.Pilot == null)
                            continue;


                        MyIdentity Identity = c.Pilot.GetIdentity();
                        if (Identity is null)
                        {
                            //This shouldnt occur, but we added this for null check
                            Log.Error($"{c.Pilot.EntityId} doesnt have an identity!");
                            continue;
                        }


                        Identity.Character?.Close();
                        Identity.SavedCharacters.Clear();


                        if (MySession.Static.Players.TryGetPlayerId(Identity.IdentityId, out MyPlayer.PlayerId ID2))
                        {
                            Log.Warn("Spawning " + Identity.DisplayName + " into their cockpit!");
                            //Re-spawn this new player identity back into their proper body
                            SpawnIntoCharacter(ID2, c.Pilot);
                        }
                    }
                }
            }


        }

        public void SpawnIntoCharacter(MyPlayer.PlayerId PlayerID, MyCharacter newCharacter)
        {
            //newCharacter.SetPlayer(player);
            //MyPlayerCollection.ChangePlayerCharacter(player, this, this);


            MyIdentity Identity = MySession.Static.Players.TryGetPlayerIdentity(PlayerID);
            if (Identity == null)
                return;



            //Set the identity new character
            Identity.ChangeCharacter(newCharacter);

            //Prevents trash collector from grabbing characters
            Identity.LastLogoutTime = DateTime.Now;
            Identity.LastLoginTime = DateTime.Now - TimeSpan.FromSeconds(5);

            //Sync.Players.SetControlledEntityInternal(player.Id, entity, false);
            //Shouldnt have to do above since player isnt online


            //SetPlayerDeadInternal
            Identity.SetDead(false);

            //Event lets players know the player has been spawned/revived
            Events.RaiseStaticEvent<ulong, int, bool, bool>(OnSetPlayerDeadSuccess, PlayerID.SteamId, PlayerID.SerialId, false, false);
            //Log.Error($"Reviving player {Identity.DisplayName}!");

        }

        private void RelockAllGear()
        {
            //Ye ye you get the jist
            foreach (var grid in GridObjects)
            {
                foreach (var block in grid.CubeBlocks.OfType<MyObjectBuilder_LandingGear>())
                {
                    block.AutoLock = true;

                    block.IsLocked = false;
                    block.MasterToSlave = null;
                    block.GearPivotPosition = null;
                    block.OtherPivot = null;
                    block.AttachedEntityId = null;
                    block.LockMode = SpaceEngineers.Game.ModAPI.Ingame.LandingGearMode.Unlocked;

                }
            }
        }

        private void PrepareGrids(List<MyCubeGrid> Grids)
        {
            //Preps the grid for serializing/transferring

            try
            {
                string BiggestGridName = "";
                int blockcount = Grids[0].BlocksCount;
                MyCubeGrid BiggestGrid = Grids[0];

                foreach (var Grid in Grids)
                {
                    if (Grid.BigOwners.Count > 0 && Grid.BlocksCount >= blockcount)
                    {
                        BiggestGrid = Grid;
                        blockcount = Grid.BlocksCount;
                        BigOwner = Grid.BigOwners[0];
                        BiggestGridName = Grid.DisplayName;
                    }
                }

                Velocity = BiggestGrid.Physics.LinearVelocity;
                AnglularVelocity = BiggestGrid.Physics.AngularVelocity;

                Task<GridResult> GameThread = NetworkUtility.InvokeAsync<List<MyCubeGrid>, GridResult>(GetGridBuilderAndClose, Grids);
                GameThread.Wait();

                GridResult R = GameThread.Result;
                GridObjects = R.CubeGridBuilders;
                Players = R.PlayersInSeats;


                if (BigOwner != 0)
                {
                    PluginDependencyManager.GridBackupInvoke(GridObjects, BigOwner);
                }

                //Log.Info("Total Players Around Ship: " + PlayersAroundShip.Count);
                PrepMessageForTransport();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }



        private GridResult GetGridBuilderAndClose(List<MyCubeGrid> Grids)
        {
            GridResult MethodResult = new GridResult();
            foreach (MyCubeGrid grid in Grids)
            {
                /* What else should it be? LOL? */
                foreach (MySlimBlock item in grid.GetBlocks())
                {
                    MyCockpit c = item?.FatBlock as MyCockpit;
                    if (c?.Pilot != null)
                    {
                        //Log.Warn("Found pilot: " + c.Pilot.DisplayName);

                        MyCharacter Character = c.Pilot;
                        MyIdentity PilotIdentity = Character.GetIdentity();

                        //Log.Warn(PilotIdentity.DisplayName + " has the following characters saved in this world:");
                        //Log.Warn("Removing saved character: " + Character.EntityId);
                        PilotIdentity.SavedCharacters.Remove(Character.EntityId);

                        PlayerItem Item = new PlayerItem(Character.DisplayName, Character.ControlSteamId, Character.GetPlayerIdentityId(), null, Vector3D.Zero);

                        //Try and get playertoolbar
                        if (Character.GetPlayerId(out MyPlayer.PlayerId ID))
                        {
                            var toolbar = MySession.Static.Toolbars.TryGetPlayerToolbar(ID);
                            if (toolbar != null)
                            {
                                Item.SetToolbar(toolbar.GetObjectBuilder());
                            }
                        }




                        //Character.Close();
                        //Log.Warn("Adding player: " + Item.PlayerName + " to playerlist!");

                        if (!MethodResult.PlayersInSeats.Contains(Item))
                            MethodResult.PlayersInSeats.Add(Item);
                    }
                }



                //Hmm? Maybe remove characters
                if (!(grid.GetObjectBuilder() is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilder.AngularVelocity = Vector3.Zero;
                objectBuilder.LinearVelocity = Vector3.Zero;

                MethodResult.CubeGridBuilders.Add(objectBuilder);
            }



            GridUtilities.CloseAllGrids(Grids, "Grid has been sent to different server!");
            return MethodResult;

        }





        private void PrepMessageForTransport()
        {
            foreach (MyObjectBuilder_CubeGrid obj in GridObjects)
            {
                GridItem Item = new GridItem(obj);
                Grids.Add(Item);
            }




            SendPlayers();
            Networking.Networking.PublishMessage(MessageType.GridTransport, this, Target.Client);


        }

        private void SendPlayers()
        {
            Log.Info("Sending all players to: " + PlayerDirectIP);
            foreach (var player in Players)
            {
                ModCommunication.SendMessageTo(new JoinServerMessage(PlayerDirectIP), player.SteamID);
            }
        }



        public class GridResult
        {
            public List<MyObjectBuilder_CubeGrid> CubeGridBuilders = new List<MyObjectBuilder_CubeGrid>();
            public List<PlayerItem> PlayersInSeats = new List<PlayerItem>();
        }

        /* Identity Creation */

        private MyIdentity CreateIdentityForPlayer(PlayerItem P)
        {

            MyIdentity NewPlayerIdentity = MySession.Static.Players.CreateNewIdentity(P.PlayerName);

            if (!Sync.Clients.TryGetClient(P.SteamID, out MyNetworkClient Client))
            {
                Client = Sync.Clients.AddClient(P.SteamID, P.PlayerName);
            }

            if (Client == null)
            {
                Log.Error($"Couldnt create client for {P.SteamID}!");
                return null;
            }

            MyPlayer myPlayer = Sync.Players.CreateNewPlayer(NewPlayerIdentity, Client, P.PlayerName, true);

            //Remove client after we added it
            Sync.Clients.RemoveClient(P.SteamID);
            return NewPlayerIdentity;
        }

        private void SetToolBar(MyPlayer myPlayer, PlayerItem P)
        {
            MyToolbar myToolbar = new MyToolbar(MyToolbarType.Character);
            if (P.PlayerToolBar != null)
            {
                myToolbar.Init(P.PlayerToolBar, myPlayer.Character, true);
            }

            MySession.Static.Toolbars.RemovePlayerToolbar(myPlayer.Id);
            MySession.Static.Toolbars.AddPlayerToolbar(myPlayer.Id, myToolbar);
        }

    }

    [ProtoContract]
    public struct GridItem
    {
        [ProtoMember(1)]
        public string GridName;

        [ProtoMember(2)]
        public byte[] GridObject;

        public GridItem(MyObjectBuilder_CubeGrid Grid)
        {
            GridName = Grid.DisplayName;
            GridObject = EntityBase.SerializeOB(Grid);
        }
    }

    [ProtoContract]
    public class PlayerItem
    {
        [ProtoMember(1)]
        public readonly string PlayerName;

        [ProtoMember(2)]
        public readonly ulong SteamID;

        [ProtoMember(3)]
        public readonly long IdentityID;


        public long NewIdentity;

        [ProtoMember(4)]
        public long CharacterEntityID;

        [ProtoMember(5)]
        public readonly MyObjectBuilder_Character CharacterBuilder;



        [ProtoMember(10)]
        public readonly Vector3D Position;

        [ProtoMember(11)]
        public MyObjectBuilder_Toolbar PlayerToolBar;


        public PlayerItem(string Name, ulong PlayerSteamID, long PlayerIdentityID, MyObjectBuilder_Character Character, Vector3D CharacterPos, long CharacterEntity = 0)
        {
            PlayerName = Name;
            SteamID = PlayerSteamID;
            IdentityID = PlayerIdentityID;
            CharacterBuilder = Character;
            Position = CharacterPos;
            CharacterEntityID = CharacterEntity;
            PlayerToolBar = null;

            NewIdentity = 0;
        }

        public PlayerItem() { }

        public void SetToolbar(MyObjectBuilder_Toolbar Toolbar)
        {
            PlayerToolBar = Toolbar;
        }


    }
}
