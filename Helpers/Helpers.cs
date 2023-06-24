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
    partial class Program
    {
        static List<IMyGyro> getGyros(MyGridProgram myGridProgram)
        {
            List<IMyGyro> gyros = new List<IMyGyro>();
            myGridProgram.GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros, block => block.IsSameConstructAs(myGridProgram.Me));
            return gyros;
        }

        static List<IMyThrust> getThrusters(MyGridProgram myGridProgram)
        {
            List<IMyThrust> thrusters = new List<IMyThrust>();
            myGridProgram.GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters, block => block.IsSameConstructAs(myGridProgram.Me));
            return thrusters;
        }
        static IMyCockpit getFirstCockpit(MyGridProgram myGridProgram)
        {
            var cockpits = new List<IMyCockpit>();
            myGridProgram.GridTerminalSystem.GetBlocksOfType(cockpits, block => block.IsSameConstructAs(myGridProgram.Me));

            return cockpits[0];
        }
        
    }
}
