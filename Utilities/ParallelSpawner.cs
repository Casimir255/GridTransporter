using NLog;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace GridTransporter.Utilities
{
    public class ParallelSpawner
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly int _maxCount;
        private readonly IEnumerable<MyObjectBuilder_CubeGrid> _grids;
        private readonly Action<HashSet<MyCubeGrid>> _callback;
        private readonly HashSet<MyCubeGrid> _spawned;
        private MyObjectBuilder_CubeGrid _BiggestGrid;
        private static int Timeout = 6000;



        //Bounds
        private BoundingSphereD SphereD;
        private MyOrientedBoundingBoxD BoxD;
        private BoundingBoxD BoxAAB = new BoundingBoxD();

        //Delta
        private Vector3D Delta3D; //This should be a vector from the grids center, to that of the CENTER of the grid
        private Vector3D TargetPos = Vector3D.Zero;


        public ParallelSpawner(IEnumerable<MyObjectBuilder_CubeGrid> grids, Action<HashSet<MyCubeGrid>> callback = null)
        {
            _grids = grids;
            _maxCount = grids.Count();
            _callback = callback;
            _spawned = new HashSet<MyCubeGrid>();

            //SaveGridToFile(@"C:\TestSave", "TestTransfer", _grids);
        }

        private static bool SaveGridToFile(string SavePath, string GridName, IEnumerable<MyObjectBuilder_CubeGrid> GridBuilders)
        {
            Directory.CreateDirectory(SavePath);
            MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), GridName);
            definition.CubeGrids = GridBuilders.ToArray();
            //PrepareGridForSave(definition);

            MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[] { definition };




            Log.Warn("Saving grid @" + Path.Combine(SavePath, GridName + ".sbc"));
            return MyObjectBuilderSerializer.SerializeXML(Path.Combine(SavePath, GridName + ".sbc"), false, builderDefinition);
        }


        public async Task<bool> Start(Vector3D Target, bool LoadInOriginalPosition = true)
        {


            TargetPos = Target;
            if (_grids.Count() == 0)
            {
                //Simple grid/objectbuilder null check. If there are no gridys then why continue?
                return true;
            }


            // Fix for recent keen update. (if grids have projected grids saved then they will get the infinite streaming bug)
            foreach (var cubeGrid in _grids)
            {
                cubeGrid.PlayerPresenceTier = MyUpdateTiersPlayerPresence.Normal;
                cubeGrid.CreatePhysics = true;



                // Set biggest grid in grid group
                if (_BiggestGrid == null || _BiggestGrid.CubeBlocks.Count < cubeGrid.CubeBlocks.Count)
                    _BiggestGrid = cubeGrid;

                foreach (var block in cubeGrid.CubeBlocks.OfType<MyObjectBuilder_Projector>())
                {
                    block.ProjectedGrid = null;
                    block.ProjectedGrids?.Clear();
                }
            }


            Log.Info($"Starting grid spawning for {_BiggestGrid.DisplayName} Target: {Target}");


            //Remap to fix entity conflicts
            MyEntities.RemapObjectBuilderCollection(_grids);


            //This should return more than a bool (we only need to run on game thread to find a safe spot)

            bool Spawned = await NetworkUtility.InvokeAsync<bool, bool>(CalculateSafePositionAndSpawn, LoadInOriginalPosition);

            if (Spawned)
            {
                foreach (var o in _grids)
                {

                    MyAPIGateway.Entities.CreateFromObjectBuilderParallel(o, false, Increment);
                }
                return true;
            }
            else
            {
                return false;
            }

        }



        private bool CalculateSafePositionAndSpawn(bool keepOriginalLocation)
        {
            try
            {

                //Calculate all required grid bounding objects
                FindGridBounds();



                //The center of the grid is not the actual center
                // Log.Info("SphereD Center: " + SphereD.Center);
                Delta3D = SphereD.Center - _BiggestGrid.PositionAndOrientation.Value.Position;
                //Log.Info("Delta: " + Delta3D);


                //This has to be ran on the main game thread!
                if (keepOriginalLocation)
                {
                    //If the original spot is clear, return true and spawn
                    if (OriginalSpotClear())
                    {
                        //if (Config.DigVoxels) DigVoxels();
                        return true;
                    }
                }


                //This is for aligning to gravity. If we are trying to find a safe spot, lets check gravity, and if we did recalculate, lets re-calc grid bounds
                if (CalculateGridPosition(TargetPos))
                {
                    //We only need to align to gravity if a new spawn position is required
                    EnableRequiredItemsOnLoad();

                    FindGridBounds();
                }


                //Find new spawn position either around character or last save (Target is specified on spawn call)
                var pos = FindPastePosition(TargetPos);
                if (!pos.HasValue)
                {
                    Log.Warn("No free spawning zone found! Stopping load!");
                    return false;
                }



                // Update grid position
                TargetPos = pos.Value;
                UpdateGridsPosition(TargetPos);


                return true;

            }
            catch (Exception Ex)
            {
                Log.Fatal(Ex);
                return false;
            }
        }

        private Vector3D? FindPastePosition(Vector3D Target)
        {
            //Log.info($"BoundingSphereD: {SphereD.Center}, {SphereD.Radius}");
            //Log.info($"MyOrientedBoundingBoxD: {BoxD.Center}, {BoxD.GetAABB()}");

            return MyEntities.FindFreePlaceCustom(Target, (float)SphereD.Radius, 90, 10, 1.5f, 10);
        }


        private void FindGridBounds()
        {
            BoxAAB = new BoundingBoxD();
            BoxAAB.Include(_BiggestGrid.CalculateBoundingBox());

            MatrixD BiggestGridMatrix = _BiggestGrid.PositionAndOrientation.Value.GetMatrix();
            MatrixD BiggestGridMatrixToLocal = MatrixD.Invert(BiggestGridMatrix);


            Vector3D[] corners = new Vector3D[8];
            foreach (var grid in _grids)
            {
                if (grid == _BiggestGrid)
                    continue;


                BoundingBoxD box = grid.CalculateBoundingBox();

                MyOrientedBoundingBoxD worldBox = new MyOrientedBoundingBoxD(box, grid.PositionAndOrientation.Value.GetMatrix());
                worldBox.Transform(BiggestGridMatrixToLocal);
                worldBox.GetCorners(corners, 0);

                foreach (var corner in corners)
                {
                    BoxAAB.Include(corner);
                }
            }

            BoundingSphereD Sphere = BoundingSphereD.CreateFromBoundingBox(BoxAAB);
            BoxD = new MyOrientedBoundingBoxD(BoxAAB, BiggestGridMatrix);
            SphereD = new BoundingSphereD(BoxD.Center, Sphere.Radius);



            //Test bounds to make sure they are in the right spot

            /*

            long ID = MySession.Static.Players.TryGetIdentityId(76561198045096439);
            Vector3D[] array = new Vector3D[8];
            BoxD.GetCorners(array, 0);

            for (int i = 0; i <= 7; i++)
            {
                CharacterUtilities.SendGps(array[i], i.ToString(), ID, 10);
            }
            */

            //Log.Info($"HangarDebug: {BoxD.ToString()}");

        }

        private bool OriginalSpotClear()
        {
            List<MyEntity> entities = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInOBB(ref BoxD, entities);

            bool SpotCleared = true;
            foreach (var entity in entities.OfType<MyCubeGrid>())
            {

                MyOrientedBoundingBoxD OBB = new MyOrientedBoundingBoxD(entity.PositionComp.LocalAABB, entity.WorldMatrix);

                ContainmentType Type = BoxD.Contains(ref OBB);

                //Log.Info($"{entity.DisplayName} Type: {Type.ToString()}");

                if (Type == ContainmentType.Contains || Type == ContainmentType.Intersects)
                {
                    SpotCleared = false;
                    //_Response.Respond("There are potentially other grids in the way. Attempting to spawn around the location to avoid collisions.");
                    break;
                }

            }

            return SpotCleared;
        }



        private void UpdateGridsPosition(Vector3D TargetPos)
        {
            //Log.Info("New Grid Position: " + TargetPos);

            //Translated point
            TargetPos -= Delta3D;


            //Now need to create a delta change from the initial position to the target position
            Vector3D Delta = TargetPos - _BiggestGrid.PositionAndOrientation.Value.Position;
            Parallel.ForEach(_grids, grid =>
            {
                Vector3D CurrentPos = grid.PositionAndOrientation.Value.Position;
                //MatrixD worldMatrix = MatrixD.CreateWorld(CurrentPos + Delta, grid.PositionAndOrientation.Value.Orientation.Forward, grid.PositionAndOrientation.Value.Orientation.Up,);
                grid.PositionAndOrientation = new MyPositionAndOrientation(CurrentPos + Delta, grid.PositionAndOrientation.Value.Orientation.Forward, grid.PositionAndOrientation.Value.Orientation.Up);

            });
        }

        public void Increment(IMyEntity entity)
        {
            var grid = (MyCubeGrid)entity;
            _spawned.Add(grid);

            if (_spawned.Count < _maxCount)
                return;

            try
            {

                foreach (MyCubeGrid g in _spawned)
                {
                    MyAPIGateway.Entities.AddEntity(g, true);
                }

            }
            catch (Exception ex)
            {
                Log.Fatal("Grid loading crashed!", ex);
                return;
            }

            Log.Info($"Spawning Completed for {_BiggestGrid.DisplayName} @ {grid.PositionComp.GetPosition()}");
            _callback?.Invoke(_spawned);

        }





        /*  Align to gravity code.
         * 
         * 
         */

        private void EnableRequiredItemsOnLoad()
        {
            //This really doesnt need to be ran on the game thread since we are still altering the grid before spawn

            Parallel.ForEach(_grids, grid =>
            {

                grid.LinearVelocity = new SerializableVector3();
                grid.AngularVelocity = new SerializableVector3();

                int counter = 0;
                foreach (MyObjectBuilder_Thrust Block in grid.CubeBlocks.OfType<MyObjectBuilder_Thrust>())
                {
                    counter++;
                    Block.Enabled = true;
                }

                foreach (MyObjectBuilder_Reactor Block in grid.CubeBlocks.OfType<MyObjectBuilder_Reactor>())
                {
                    Block.Enabled = true;
                }

                foreach (MyObjectBuilder_BatteryBlock Block in grid.CubeBlocks.OfType<MyObjectBuilder_BatteryBlock>())
                {
                    Block.Enabled = true;
                    Block.SemiautoEnabled = true;
                    Block.ProducerEnabled = true;
                    Block.ChargeMode = 0;
                }

                grid.DampenersEnabled = true;
            });

        }

        private bool CalculateGridPosition(Vector3D Target)
        {
            Vector3D forwardVector = Vector3D.Zero;


            //Hangar.Debug("Total Grids to be pasted: " + _grids.Count());

            //Attempt to get gravity/Artificial gravity to align the grids to


            //Here you can adjust the offset from the surface and rotation.
            //Unfortunatley we move the grid again after this to find a free space around the character. Perhaps later i can incorporate that into
            //LordTylus's existing grid checkplament method
            float gravityRotation = 0f;

            Vector3 gravityDirectionalVector = MyGravityProviderSystem.CalculateNaturalGravityInPoint(Target);

            bool AllowAlignToNatrualGravity = false;
            if (AllowAlignToNatrualGravity && gravityDirectionalVector == Vector3.Zero)
            {
                gravityDirectionalVector = MyGravityProviderSystem.CalculateArtificialGravityInPoint(Target);
            }


            if (gravityDirectionalVector == Vector3.Zero)
                return false;


            //Calculate and apply grid rotation
            Vector3D upDirectionalVector;
            if (gravityDirectionalVector != Vector3.Zero)
            {

                gravityDirectionalVector.Normalize();
                upDirectionalVector = -gravityDirectionalVector;

                if (forwardVector == Vector3D.Zero)
                {
                    forwardVector = Vector3D.CalculatePerpendicularVector(gravityDirectionalVector);
                    if (gravityRotation != 0f)
                    {
                        MatrixD matrixa = MatrixD.CreateFromAxisAngle(upDirectionalVector, gravityRotation);
                        forwardVector = Vector3D.Transform(forwardVector, matrixa);
                    }
                }
            }
            else if (forwardVector == Vector3D.Zero)
            {
                forwardVector = Vector3D.Right;
                upDirectionalVector = Vector3D.Up;
            }
            else
            {
                upDirectionalVector = Vector3D.CalculatePerpendicularVector(-forwardVector);
            }

            BeginAlignToGravity(Target, forwardVector, upDirectionalVector);
            return true;

        }

        private void BeginAlignToGravity(Vector3D Target, Vector3D forwardVector, Vector3D upVector)
        {
            //Create WorldMatrix
            MatrixD worldMatrix = MatrixD.CreateWorld(Target, forwardVector, upVector);

            int num = 0;
            MatrixD referenceMatrix = MatrixD.Identity;
            MatrixD rotationMatrix = MatrixD.Identity;

            //Find biggest grid and get their postion matrix
            Parallel.ForEach(_grids, grid =>
            {
                //Option to clone the BP
                //array[i] = (MyObjectBuilder_CubeGrid)TotalGrids[i].Clone();
                if (grid.CubeBlocks.Count > num)
                {
                    num = grid.CubeBlocks.Count;
                    referenceMatrix = grid.PositionAndOrientation.Value.GetMatrix();
                    rotationMatrix = FindRotationMatrix(grid);
                }
            });

            //Huh? (Keen does this so i guess i will too) My guess so it can create large entities
            MyEntities.IgnoreMemoryLimits = true;

            //Update each grid in the array
            Parallel.ForEach(_grids, grid =>
            {
                if (grid.PositionAndOrientation.HasValue)
                {
                    MatrixD matrix3 = grid.PositionAndOrientation.Value.GetMatrix() * MatrixD.Invert(referenceMatrix) * rotationMatrix;
                    grid.PositionAndOrientation = new MyPositionAndOrientation(matrix3 * worldMatrix);
                }
                else
                {
                    grid.PositionAndOrientation = new MyPositionAndOrientation(worldMatrix);
                }
            });
        }

        public static MatrixD FindRotationMatrix(MyObjectBuilder_CubeGrid cubeGrid)
        {


            var resultMatrix = MatrixD.Identity;
            var cockpits = cubeGrid.CubeBlocks.OfType<MyObjectBuilder_Cockpit>()
                .Where(blk =>
                {
                    return !(blk is MyObjectBuilder_CryoChamber)
                        && blk.SubtypeName.IndexOf("bathroom", StringComparison.InvariantCultureIgnoreCase) == -1;
                })
                .ToList();

            MyObjectBuilder_CubeBlock referenceBlock = cockpits.Find(blk => blk.IsMainCockpit) ?? cockpits.FirstOrDefault();




            if (referenceBlock == null)
            {
                var remoteControls = cubeGrid.CubeBlocks.OfType<MyObjectBuilder_RemoteControl>().ToList();
                referenceBlock = remoteControls.Find(blk => blk.IsMainCockpit) ?? remoteControls.FirstOrDefault();


            }

            if (referenceBlock == null)
            {
                referenceBlock = cubeGrid.CubeBlocks.OfType<MyObjectBuilder_LandingGear>().FirstOrDefault();
            }





            if (referenceBlock != null)
            {
                if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Right)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Forward, MathHelper.ToRadians(-90));
                else if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Left)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Forward, MathHelper.ToRadians(90));
                else if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Down)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Forward, MathHelper.ToRadians(180));
                else if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Forward)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Left, MathHelper.ToRadians(-90));
                else if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Backward)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Left, MathHelper.ToRadians(90));
            }



            return resultMatrix;
        }

    }
}
