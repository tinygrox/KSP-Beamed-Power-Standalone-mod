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
            //else if (part.Modules.Contains<ModuleDeployablePart>())
            //{
            //    if (part.Modules.GetModule<ModuleDeployablePart>().deployState != ModuleDeployablePart.DeployState.EXTENDED)
            //    {
            //        if (part.Modules.Contains<PhotonSail>())
            //        {
            //            part.Modules.GetModule<PhotonSail>().Fields.SetValue("received_power", 0f);
            //        }
            //    }
            //}
        }
    }

    public class OcclusionData
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

    // a class used for some of the propulsion modules
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
            double flux = (Angle < 90 & Angle > -90) ? Math.Cos(Angle) : 0;
            return flux;
        }
    }

    public class ReceiverPowerCalc : PartModule
    {
        // adds received power calculator to receivers right-click menu in editor

        [KSPField(guiName = "Distance", groupName = "calculator1", groupDisplayName = "Received Power Calculator", groupStartCollapsed = true, guiUnits = "Mm", guiActive = false, guiActiveEditor = true, isPersistant = false), UI_FloatRange(minValue = 0, maxValue = 10000000, stepIncrement = 0.001f, scene = UI_Scene.Editor)]
        public float dist_ui;

        [KSPField(guiName = "Source Dish Diameter", groupName = "calculator1", guiUnits = "m", guiActive = false, guiActiveEditor = true, isPersistant = false), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float dish_dia_ui;

        [KSPField(guiName = "Source Efficiency", groupName = "calculator1", guiActive = false, guiActiveEditor = true, guiUnits = "%", isPersistant = false), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float efficiency;

        [KSPField(guiName = "Power Beamed", groupName = "calculator1", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true, isPersistant = false), UI_FloatRange(minValue = 0, maxValue = 100000, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float beamedPower;

        [KSPField(guiName = "Result", groupName = "calculator1", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true, isPersistant = false)]
        public float powerReceived;

        [KSPField(guiName = "Beamed Wavelength", groupName = "calculator1", guiActiveEditor = true, guiActive = false, isPersistant = false)]
        public string wavelength_ui;

        [KSPEvent(guiName = "Toggle Wavelength", guiActive = false, guiActiveEditor = true, groupName = "calculator1", isPersistent = false)]
        public void ToggleWavelength()
        {
            wavelength_ui = (wavelength_ui == "Long") ? "Short" : "Long";
        }

        public void CalculatePower(float recvDia, float recvefficiency)
        {
            float wavelength_num = (float)((wavelength_ui == "Long") ? Math.Pow(10, -3) : 5 * Math.Pow(10, -8));
            float spot_size = (float)(1.44 * wavelength_num * dist_ui * 1000000 / dish_dia_ui);
            powerReceived = (spot_size > recvDia) ?
                recvDia / spot_size * beamedPower * (efficiency / 100) * recvefficiency : beamedPower * (efficiency / 100) * recvefficiency;
            powerReceived = (float)Math.Round(powerReceived, 1);
        }
    }
}
