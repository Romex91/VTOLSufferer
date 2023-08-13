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
                public double stabilizatioThreshold = 1f;
            }


            // https://www.desmos.com/calculator/m27bzme75s
            public static double applyStabilizationThreshold(double normalizedValue, double normalizedThreshold)
            {
                if (normalizedThreshold <= 0 || normalizedThreshold >= 1) throw new Exception("power should be > 0"); ;
                if (normalizedValue > normalizedThreshold) return 1;
                if (normalizedValue >= -normalizedThreshold) return normalizedValue / normalizedThreshold;
                return -1;
            }


            public static double normalize(double value, double maxValue, Display display)
            {
                double normalized = value / maxValue;
                if (normalized > 1 || normalized < -1)
                {
                    if (display != null)  display.log("normalizedValue shouldn't exceed maxValue");
                }
                return Math.Max(-1, Math.Min(1, normalized)); ;
            }

            // If targetValue is distance then
            // derivatives[0] is distance
            // derivatives[1] (first derivative of distance) is velocity
            // derivatives[2] is acceleration
            public static double computeLastDerivative(double targetValue, List<Derivative> derivatives, Display display = null)
            {
                double normalizedTargetValue = normalize(targetValue, derivatives[0].maxValue, display);
                normalizedTargetValue = Math.Max(-1, Math.Min(1, normalizedTargetValue));

                for (int i = 0; i < derivatives.Count; i++)
                {
                    var normalizedCurrentValue = normalize(derivatives[i].currentValue, derivatives[i].maxValue, display);
                    var normalizedStabilizationThreshold = normalize(derivatives[i].stabilizatioThreshold, derivatives[i].maxValue, display);
                    // The bigger the difference between target and current values the larger the next
                    // derivative should be to compensate for the difference.
                    // Examples:
                    // If target distance is 100KM and current distance is 1KM, we want the highest speed possible
                    // If target velocity is -100m/s and current velocity is 100m/s we want the highest decceleration possible
                    // If target distance is 100KM and current distance is 99.9KM it means that we're almost there and need to
                    // lower the speed down in order to not fly too far. 
                    double prevTargetValue = normalizedTargetValue;
                    normalizedTargetValue = applyStabilizationThreshold((normalizedTargetValue - normalizedCurrentValue) / 2, normalizedStabilizationThreshold);

                    if (display != null) display.log($"{derivatives[i].name}: targ{prevTargetValue.ToString("0.00")} cur{normalizedCurrentValue.ToString("0.00")} next{normalizedTargetValue.ToString("0.00")}\n");
                }

                return normalizedTargetValue;
            }
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

                double gyroRoll = AutopilotDerivativesWaterfall.computeLastDerivative(controller.MoveIndicator.X * 100, new List<AutopilotDerivativesWaterfall.Derivative>
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
