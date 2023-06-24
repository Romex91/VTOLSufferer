using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        // Hound-class ships has double-merge block engine connection for better survival rate
        // (you need to destroy two merge blocks to cut off the engine from the ship)
        // When connecting grids with two hinges there may be a clang force.
        // To avoid clang force only one hinge is active.
        class HingedEngine
        {
            List<IMyMotorAdvancedStator> hinges;
            List<IMyThrust> thrusters;

            MyGridProgram program;

            public HingedEngine(MyGridProgram myGridProgram, List<IMyMotorAdvancedStator> hinges, List<IMyThrust> thrusters)
            {
                this.program = myGridProgram;
                this.hinges = hinges;
                this.thrusters = thrusters;
            }

            public static List<HingedEngine> InitHingedEngines(MyGridProgram myGridProgram)
            {
                List<HingedEngine> hingePairs = new List<HingedEngine>();


                List<IMyMotorAdvancedStator> hinges = new List<IMyMotorAdvancedStator>();
                List<IMyThrust> thrustesrs = new List<IMyThrust>();
                myGridProgram.GridTerminalSystem.GetBlocksOfType(hinges);
                myGridProgram.GridTerminalSystem.GetBlocksOfType(thrustesrs);

                while (hinges.Count > 0)
                {
                    List<IMyMotorAdvancedStator> engineHinges = new List<IMyMotorAdvancedStator>();
                    List<IMyThrust> engineThrusters = new List<IMyThrust>(); 
                    engineHinges.Add(hinges[0]);
                    hinges.RemoveAt(0);

                    if (hinges.Count > 0)
                    {
                        var closestHinge = hinges.Find((IMyMotorAdvancedStator hinge) =>
                        {
                            var distance = Vector3D.Distance(hinge.GetPosition(), engineHinges[0].GetPosition());
                            return distance < 3;
                        });
                        if (closestHinge != null)
                        {
                            engineHinges.Add(closestHinge);
                            hinges.Remove(closestHinge);
                        }
                    }

                    thrustesrs.ForEach((IMyThrust thruster) => {
                        var distance = Vector3D.Distance(thruster.GetPosition(), engineHinges[0].GetPosition());
                        if (distance < 6)
                        {
                            engineThrusters.Add(thruster);
                        }
                    });

                    myGridProgram.Echo("Detected a hinge group with " + engineHinges.Count + " hinges and " + engineThrusters.Count + " thrusters ");
                    hingePairs.Add(new HingedEngine(myGridProgram, engineHinges, engineThrusters));
                }
                return hingePairs;
            }

            private IMyMotorAdvancedStator ActiveHinge { get
                {
                    IMyMotorAdvancedStator activeHinge = hinges[0]; ;
                    hinges.ForEach((IMyMotorAdvancedStator hinge) => {
                        if (!hinge.IsAttached || !hinge.IsFunctional) return;
                        activeHinge = hinge;
                    });

                    hinges.ForEach((IMyMotorAdvancedStator hinge) => {
                        if (hinge == activeHinge)
                        {
                            hinge.Torque = 100000000;
                        }else
                        {
                            hinge.Torque = 0;
                        }
                    });

                    return activeHinge;
                }
            }

            public double Angle { get {
                    return ActiveHinge.Angle;
                }
            }

            public double TargetVelocityRad
            {
                set 
                {
                    ActiveHinge.LowerLimitDeg = -87;
                    ActiveHinge.UpperLimitDeg = 87;
                    ActiveHinge.TargetVelocityRad = (float) value;
                }
                get
                {
                    return hinges[0].TargetVelocityRad;
                }
            }

            public MatrixD HingeWorldMatrix {
                get
                {
                    return ActiveHinge.WorldMatrix;
                }
            }
            public string Name { get {
                    return ActiveHinge.CustomName;
                }
            }

            public double ThrustOverride
            {
                set
                {
                    thrusters.ForEach((IMyThrust thruster) =>
                    {
                        thruster.ThrustOverride = (float) Math.Max(value, 1);
                    });
                }
            }
        }
    }
}
