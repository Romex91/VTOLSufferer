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

        List<HingedEngine> engineHinges;
        List<IMyThrust> thrusters;
        LinearStabilization thrustStabilization = new LinearStabilization(0.001f, 0.96f, 0.8f);
        
        IMyShipController controller;


        Vector3 desiredVelocity = new Vector3I();
        
        DoubleMergeBlocksConnectionConroller doubleMergeBlocksConnectionConroller;

        double elevationSpeed;
        double elevationSpeedPrev;
        double elevationSpeedDelta;

        DateTime prevMeasurementTime = DateTime.Now;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            gyros = getGyros(this);
            display = new Display(this);

            thrusters = getThrusters(this);

            controller = getFirstCockpit(this);

            engineHinges = HingedEngine.InitHingedEngines(this);

            doubleMergeBlocksConnectionConroller = new DoubleMergeBlocksConnectionConroller(this, display, controller);
        }

        public void Save()
        {

        }

        public void controlEngines(double pitch, double roll, double elevationSpeed, double elevationSpeedDelta, Vector3D linearVelocity)
        {
            var worldMatrix = controller.WorldMatrix;

            double vericalPitchFactor = (double)(Math.Sin(pitch / 180 * Math.PI));
            double horizontalPitchFactor = (double)(Math.Cos(pitch / 180 * Math.PI));
            display.log("f:" + vericalPitchFactor.ToString() + " e :" + elevationSpeed + "\n");

            double desiredElevationSpeedDelta = AutopilotDerivativesWaterfall.cumputeLastDerivative(desiredVelocity.Y + desiredVelocity.Z * vericalPitchFactor, new List<AutopilotDerivativesWaterfall.Derivative>
            {
                new AutopilotDerivativesWaterfall.Derivative { name = "velocity Z", maxValue = 50, currentValue = (double)elevationSpeed, stabilizatioThreshold = 1f },
            }) * 10;

            display.log("d :" + desiredElevationSpeedDelta.ToString("0.0") + "\n");
            display.log("a :" + elevationSpeedDelta.ToString("0.0") + "\n");

            double G = controller.GetNaturalGravity().Length();
            int functionalThrustersCount = thrusters.Count((IMyThrust thruster) => { return thruster.IsFunctional; });
            double forcePerThruster = (double)(controller.CalculateShipMass().TotalMass * (G + desiredElevationSpeedDelta) / functionalThrustersCount);

            forcePerThruster = thrustStabilization.adjustValue(forcePerThruster, desiredElevationSpeedDelta, elevationSpeedDelta, this);

            display.log("G :" + G.ToString("0.0000"));
            display.log(" Mass : " + controller.CalculateShipMass().TotalMass.ToString("0.0"));
            display.log(" N:" + functionalThrustersCount.ToString() + "\n");

            foreach (HingedEngine engine in engineHinges)
            {
                var hingeMatrix = engine.HingeWorldMatrix;
                var direction = new Vector3D(hingeMatrix.M21, hingeMatrix.M22, hingeMatrix.M23);
                var directionLocal = Vector3D.TransformNormal(direction, MatrixD.Transpose(worldMatrix));
                
                var orientation = directionLocal.X > 0.8 ? 1 : -1;
                double engineToGroundDegrees = (double) (engine.Angle * orientation / Math.PI * 180 - pitch);

                double localThrustValue = (double)((forcePerThruster) / Math.Cos(engineToGroundDegrees / 180 * Math.PI) / Math.Cos(roll/180 * Math.PI));
                engine.ThrustOverride = localThrustValue;
                display.log("Thrust :" + localThrustValue.ToString("0.0") + "\n");
                //display.log("engineToGroundDegrees :" + engineToGroundDegrees.ToString("0.0") + "\n");

                engine.TargetVelocityRad = orientation * AutopilotDerivativesWaterfall.cumputeLastDerivative(desiredVelocity.Z * horizontalPitchFactor, new List<AutopilotDerivativesWaterfall.Derivative>
                {
                   new AutopilotDerivativesWaterfall.Derivative { name = "velocity Y", maxValue = 125, currentValue = (double)linearVelocity.Z, stabilizatioThreshold = 1f },
                   new AutopilotDerivativesWaterfall.Derivative { name = "engine-to-ground Angle", maxValue = 50, currentValue = engineToGroundDegrees, stabilizatioThreshold = 5f },
                }) * 5;

                // display.log($"{rotor.Name} {orientation.ToString("0.0")} angle: ${ (rotor.Angle * 180 / Math.PI).ToString("0.0") } ${ rotor.TargetVelocityRad.ToString("0.0") } \n");
                // display.log($"{(rotor.Angle * orientation - angleDeg * Math.PI / 180).ToString("0.0") } \n");
            }
        }

        void UpdateDesiredVelocity(Vector3 linearVelocity)
        {

            if (controller.MoveIndicator.X == 0)
            {
                desiredVelocity.X = 0;
            }
            else {
                if (desiredVelocity.X * linearVelocity.X > 0 && Math.Abs(desiredVelocity.X) < Math.Abs(linearVelocity.X))
                {
                    desiredVelocity.X = linearVelocity.X;
                }
                desiredVelocity.X = Math.Max(-100, Math.Min(100, desiredVelocity.X + controller.MoveIndicator.X / 6f)); 
            }

            desiredVelocity.Y = controller.MoveIndicator.Y * 20;

            if (controller.MoveIndicator.Z == 0)
            {
                desiredVelocity.Z = 0;
            }
            else
            {
                if (desiredVelocity.Z * linearVelocity.Z > 0 && Math.Abs(desiredVelocity.Z) < Math.Abs(linearVelocity.Z))
                {
                    desiredVelocity.Z = linearVelocity.Z;
                }
                desiredVelocity.Z = Math.Max(-125, Math.Min(125, desiredVelocity.Z + controller.MoveIndicator.Z / 4f));
            }

            display.log("d" + desiredVelocity.ToString("0.00") + "\n");
            display.log("a" + linearVelocity.ToString("0.00") + "\n");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            var matrix = controller.WorldMatrix;
            Vector3D position = new Vector3D(matrix.M41, matrix.M42, matrix.M43);

            var localUp = Vector3D.TransformNormal(Vector3D.Normalize(position), MatrixD.Transpose(matrix));
            Vector3D linearVelocity = Vector3D.TransformNormal(controller.GetShipVelocities().LinearVelocity, MatrixD.Transpose(matrix));
            Vector3D angularVelocity = Vector3D.TransformNormal(controller.GetShipVelocities().AngularVelocity, MatrixD.Transpose(matrix));
            double roll = (double)(-Math.Atan2(localUp.X, localUp.Y) * 180 / Math.PI);

            double pitch = (double)(Math.Atan2(localUp.Z, localUp.Y) * 180 / Math.PI);

            elevationSpeedPrev = elevationSpeed;
            elevationSpeed = (double)Vector3D.Dot(localUp, linearVelocity);

            DateTime now = DateTime.Now;
            elevationSpeedDelta = (double)((elevationSpeed - elevationSpeedPrev)/(now - prevMeasurementTime).TotalSeconds);
            prevMeasurementTime = now;

            UpdateDesiredVelocity(linearVelocity);

            double gyroRoll = 5 * AutopilotDerivativesWaterfall.cumputeLastDerivative(desiredVelocity.X, new List<AutopilotDerivativesWaterfall.Derivative>
                {
                   new AutopilotDerivativesWaterfall.Derivative { name = "velocity X", maxValue = 100, currentValue = (double)linearVelocity.X },
                   new AutopilotDerivativesWaterfall.Derivative { name = "rollAngle", maxValue = 40, currentValue = roll, stabilizatioThreshold = 5f },
                   new AutopilotDerivativesWaterfall.Derivative { name = "rollAngularVelocity", maxValue = 1, currentValue = (double) - angularVelocity.Z, stabilizatioThreshold = 2f },
                });


            foreach (var gyro in gyros)
            {
                gyro.GyroOverride = true;
                gyro.Yaw = controller.RotationIndicator.Y;
                gyro.Pitch = controller.RotationIndicator.X;
                gyro.Roll = (float) gyroRoll;
            }

            controlEngines(pitch, roll, elevationSpeed, elevationSpeedDelta, linearVelocity);

            display.flush();
            doubleMergeBlocksConnectionConroller.Control();
        }
    }
}
