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
                                break;
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
                        }
                    }
                }
            }
        }
    }

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
            else if (part.Modules.Contains<ModuleDeployablePart>())
            {
                if (part.Modules.GetModule<ModuleDeployablePart>().deployState != ModuleDeployablePart.DeployState.EXTENDED)
                {
                    if (part.Modules.Contains<PhotonSail>())
                    {
                        part.Modules.GetModule<PhotonSail>().Fields.SetValue("received_power", 0f);
                    }
                }
            }
        }
    }

    public class RelativeOrientation
    {
        public double FractionalFlux(Vector3d source_pos, Vector3d dest_pos, Vessel receiver, Part recvPart)
        {
            Vector3 resultant = source_pos - dest_pos;
            Vector3 upvector = receiver.upAxis;
            Quaternion vesselRot = Quaternion.FromToRotation(resultant, upvector);
            Quaternion partRotation = recvPart.attRotation;
            Quaternion resRotation = vesselRot * partRotation;
            resRotation.ToAngleAxis(out float Angle, out _);
            double flux = (Angle < 90 &  Angle > -90) ? Math.Cos(Angle) : 0;
            return flux;
        }
    }

    // get if receiver is occluded from source by a celestial body
    public class BPOcclusion : OccluderHorizonCulling
    {
        public BPOcclusion(Transform transform1, double radiusx1, double radiusy1, double radiusz1, CelestialBody body1) 
            : base(transform1, radiusx1, radiusy1, radiusz1)
        {
            transform = transform1;
            radiusXRecip = radiusx1;
            radiusYRecip = radiusy1;
            radiusZRecip = radiusz1;
            invRotation = transform1.rotation.Inverse();
            body = body1;
            useBody = true;
        }  
    }
}
