using Sandbox.Game.Entities;
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
        List<IMyGyro> gyros;
        List<IMyThrust> thrusters;
        IMyShipController controller;
        LinearStabilization rollStabilization;
        LinearStabilization pitchStabilization;
        LinearStabilization thrustStabilization;

        double elevationSpeed;
        double elevationSpeedPrev;
        DateTime prevMeasurementTime = DateTime.Now;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            gyros = getGyros(this);
            thrusters = getThrusters(this);
            display = new Display(this);
            rollStabilization = new LinearStabilization(0.01f, 0.01f, 0f);
            pitchStabilization = new LinearStabilization(0.001f, 0.01f, 0f);
            thrustStabilization = new LinearStabilization(0.001f, 0.96f, 0.8f);

            controller = getFirstCockpit(this);
        }

        public void Save()
        {

        }

        public double computeRollForce(
            double targetVelocity, 
            double currentVelocity,
            double currentAngle, /*roughly corresponds to currentAcceleration*/
            double curentAngularVelocity,
            Display display
        ) {
                return AutopilotDerivativesWaterfall.cumputeLastDerivative(targetVelocity, new List<AutopilotDerivativesWaterfall.Derivative>
                {
                   new AutopilotDerivativesWaterfall.Derivative { name = "velocity X", maxValue = 100, currentValue = currentVelocity, stabilizatioThreshold = 40 },
                   new AutopilotDerivativesWaterfall.Derivative { name = "rollAngle", maxValue = 40, currentValue = currentAngle, stabilizatioThreshold = 30 },
                   new AutopilotDerivativesWaterfall.Derivative { name = "rollAngularVelocity", maxValue = 2, currentValue = curentAngularVelocity, stabilizatioThreshold = 1 },
                }, display);
        }


        public double computePitchForce(
            double targetVelocity,
            double currentVelocity,
            double currentAngle, /*roughly corresponds to currentAcceleration*/
            double currentAngularVelocity,
            Display display
        )
        {
            display.log($"currentAngularVelocity {currentAngularVelocity.ToString("0.00")}\n");
            return AutopilotDerivativesWaterfall.cumputeLastDerivative(targetVelocity, new List<AutopilotDerivativesWaterfall.Derivative>
                {
                   new AutopilotDerivativesWaterfall.Derivative { name = "velocity X", maxValue = 100, currentValue = currentVelocity, stabilizatioThreshold = 5 },
                   new AutopilotDerivativesWaterfall.Derivative { name = "pitchAngle", maxValue = 50, currentValue = currentAngle, stabilizatioThreshold = 10 },
                   new AutopilotDerivativesWaterfall.Derivative { name = "rollAngularVelocity", maxValue = 2, currentValue = currentAngularVelocity, stabilizatioThreshold = 1 },
                }, display);

        }

        public void Main(string argument, UpdateType updateSource)
        {
            var worldMatrix = controller.WorldMatrix;

            Vector3D planetCenter;
            if (!controller.TryGetPlanetPosition(out planetCenter))
            {
                display.log("Planet not found.\nThis is VTOL.\nTurning offline.");
                foreach (var gyro in gyros)
                {
                    gyro.GyroOverride = false;
                }
                return;
            }
            var upDirectionGlobal = Vector3D.Normalize(controller.GetPosition() - planetCenter);
            var localUp = Vector3D.TransformNormal(Vector3D.Normalize(upDirectionGlobal), MatrixD.Transpose(worldMatrix));

            Vector3D position = new Vector3D(worldMatrix.M41, worldMatrix.M42, worldMatrix.M43);

            Vector3D linearVelocity = Vector3D.TransformNormal(controller.GetShipVelocities().LinearVelocity, MatrixD.Transpose(worldMatrix));
            Vector3D angularVelocity = Vector3D.TransformNormal(controller.GetShipVelocities().AngularVelocity, MatrixD.Transpose(worldMatrix));
            
            double roll = (double)(-Math.Atan2(localUp.X, localUp.Y) * 180 / Math.PI);
            double pitch = (double)(Math.Atan2(localUp.Z, localUp.Y) * 180 / Math.PI);


            elevationSpeedPrev = elevationSpeed;
            elevationSpeed = (double) Vector3D.Dot(localUp, linearVelocity);
            DateTime now = DateTime.Now;
            double elevationSpeedDelta = (double)((elevationSpeed - elevationSpeedPrev) / (now - prevMeasurementTime).TotalSeconds);
            prevMeasurementTime = now;

            display.log($"roll {roll.ToString("0.00")}\n");

            double gyroRoll = computeRollForce(
                controller.MoveIndicator.X * 100,
                linearVelocity.X,
                roll,
                -angularVelocity.Z,
                display
            ) * 2;

            display.log($"\npitch {pitch.ToString("0.00")}\n");

            double gyroPitch = computePitchForce(
                -controller.MoveIndicator.Z * 100,
                 -linearVelocity.Z,
                 pitch,
                 -angularVelocity.X,
                 display
            ) * 2;

            double desiredElevationSpeedDelta = AutopilotDerivativesWaterfall.cumputeLastDerivative(controller.MoveIndicator.Y * 100, new List<AutopilotDerivativesWaterfall.Derivative>
            {
                new AutopilotDerivativesWaterfall.Derivative { name = "velocity Z", maxValue = 50, currentValue = (double)elevationSpeed, stabilizatioThreshold = 1f },
            }) * 10;

            double G = controller.GetNaturalGravity().Length();
            double shipMass = controller.CalculateShipMass().TotalMass;
            display.log($"G {G.ToString("0.000")} \n");
            display.log($"shipMass {shipMass.ToString("0.000")} \n");

            double forcePerThruster = (double)(shipMass * (G + desiredElevationSpeedDelta)) / thrusters.Count;
            double thrustValue = thrustStabilization.adjustValue(forcePerThruster, desiredElevationSpeedDelta, elevationSpeedDelta, this);

            display.log($"forcePerThruster {forcePerThruster.ToString("0.000")} \n");
            display.log($"thrustValue {thrustValue.ToString("0.000")}\ndesiredElevationSpeedDelta {desiredElevationSpeedDelta.ToString("0.000")} \n");
            display.log($"elevationSpeedDelta {elevationSpeedDelta.ToString("0.000")} \n");

            thrustValue = (double)((thrustValue + 1) / Math.Cos(pitch / 180 * Math.PI));

            display.log($"thrustValue {thrustValue.ToString("0.000")} \n");

            foreach (var thruster in thrusters)
            {
                thruster.ThrustOverride = (float) thrustValue;
            }

            foreach (var gyro in gyros)
            {
                gyro.GyroOverride = true;
                gyro.Yaw = (float) (controller.RotationIndicator.Y * (-controller.MoveIndicator.Z > 0.1 ? 0.3 : 1));
                gyro.Pitch = (float) gyroPitch;
                gyro.Roll = (float) gyroRoll + (-controller.MoveIndicator.Z > 0.1 ? 10 * controller.RotationIndicator.Y : 0);
            }

            display.flush();
        }
    }
}
