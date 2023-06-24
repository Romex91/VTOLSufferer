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
        IMyTextSurface display;
        IMyCockpit cockpit;
        List<IMyPistonBase> pistons = new List<IMyPistonBase>();

        const double mouseSensitivity = 0.001f;
        const double keyboardSensitivity = 0.02f;
        const double relaxation = 0.01f;
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            var cockpits = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(cockpits, block => block.IsSameConstructAs(Me));

            cockpit = cockpits[0];

            this.display = cockpit.GetSurface(0);
            display.ContentType = ContentType.TEXT_AND_IMAGE;
            display.TextPadding = 0.1f;
            display.FontSize = 0.8f;

            GridTerminalSystem.GetBlocksOfType(pistons, block => block.IsSameConstructAs(Me));
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            var stringBuilder = new StringBuilder(1024);
            var worldMatrix = cockpit.WorldMatrix;
            var mouseInput = cockpit.RotationIndicator;
            var wasd = cockpit.MoveIndicator;

            foreach (var piston in pistons)
            {
                var pistonMatrix = piston.WorldMatrix;
                var direction = new Vector3D(pistonMatrix.M21, pistonMatrix.M22, pistonMatrix.M23);
                var directionLocal = Vector3D.TransformNormal(direction, MatrixD.Transpose(worldMatrix));

                if (directionLocal.X < -0.8)
                {
                    piston.Velocity += - mouseInput.Y * mouseSensitivity;
                } else if (directionLocal.Y > 0.8)
                {
                    piston.Velocity += - mouseInput.X * mouseSensitivity;
                } else
                {
                    stringBuilder.Append($"WASD: {wasd.Y.ToString("0.00")} {piston.Velocity.ToString("0.00")}\n");
                    piston.Velocity += (double)directionLocal.Z * wasd.Z * keyboardSensitivity;
                }

                if (Math.Abs(piston.Velocity) <= relaxation)
                {
                    piston.Velocity = 0;
                } else
                {
                    piston.Velocity -= piston.Velocity / Math.Abs(piston.Velocity) * relaxation;
                }

                stringBuilder.Append($"{directionLocal.ToString("0.00")} v: {piston.Velocity} l: {piston.CurrentPosition}\n");
            }

            display.WriteText(stringBuilder.ToString());

        }
    }
}
