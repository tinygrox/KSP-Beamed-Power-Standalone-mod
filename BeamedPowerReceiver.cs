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
        public Vector3d source; public Vector3d dest; double received_power;
        public List<Vessel> CorrectVesselList; public int frames;
        public List<double> excessList; public List<double> constantList;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        // WirelessSource modulesource = new WirelessSource();

        public void Start()
        {
            frames = 595;
            CorrectVesselList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
        }

        // setting action group capability
        [KSPAction(guiName = "Toggle Power Receiver")]
        public void ToggleBPReceiver(KSPActionParam param)
        {
            if (Listening == true)
            {
                Listening = false;
            }
            else
            {
                Listening = true;
            }
        }

        [KSPAction(guiName = "Activate Power Receiver")]
        public void ActivateBPReceiver(KSPActionParam param)
        {
            if (Listening == false)
            {
                Listening = true;
            }
        }

        [KSPAction(guiName = "Deactivate Power Receiver")]
        public void DeactivateBPReceiver(KSPActionParam param)
        {
            if (Listening == true)
            {
                Listening = false;
            }
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

        // Loading all vessels that have WirelessSource module, and adding them to a list to use later
        private void LoadVesselsList()
        {
            ConfigNode BPNode = ConfigNode.Load(KSPUtil.ApplicationRootPath + "GameData/BeamedPowerStandalone/PluginData/save.cfg");
            CorrectVesselList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            frames = 0;
            for (int x = 0; x < FlightGlobals.Vessels.Count; x++)
            {
                for (int n = 0; n < BPNode.nodes.Count; n++)
                {
                    string vesselId = BPNode.nodes[n].GetValue("persistentId");
                    if (Convert.ToString(FlightGlobals.Vessels[x].id) == vesselId)
                    {
                        CorrectVesselList.Add(FlightGlobals.Vessels[x]);
                        double excess1 = Convert.ToDouble(BPNode.nodes[n].GetValue("excess"));
                        double excess2 = Convert.ToDouble(BPNode.nodes[n].GetValue("constant"));
                        excessList.Add(excess1);
                        constantList.Add(excess2);
                        break;
                    }
                }
            }
        }

        // main block of code - runs every physics frame
        public void FixedUpdate()
        {
            frames += 1;
            if (frames == 600)
            {
                LoadVesselsList();
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
                    this.part.RequestResource(EChash, -received_power * Time.fixedDeltaTime);
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
}
 