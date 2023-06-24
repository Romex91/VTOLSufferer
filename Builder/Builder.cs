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
        Dictionary<string, int> desiredAmountMap = new Dictionary<string, int>()
            {
                { "BulletproofGlass", 100 },
                { "Canvas", 10 },
                { "Computer", 200 },
                { "Construction", 400 },
                { "Detector", 10 },
                { "Display", 200 },
                { "Explosives", 0 },
                { "Girder", 200 },
                { "GravityGenerator", 0 },
                { "InteriorPlate", 400 },
                { "LargeTube", 20 },
                { "MetalGrid", 20 },
                { "Medical", 0 },
                { "Motor", 400 },
                { "PowerCell", 100 },
                { "RadioCommunication", 10 },
                { "Reactor", 0 },
                { "SmallTube", 200 } ,
                { "SolarCell", 0 },
                { "SteelPlate", 1000 },
                { "Superconductor", 0 },
                { "Thrust", 0 },
                { "ZoneChip", 0 },
            };

        RollStabilizer rollStabilizer;
        IMyTextSurface display;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            List<IMyGyro> gyros = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros, block => block.IsSameConstructAs(Me));

            var displays = GridTerminalSystem.GetBlockWithName("Builder Cockpit") as IMyTextSurfaceProvider;
            this.display = displays.GetSurface(0);
            display.ContentType = ContentType.TEXT_AND_IMAGE;
            display.TextPadding = 0.1f;
            display.FontSize = 0.8f;

            rollStabilizer = new RollStabilizer(GridTerminalSystem.GetBlockWithName("Builder Cockpit") as IMyCockpit, gyros, (Display)display);
        }

        public void Save()
        {

        }

        public int GetTotalItemsCount(MyItemType type)
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);

            var totalCount = 0;
            foreach (var block in blocks)
            {
                for (var i = 0; i < block.InventoryCount; i++)
                {
                    var inventory = block.GetInventory(i);
                    totalCount += inventory.GetItemAmount(type).ToIntSafe();
                }
            }

            return totalCount;
        }
        
        private static string SerializeMatrix(MatrixD matrix)
        {
            return $"{matrix.M11.ToString("0.00")} {matrix.M12.ToString("0.00")} {matrix.M13.ToString("0.00")}\n"
                 + $"{matrix.M21.ToString("0.00")} {matrix.M22.ToString("0.00")} {matrix.M23.ToString("0.00")}\n"
                 + $"{matrix.M31.ToString("0.00")} {matrix.M32.ToString("0.00")} {matrix.M33.ToString("0.00")}\n";
        }

        public void Main(string argument, UpdateType updateSource)
        {

            var assemblers = new List<IMyAssembler>();

            var stringBuilder = new StringBuilder(1024);
            GridTerminalSystem.GetBlocksOfType(assemblers);
            foreach (var assembler in assemblers)
            {
                stringBuilder.Append(assembler.CustomName + " " + (assembler.IsProducing ? "PRODUCING" : "IDLE"));
                stringBuilder.Append("\n");
            }

            foreach (var item in desiredAmountMap)
            {
                int totalCount = GetTotalItemsCount(new MyItemType("MyObjectBuilder_Component", item.Key));
                if (item.Value > totalCount)
                {
                    stringBuilder.Append(item.Key + " " + totalCount + "/" + item.Value + "\n");
                    foreach (var assembler in assemblers)
                    {
                        if (assembler.IsQueueEmpty)
                        {
                            try
                            {
                                assembler.AddQueueItem(MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/" + item.Key), (decimal)10);
                            }
                            catch
                            {
                                try
                                {
                                    assembler.AddQueueItem(MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/" + item.Key + "Component"), (decimal)10);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
            }


            display.WriteText(stringBuilder);

            rollStabilizer.overrideControls();
        }
    }
}
