using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        // Hound-class ships has double-merge block engine connection for better survival rate
        // (you need to destroy two merge blocks to cut off the engine from the ship)
        // The prise for this is a connection bug in Space Engeneer.
        // When having two pairs of active merge blocks the game cannot decide which merge block to connect.
        // This function solves the problem by disabling vertical directed merge blocks when horizontal blocks are disabled.
        class DoubleMergeBlocksConnectionConroller
        {
            struct MergeBlockPair {
                public MergeBlockPair(IMyShipMergeBlock horizontal, IMyShipMergeBlock vertical) : this()
                {
                    this.horizontal = horizontal;
                    this.vertical = vertical;
                }

                public IMyShipMergeBlock vertical;
                public IMyShipMergeBlock horizontal;
            }
            private List<MergeBlockPair> mergeBlockPairs = new List<MergeBlockPair>();

            private bool IsVertical(IMyTerminalBlock mergeBlock, MatrixD worldMatrix, MyGridProgram myGridProgram)
            {
                var mergeBlockMatrix = mergeBlock.WorldMatrix;
                var direction = new Vector3D(mergeBlockMatrix.M21, mergeBlockMatrix.M22, mergeBlockMatrix.M23);
                var directionLocal = Vector3D.TransformNormal(direction, MatrixD.Transpose(worldMatrix));
                myGridProgram.Echo(mergeBlock.CustomName + directionLocal.ToString("0.0"));
                return directionLocal.Z < -0.8;
            }

            public DoubleMergeBlocksConnectionConroller(MyGridProgram myGridProgram, Display display, IMyShipController controller)
            {
                var shipMergeBlocks=new List<IMyShipMergeBlock>();
                myGridProgram.GridTerminalSystem.GetBlocksOfType(shipMergeBlocks);
                shipMergeBlocks.RemoveAll((IMyShipMergeBlock block) =>
                {
                    return block.CustomName != "Ship Merge Block";
                });

                // Build pairs of vertical/horizontal merge blocks.
                var worldMatrix = controller.WorldMatrix;
                shipMergeBlocks.ForEach((IMyShipMergeBlock verticalMergeBlock) => {
                    
                    if (!IsVertical(verticalMergeBlock, worldMatrix, myGridProgram)) return;

                    // Find closest horizontal mergeBlock
                    int minDistance = int.MaxValue;
                    IMyShipMergeBlock closestsHorizontalMergeBlock = null;
                    shipMergeBlocks.ForEach((IMyShipMergeBlock horizontalMergeBlock) =>
                    {
                        if (IsVertical(horizontalMergeBlock, worldMatrix, myGridProgram)) return;
                        
                        int distance = horizontalMergeBlock.Position.RectangularDistance(verticalMergeBlock.Position);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestsHorizontalMergeBlock = horizontalMergeBlock;
                        }
                    });

                    mergeBlockPairs.Add(new MergeBlockPair(closestsHorizontalMergeBlock, verticalMergeBlock));
                });
            }

            public void Control()
            {
                mergeBlockPairs.ForEach((MergeBlockPair mergeBlockPair) =>
                {
                    mergeBlockPair.vertical.Enabled = mergeBlockPair.vertical.IsConnected || mergeBlockPair.horizontal.IsConnected;
                });
            }
        }
    }
}
