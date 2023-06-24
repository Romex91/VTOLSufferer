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
        RollStabilizer rollStabilizer;
        Display display;
        public Program()
        {

            display = new Display(this);

            rollStabilizer = new RollStabilizer(GridTerminalSystem.GetBlockWithName("Miner Cockpit") as IMyCockpit, getGyros(this), display);

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            var frontThruster = GridTerminalSystem.GetBlockWithName("Forward Miner Thruster") as IMyThrust;
            var cockpit = GridTerminalSystem.GetBlockWithName("Miner Cockpit") as IMyCockpit;
            var connector = GridTerminalSystem.GetBlockWithName("Miner Connector") as IMyShipConnector;

            frontThruster.Enabled = cockpit.MoveIndicator.Z >= 0 && MyShipConnectorStatus.Connected != connector.Status;
            
            rollStabilizer.overrideControls();
            display.flush();
        }
    }
}
