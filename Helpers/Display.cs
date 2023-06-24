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
        public class Display
        {
            IMyTextSurface display;
            StringBuilder stringBuilder = new StringBuilder();
            int paddingLeft = 6;

            public Display(MyGridProgram myGridProgram)
            {
                var providers = new List<IMyTextPanel>();
                myGridProgram.GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(providers, block => block.IsSameConstructAs(myGridProgram.Me));

                this.display = providers.Count > 0 ? providers[0] : getFirstCockpit(myGridProgram).GetSurface(0);

                display.ContentType = ContentType.TEXT_AND_IMAGE;
                display.TextPadding = 10.1f;
                display.FontSize = 0.8f;
            }

            public void log(string message)
            {
                stringBuilder.Append(new String(' ', (int) Math.Floor(Math.Max(0, 2.5 * paddingLeft--))) +  message);
            }

            public void flush()
            {
                display.WriteText(stringBuilder.ToString());
                paddingLeft = 6;
                stringBuilder.Clear();
            }
        }
    }
}
