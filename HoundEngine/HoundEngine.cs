using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        Display display;

        bool waitingForAttachment = false;
        int ticksToWaitForAttachment = 0;
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            display = new Display(this);
        }

        bool AreHingesAttached(List<IMyTerminalBlock> hinges)
        {

            return hinges.Any((IMyTerminalBlock block) =>
            {
                var hinge = block as IMyMotorAdvancedStator;
                return hinge.IsAttached;
            });
        }

        bool IsBuildFinished()
        {
            IMyProjector connectorProjector = GridTerminalSystem.GetBlockWithName("Engine Connector Projector") as IMyProjector; 
            IMyProjector engineProjector = GridTerminalSystem.GetBlockWithName("Engine Projector") as IMyProjector;
            if (connectorProjector == null || engineProjector == null)
                return false;

            display.log("Blocks To Build:" + engineProjector.RemainingBlocks.ToString() + "/" + engineProjector.TotalBlocks.ToString() + "\n");

            return connectorProjector.RemainingBlocks == 0 && engineProjector.RemainingBlocks == 0;
        }

        void AttachHinges(List<IMyTerminalBlock> hinges)
        {
            hinges.ForEach((IMyTerminalBlock block) =>
            {
                var hinge = block as IMyMotorAdvancedStator;
                if (!hinge.IsAttached)
                {
                    hinge.Detach();
                    hinge.Attach();
                }
            });
        }

        void AdjustHingesParms(List<IMyTerminalBlock> hinges)
        {
            hinges.ForEach((IMyTerminalBlock block) =>
            {
                var hinge = block as IMyMotorAdvancedStator;
                if (!hinge.IsAttached)
                    hinge.Torque = 0;
                else
                    hinge.Torque = 10000000;
            });
        }

        void DetachHinges(List<IMyTerminalBlock> hinges)
        {
            hinges.ForEach((IMyTerminalBlock block) =>
            {
                var hinge = block as IMyMotorAdvancedStator;
                hinge.Detach();
            });
        }

        void CheckConnector(List<IMyTerminalBlock> hinges, List<IMyTerminalBlock> engineMergeBlocks) {

            // display.log("Hinges To Connector Distance:" + Vector3D.Distance(engineMergeBlocks[0].GetPosition(), hinges[0].GetPosition()) + "\n");
            // display.log("Hinges To Connector Distance:" + Vector3D.Distance(engineMergeBlocks[1].GetPosition(), hinges[1].GetPosition()) + "\n");
            if (engineMergeBlocks.Count < 2
                || hinges.Count < 2
                || Vector3D.Distance(engineMergeBlocks[0].GetPosition(), hinges[0].GetPosition()) > 1.5 
                || Vector3D.Distance(engineMergeBlocks[1].GetPosition(), hinges[1].GetPosition()) > 1.5)
            {
                display.log("Connector is damaged! Detaching!");
                DetachHinges(hinges);
            }
        }
        bool AreHingesNormalized(List<IMyTerminalBlock> hinges)
        {
            return hinges.All((IMyTerminalBlock block) => {
                var hinge = block as IMyMotorAdvancedStator;
                return Math.Abs(hinge.Angle) < 0.2;
            });
        }
        void NormalizeHinges(List<IMyTerminalBlock> hinges)
        {
            if (hinges.Count!= 2)
            {
                return;
            }
            var hinge1 = hinges[0] as IMyMotorAdvancedStator;
            var hinge2 = hinges[1] as IMyMotorAdvancedStator;

            if (hinge1.IsAttached)
            {
                hinge1.TargetVelocityRad = -hinge1.Angle;
                hinge2.TargetVelocityRad = -hinge1.Angle;
            } else
            {
                hinge1.TargetVelocityRad = 0;
                hinge2.TargetVelocityRad = 0;
            }
        }

        void RemoveDistantBlocks(List<IMyTerminalBlock> blocks, int rectangularDistance)
        {
            blocks.RemoveAll((IMyTerminalBlock block) => {
                var distance = Vector3D.Distance(Me.GetPosition(), block.GetPosition());
                display.log(block.CustomName + " " + distance.ToString("0.0") + "\n");
                return distance > rectangularDistance;
            });
        }

        void DisableThrusters()
        {
            List<IMyThrust> myThrusts = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(myThrusts);

            myThrusts.ForEach((IMyThrust thruster) => { thruster.Enabled = false; });
        }

        public void MainImpl(string argument, UpdateType updateSource)
        {
            IMyShipMergeBlock connectorMergeBlock = GridTerminalSystem.GetBlockWithName("Connectors Merge Block") as IMyShipMergeBlock;
            IMyProjector connectorProjector = GridTerminalSystem.GetBlockWithName("Engine Connector Projector") as IMyProjector;
            List<IMyTerminalBlock> hinges = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName("Engine Hinge", hinges);
            RemoveDistantBlocks(hinges, 8);
            AdjustHingesParms(hinges);

            DisableThrusters();
            if (connectorProjector == null) return;

            if (waitingForAttachment)
            {
                display.log("Attaching Engine Connector! " + ticksToWaitForAttachment.ToString());
                ticksToWaitForAttachment--;
                if (ticksToWaitForAttachment < 0 || AreHingesAttached(hinges))
                {
                    waitingForAttachment = false;
                }

                AttachHinges(hinges);
                return;
            }

            List<IMyTerminalBlock> engineMergeBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName("Engine-to-Ship Merge Block", engineMergeBlocks);
            RemoveDistantBlocks(engineMergeBlocks, 8);

            IMyShipMergeBlock carMergeBlock = GridTerminalSystem.GetBlockWithName("Car-to-Engine Merge Block") as IMyShipMergeBlock;

            if (carMergeBlock != null)
            {
                if (argument == "DetachVehicle"
                    && engineMergeBlocks.Count > 0 
                    && engineMergeBlocks.Any((IMyTerminalBlock block) => (block as IMyShipMergeBlock).IsConnected))
                {
                    display.log("Detaching Vehicle!\n");
                    carMergeBlock.Enabled = false;
                }
                else
                {
                    carMergeBlock.Enabled = true;
                }
                
                engineMergeBlocks.ForEach((IMyTerminalBlock block) => {
                    var mergeBlock = (block as IMyShipMergeBlock);
                    if (argument == "DetachEngine" && carMergeBlock.IsConnected)
                    {
                        display.log("Detaching Egnines!\n");
                        mergeBlock.Enabled = false;
                    } else
                    {
                        mergeBlock.Enabled = true;
                    }
                });
            }
            NormalizeHinges(hinges);
            if (AreHingesAttached(hinges))
            {
                display.log("Hinges are attached. Ready to connect engine!\n");
 
                connectorProjector.Enabled = false;

                if (AreHingesNormalized(hinges))
                {
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    CheckConnector(hinges, engineMergeBlocks);
                } else
                {
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                }
                return;
            }

            connectorProjector.Enabled = true;

            if (IsBuildFinished())
            {
                display.log("Build is finished. Connecting Hinges");
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                connectorMergeBlock.Enabled = false;

                waitingForAttachment = true;
                ticksToWaitForAttachment = 100;
                return;
            }
            display.log("No engines attached.");

            connectorMergeBlock.Enabled = true;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            MainImpl(argument, updateSource);
            display.flush();
        }
    }
}
