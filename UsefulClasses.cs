using System;
using System.Collections.Generic;
using UnityEngine;
using CommNet.Occluders;

namespace BeamedPowerStandalone
{
    public class VesselFinder
    {
        // Loading all vessels that have WirelessSource module, and adding them to a list to use later
        public void SourceData(out List<Vessel> vesselList, out List<double> excess, out List<double> constant, out List<string> target, out List<string> wave)
        {
            ConfigNode Node = ConfigNode.Load(KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/persistent.sfs");
            ConfigNode FlightNode = Node.GetNode("GAME").GetNode("FLIGHTSTATE");

            vesselList = new List<Vessel>(); excess = new List<double>();
            constant = new List<double>(); target = new List<string>();
            wave = new List<string>();

            foreach (ConfigNode vesselnode in FlightNode.GetNodes("VESSEL"))
            {
               foreach (ConfigNode partnode in vesselnode.GetNodes("PART"))
               {
                  if (partnode.HasNode("MODULE"))
                  {
                     foreach (ConfigNode module in partnode.GetNodes("MODULE"))
                     {
                        if (module.GetValue("name") == "WirelessSource")
                        {
                           foreach (Vessel vessel in FlightGlobals.Vessels)
                           {
                              if (vesselnode.GetValue("name") == vessel.GetDisplayName())
                              {
                                 vesselList.Add(vessel);
                                 if (vessel.loaded)
                                 {
                                    foreach (Part part in vessel.Parts)
                                    {
                                       if (part.Modules.Contains<WirelessSource>())
                                       {
                                          excess.Add(Convert.ToDouble(part.Modules.GetModule<WirelessSource>().Fields.GetValue("excess")));
                                          constant.Add(Convert.ToDouble(part.Modules.GetModule<WirelessSource>().Fields.GetValue("constant")));
                                          target.Add(Convert.ToString(part.Modules.GetModule<WirelessSource>().Fields.GetValue("TransmittingTo")));
                                          wave.Add(Convert.ToString(part.Modules.GetModule<WirelessSource>().Fields.GetValue("Wavelength")));
                                          break;
                                       }
                                    }
                                 }
                                 else
                                 {
                                    excess.Add(Convert.ToDouble(module.GetValue("excess")));
                                    constant.Add(Convert.ToDouble(module.GetValue("constant")));
                                    target.Add(module.GetValue("TransmittingTo"));
                                    wave.Add(module.GetValue("Wavelength"));
                                 }
                                 break;
                              }
                           }
                           break;
                        }
                        else if (module.GetValue("name") == "WirelessReflector")
                        {
                           foreach (Vessel vessel in FlightGlobals.Vessels)
                           {
                              if (vesselnode.GetValue("name") == vessel.GetDisplayName())
                              {
                                 vesselList.Add(vessel);
                                 if (vessel.loaded)
                                 {
                                    foreach (Part part in vessel.Parts)
                                    {
                                       if (part.Modules.Contains<WirelessReflector>())
                                       {
                                          excess.Add(Convert.ToDouble(part.Modules.GetModule<WirelessReflector>().Fields.GetValue("excess")));
                                          constant.Add(Convert.ToDouble(part.Modules.GetModule<WirelessReflector>().Fields.GetValue("constant")));
                                          target.Add(Convert.ToString(part.Modules.GetModule<WirelessReflector>().Fields.GetValue("TransmittingTo")));
                                          wave.Add(Convert.ToString(part.Modules.GetModule<WirelessReflector>().Fields.GetValue("Wavelength")));
                                          break;
                                       }
                                    }
                                 }
                                 else
                                 {
                                    excess.Add(Convert.ToDouble(module.GetValue("excess")));
                                    constant.Add(Convert.ToDouble(module.GetValue("constant")));
                                    target.Add(module.GetValue("TransmittingTo"));
                                    wave.Add(module.GetValue("Wavelength"));
                                 }
                              }
                           }
                        }
                     }
                  }
               }
            }
        }

        // gets all receiver spacecraft's confignodes from savefile
        public void ReceiverData(out List<ConfigNode> receiversList)
        {
            ConfigNode Node = ConfigNode.Load(KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/persistent.sfs");
            ConfigNode FlightNode = Node.GetNode("GAME").GetNode("FLIGHTSTATE");

            receiversList = new List<ConfigNode>();

            foreach (ConfigNode vesselnode in FlightNode.GetNodes("VESSEL"))
            {
                foreach (ConfigNode partnode in vesselnode.GetNodes("PART"))
                {
                    if (partnode.HasNode("MODULE"))
                    {
                        foreach (ConfigNode module in partnode.GetNodes("MODULE"))
                        {
                            if (module.GetValue("name") == "WirelessReceiver" | module.GetValue("name") == "WirelessReceiverDirectional")
                            {
                                receiversList.Add(vesselnode);
                                break;
                            }
                            else if (module.GetValue("name") == "WirelessReflector" | module.GetValue("name") == "ThermalEngine")
                            {
                                receiversList.Add(vesselnode);
                                break;
                            }
                            else if (module.GetValue("name") == "AblativeEngine" | module.GetValue("name") == "PhotonSail")
                            {
                                receiversList.Add(vesselnode);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    // syncs animation state (eg retracted/extended) with power received/transmitted
    public class AnimationSync
    {
        public void SyncAnimationState(Part part)
        {
            if (part.Modules.Contains<ModuleDeployableAntenna>())
            {
                if (part.Modules.GetModule<ModuleDeployableAntenna>().deployState != ModuleDeployableAntenna.DeployState.EXTENDED)
                {
                    if (part.Modules.Contains<WirelessSource>())
                    {
                        part.Modules.GetModule<WirelessSource>().Fields.SetValue("Transmitting", false);
                    }
                    else if (part.Modules.Contains<WirelessReceiver>())
                    {
                        part.Modules.GetModule<WirelessReceiver>().Fields.SetValue("Listening", false);
                    }
                    else if (part.Modules.Contains<WirelessReceiverDirectional>())
                    {
                        part.Modules.GetModule<WirelessReceiverDirectional>().Fields.SetValue("Listening", false);
                    }
                }
            }
        }
    }

    public class PlanetOcclusion
    {
        // checks for occlusion by each celestial body
        public void IsOccluded(Vector3d source, Vector3d dest, string wavelength, out CelestialBody celestialBody, out bool occluded)
        {
            bool planetocclusion = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().planetOcclusion;
            Transform transform2; double radius2; celestialBody = new CelestialBody(); occluded = new bool();

            for (int x = 0; x < FlightGlobals.Bodies.Count; x++)
            {
                transform2 = FlightGlobals.Bodies[x].transform;
                radius2 = FlightGlobals.Bodies[x].Radius;
                celestialBody = FlightGlobals.Bodies[x];
                radius2 *= (wavelength == "Long") ? 0.7 : 0.9;

                OccluderHorizonCulling occlusion = new OccluderHorizonCulling(transform2, radius2, radius2, radius2);
                occlusion.Update();
                occluded = occlusion.Raycast(source, dest);
                if (occluded == true)
                {
                    break;
                }
            }
            if (planetocclusion == false)
            {
                occluded = false;
            }
        }
    }

    public class RelativisticEffects
    {
        bool relativistic; const float c = 299792452;
        public double RedOrBlueShift(Vessel source, Vessel dest, string state, out string status)
        {
            relativistic = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().relativistic; double powerMult;
            double v = Vector3d.Magnitude(source.velocityD - dest.velocityD);
            v *= Math.Cos(Vector3d.Angle((source.GetWorldPos3D() - dest.GetWorldPos3D()), (source.velocityD - dest.velocityD)));

            if (relativistic)
            {
                if (v >= c - 1)
                {
                    powerMult = 0;
                    status = "Exceeded Light Speed";
                }
                else
                {
                    powerMult = Math.Sqrt((1 + v / c) / (1 - v / c));
                    status = state;
                }
            }
            else
            {
                powerMult = 1;
                status = state;
            }
            return powerMult;
        }

        //public bool IsWarpDriveEngaged(Part part, double prevDist, double Dist, string state, out string status)
        //{
        //    bool engaged = false; relativistic = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().relativistic;

        //    if (part.Modules.Contains)
        //    {
        //        engaged = true;
        //    }
        //}
    }
}
