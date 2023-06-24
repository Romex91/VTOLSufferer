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
        public class LinearStabilization
        {
            double stabilizationFactor;
            double stabilizationSpeed;
            double maxAdjustment;

            Display display;

            public LinearStabilization(double stabilizationSpeed, double initialStabilizationFactor, double maxAdjustment, Display display = null)
            {
                this.stabilizationSpeed = stabilizationSpeed;
                this.stabilizationFactor = initialStabilizationFactor;
                this.maxAdjustment = maxAdjustment;
                this.display = display;
            }

            void log(string message)
            {
                if (display != null)
                {
                    display.log(message);
                }
            }

            public double adjustValue(double valueToAdjust, double targetValue, double actualValue, MyGridProgram program)
            {
                if (targetValue < actualValue)
                {
                    stabilizationFactor *= 1 - stabilizationSpeed;
                } else
                {
                    stabilizationFactor *= 1 + stabilizationSpeed;
                }

                stabilizationFactor = Math.Max(1 - this.maxAdjustment, Math.Min(1 + maxAdjustment, stabilizationFactor));

                program.Echo("factor: " + stabilizationFactor.ToString() + "\n");

                return valueToAdjust * stabilizationFactor;
            }
        }

        // Derivatives are essential when automatically controlling an aircraft with vertical thrusters.
        // Acceleration is a derivative of Velocity. It also roughly coincides with roll angle.
        // Angular velocity is a derivative of roll angle.
        // Hyros jerk strength is a derviative of angular velocity.
        // To control linear speed by adjusting hyros power it's necessary to take into account each of these derivatives.
        // Otherwise it will swing never reaching equilibria.
        public class AutopilotDerivativesWaterfall
        {
            public class Derivative
            {
                public string name;
                public double maxValue;
                public double currentValue;
                public double sensitivityMultiplier = 1f;
            }


            // https://www.desmos.com/calculator/udg7ema3fd
            // 
            public static double upsideDownU(double maxValue, double power)
            {
                return -maxValue * ( (double) Math.Pow(Math.Abs(maxValue), power) ) + maxValue;
            }

            public static double normalize(double value, double maxValue, Display display)
            {
                double normalized = value / maxValue;
                if (normalized > 1 || normalized < -1)
                {
                    display.log("value shouldn't exceed maxValue");
                }
                return Math.Max(-1, Math.Min(1, normalized)); ;
            }

            public static double cumputeLastDerivative(double targetValue, List<Derivative> derivatives, Display display = null)
            {
                double normalizedTargetValue = targetValue / derivatives[0].maxValue;
                normalizedTargetValue = Math.Max(-1, Math.Min(1, normalizedTargetValue));

                if (derivatives[0].sensitivityMultiplier != 1f)
                {
                    throw new Exception("There is no point in setting sensitivityMultiplier for the first derivative. It only makes sense when set for 2nd or 3rd derivatives.");
                }

                for (int i = 0; i < derivatives.Count; i++)
                {
                    if (display != null) display.log($"{derivatives[i].name} {normalizedTargetValue.ToString("0.000")}={derivatives[i].sensitivityMultiplier.ToString("0.000")}\n");

                    double sensitivityMultiplier = normalizedTargetValue == 0 ? derivatives[i].sensitivityMultiplier : (1 - derivatives[i].sensitivityMultiplier) * normalizedTargetValue * normalizedTargetValue / Math.Abs(normalizedTargetValue) + derivatives[i].sensitivityMultiplier;
                    normalizedTargetValue *= sensitivityMultiplier;
                    normalizedTargetValue = Math.Max(-1, Math.Min(1, normalizedTargetValue));

                    if (display != null)
                        display.log($"d{(normalizedTargetValue * derivatives[i].maxValue).ToString("0.000")} a{derivatives[i].currentValue.ToString("0.000")} s{sensitivityMultiplier.ToString("0.000")} \n");

                    normalizedTargetValue = normalizedTargetValue - derivatives[i].currentValue / derivatives[i].maxValue;
                    normalizedTargetValue = Math.Max(-1, Math.Min(1, normalizedTargetValue));
                }

                return normalizedTargetValue;
            }

            //public static double cumputeLastDerivative(double targetValue, List<Derivative> derivatives, Display display = null)
            //{
            //    double normalizedTargetValue = targetValue / derivatives[0].maxValue;
            //    normalizedTargetValue = Math.Max(-1, Math.Min(1, normalizedTargetValue));

            //    if (derivatives[0].sensitivityMultiplier != 1f)
            //    {
            //        throw new Exception("There is no point in setting sensitivityMultiplier for the first derivative. It only makes sense when set for 2nd or 3rd derivatives.");
            //    }

            //    for (int i = 0; i < derivatives.Count; i++)
            //    {
            //        if (display != null) display.log($"{derivatives[i].name} {normalizedTargetValue.ToString("0.000")}={derivatives[i].sensitivityMultiplier.ToString("0.000")}\n");

            //        double sensitivityMultiplier = normalizedTargetValue == 0 ? derivatives[i].sensitivityMultiplier : (1 - derivatives[i].sensitivityMultiplier) * normalizedTargetValue * normalizedTargetValue / Math.Abs(normalizedTargetValue) + derivatives[i].sensitivityMultiplier;
            //        normalizedTargetValue *= sensitivityMultiplier;
            //        normalizedTargetValue = Math.Max(-1, Math.Min(1, normalizedTargetValue));

            //        if (display != null)
            //            display.log($"d{(normalizedTargetValue * derivatives[i].maxValue).ToString("0.000")} a{derivatives[i].currentValue.ToString("0.000")} s{sensitivityMultiplier.ToString("0.000")} \n");

            //        normalizedTargetValue = normalizedTargetValue - derivatives[i].currentValue / derivatives[i].maxValue;
            //        normalizedTargetValue = Math.Max(-1, Math.Min(1, normalizedTargetValue));
            //    }

            //    return normalizedTargetValue;
            //}
        }
       
        public class RollStabilizer
        {
            IMyShipController controller;
            List<IMyGyro> gyros;
            Display display;

            public RollStabilizer(IMyShipController controller, List<IMyGyro> gyros, Display display)
            {
                this.controller = controller;
                this.gyros = gyros;
                this.display = display;
            }

            public void overrideControls()
            {
                var matrix = controller.WorldMatrix;
                Vector3D position = new Vector3D(matrix.M41, matrix.M42, matrix.M43);
                var localUp = Vector3D.TransformNormal(Vector3D.Normalize(position), MatrixD.Transpose(matrix));
                Vector3D linearVelocity = Vector3D.TransformNormal(controller.GetShipVelocities().LinearVelocity, MatrixD.Transpose(matrix));
                Vector3D angularVelocity = Vector3D.TransformNormal(controller.GetShipVelocities().AngularVelocity, MatrixD.Transpose(matrix));
                double roll = (double) (-Math.Atan2(localUp.X, localUp.Y) * 180 / Math.PI);

                double gyroRoll = AutopilotDerivativesWaterfall.cumputeLastDerivative(controller.MoveIndicator.X * 100, new List<AutopilotDerivativesWaterfall.Derivative>
                {
                   new AutopilotDerivativesWaterfall.Derivative { name = "velocity", maxValue = 100, currentValue = (double)linearVelocity.X },
                   new AutopilotDerivativesWaterfall.Derivative { name = "rollAngle", maxValue = 5, currentValue = roll },
                   new AutopilotDerivativesWaterfall.Derivative { name = "rollAngularVelocity", maxValue = 1f, currentValue = (double) - angularVelocity.Z },
                }, display);

                display.log("gyroRoll" + gyroRoll.ToString("0.00") + "\n");

                foreach (var gyro in gyros)
                {
                    gyro.GyroOverride = true;
                    gyro.Yaw = controller.RotationIndicator.Y;
                    gyro.Pitch = controller.RotationIndicator.X;
                    gyro.Roll = (float) gyroRoll;
                }
            }
        }
    }
}
