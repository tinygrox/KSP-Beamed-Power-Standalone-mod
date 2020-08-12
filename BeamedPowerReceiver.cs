using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    // Part module for spherical or multi-directional receivers
    // code relating to planetary occlusion has been commented out for now
    public class WirelessReceiver : PartModule
    {
        // UI-right click menu in flight
        [KSPField(guiName = "Power Receiver", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool Listening;

        [KSPField(guiName = "Received Power Limiter", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, requireFullControl = true, scene = UI_Scene.Flight)]
        public float percentagePower;

        [KSPField(guiName = "Received Power", isPersistant = false, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s")]
        public float received_power_ui;

        // 'recv_diameter' and 'recv_efficiency' values are set in part cfg file
        [KSPField(isPersistant = false)]
        public float recvDiameter;

        [KSPField(isPersistant = false)]
        public float recvEfficiency;

        // declaring frequently used variables
        Vector3d source; Vector3d dest; double received_power; int frames;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        TransmitterVessel transmitter = new TransmitterVessel();
        // BPOcclusion occlusion = new BPOcclusion();

        List<Vessel> CorrectVesselList;
        List<double> excessList;
        List<double> constantList;
        List<string> targetList;

        public void Start()
        {
            frames = 145;
            CorrectVesselList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            targetList = new List<string>();
        }

        // setting action group capability
        [KSPAction(guiName = "Toggle Power Receiver")]
        public void ToggleBPReceiver(KSPActionParam param)
        {
            Listening = Listening ? false : true;
        }

        [KSPAction(guiName = "Activate Power Receiver")]
        public void ActivateBPReceiver(KSPActionParam param)
        {
            Listening = Listening ? true : true;
        }

        [KSPAction(guiName = "Deactivate Power Receiver")]
        public void DeactivateBPReceiver(KSPActionParam param)
        {
            Listening = Listening ? false : false;
        }

        // adding part info to part description tab in editor
        public string GetModuleTitle()
        {
            return "Wireless Source";
        }
        public override string GetModuleDisplayName()
        {
            return "Beamed Power Receiver";
        }
        public override string GetInfo()
        {
            return ("Dish Diameter: " + Convert.ToString(recvDiameter) + "\n"
                + "Efficiency: " + Convert.ToString(recvEfficiency) + "\n"
                + "Receiver Type: Sphere");
        }

        // main block of code - runs every physics frame
        public void FixedUpdate()
        {
            frames += 1;
            if (frames == 150)
            {
                transmitter.LoadVessels(out CorrectVesselList, out excessList, out constantList, out targetList, out _);
                frames = 0;
            }
            
            if (CorrectVesselList.Count > 0)
            {
                if (Listening == true)
                {
                    dest = this.vessel.GetWorldPos3D();
                    received_power = 0;

                    // adds up all the received power values from all vessels in CorrectVesselList 
                    for (int n = 0; n < CorrectVesselList.Count; n++)
                    {
                        if (targetList[n] == this.vessel.GetDisplayName())
                        {
                            double excess2 = excessList[n]; double constant2 = constantList[n];
                            source = CorrectVesselList[n].GetWorldPos3D();
                            double distance = Vector3d.Distance(source, dest);
                            double spotsize = constant2 * distance;

                            // adding EC that has been received
                            if (recvDiameter < spotsize)
                            {
                                //if (DirectionalClass.CheckifOccluded(CorrectVesselList[n], this.vessel)==false)
                                //{
                                received_power += Math.Round(((recvDiameter / spotsize) * recvEfficiency * excess2 * (percentagePower / 100)), 1);
                                //}
                            }
                            else
                            {
                                //if (DirectionalClass.CheckifOccluded(CorrectVesselList[n], this.vessel) == false)
                                //{
                                received_power += Math.Round(((recvEfficiency * excess2) * (percentagePower / 100)), 1);
                                //}
                            }
                        }
                    }
                    BPSettings settings = new BPSettings();
                    if (settings.BackgroundProcessing == false)
                    {
                        this.part.RequestResource(EChash, -received_power * Time.fixedDeltaTime);
                    }
                    received_power_ui = Convert.ToSingle(received_power);
                }
                else
                {
                    received_power_ui = 0;
                    received_power = 0;
                }
            }
            else
            {
                received_power_ui = 0;
                received_power = 0;
            }
        }
    }

    public class TransmitterVessel
    {
        // Loading all vessels that have WirelessSource module, and adding them to a list to use later
        public void LoadVessels(out List<Vessel> list1, out List<double> list2, out List<double> list3, out List<string> list4, out List<string> list5)
        {
            ConfigNode Node = ConfigNode.Load(KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/persistent.sfs");
            ConfigNode FlightNode = Node.GetNode("GAME").GetNode("FLIGHTSTATE");
            list1 = new List<Vessel>(); list2 = new List<double>();
            list3 = new List<double>(); list4 = new List<string>();
            list5 = new List<string>();

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
                                list2.Add(Convert.ToDouble(module.GetValue("excess")));
                                list3.Add(Convert.ToDouble(module.GetValue("constant")));
                                list4.Add(module.GetValue("TransmittingTo"));
                                list5.Add(module.GetValue("Wavelength"));
                                foreach (Vessel vessel in FlightGlobals.Vessels)
                                {
                                    if (vesselnode.GetValue("name") == vessel.GetDisplayName())
                                    {
                                        list1.Add(vessel);
                                        break;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

    }
}
 