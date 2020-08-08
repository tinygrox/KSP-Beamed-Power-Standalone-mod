using System;
using System.Collections.Generic;
using UnityEngine;
//using CommNet.Occluders;

namespace BeamedPowerStandalone
{
    // code relating to planetary occlusion has been commented out for now
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class WirelessReceiverDirectional : PartModule
    {
        public List<Vessel> CorrectVesselList;
        // UI-right click menu in flight
        [KSPField(guiName = "Power Receiver", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool Listening;

        [KSPField(guiName = "Received Power Limiter", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, requireFullControl = true, scene = UI_Scene.Flight)]
        public float percentagePower;

        [KSPField(guiName = "Received Power", isPersistant = false, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s")]
        public float received_power_ui;

        [KSPField(guiName = "Receiving from", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        public string CorrectVesselName;

        // 'recv_diameter' and 'recv_efficiency' values are set in part cfg file
        [KSPField(isPersistant = false)]
        public float recvDiameter;

        [KSPField(isPersistant = false)]
        public float recvEfficiency;

        // declaring frequently used variables
        public Vector3d source; public Vector3d dest; ConfigNode BPNode;
        public int frames; public int counter; double received_power;
        public List<double> excessList; public List<double> constantList; public List<string> TargetList;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        // WirelessSource modulesource = new WirelessSource();

        public void Start()
        {
            frames = 595;
            counter = 0;
            CorrectVesselList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            TargetList = new List<string>();
            BPNode = new ConfigNode();
        }

        [KSPEvent(guiName = "Cycle through vessels", guiActive = true, isPersistent = false, requireFullControl = true)]
        public void VesselCounter()
        {
            if (CorrectVesselList.Count > 0)
            {
                if (counter < CorrectVesselList.Count - 1)
                {
                    counter += 1;
                }
                else
                {
                    counter = 0;
                }
            }
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
                + "Receiver Type: Directional");
        }

        // get if receiver is occluded from source by a celestial body (commented for now)
        //public bool CheckifOccluded(Vessel inputSource, Vessel inputDest)
        //{
        //    source = inputSource.GetWorldPos3D();
        //    dest = inputDest.GetWorldPos3D();
        //    CelestialBody planet = new CelestialBody();
        //    OccluderHorizonCulling objOccluders = new OccluderHorizonCulling(planet.GetTransform(), planet.Radius, planet.Radius, planet.Radius);
        //    bool occluded = objOccluders.Raycast(source, dest);
        //    return occluded;
        //}

        // Loading all vessels that have WirelessSource module, and adding them to a list to use later
        private void LoadVesselsList()
        {
            BPNode = ConfigNode.Load(KSPUtil.ApplicationRootPath + "GameData/BeamedPowerStandalone/PluginData/save.cfg");
            CorrectVesselList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            TargetList = new List<string>();
            frames = 0;
            for (int x = 0; x < FlightGlobals.Vessels.Count; x++)
            {
                for (int n = 0; n < BPNode.nodes.Count; n++)
                {
                    string vesselId = BPNode.nodes[n].GetValue("vesselId");
                    if (Convert.ToString(FlightGlobals.Vessels[x].id) == vesselId)
                    {
                        CorrectVesselList.Add(FlightGlobals.Vessels[x]);
                        double excess1 = Convert.ToDouble(BPNode.nodes[n].GetValue("excess"));
                        double excess2 = Convert.ToDouble(BPNode.nodes[n].GetValue("constant"));
                        string Target = BPNode.nodes[n].GetValue("TransmittingTo");
                        excessList.Add(excess1);
                        constantList.Add(excess2);
                        TargetList.Add(Target);
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
                string vesselId = Convert.ToString(this.vessel.id);
                if (Listening == true & (TargetList[counter] == "NoVessel" | TargetList[counter] == vesselId))
                {
                    for (int m = 0; m < BPNode.nodes.Count; m++)
                    {
                        string vessel2id = Convert.ToString(CorrectVesselList[counter].id);
                        if (BPNode.nodes[m].GetValue("vesselId") == vessel2id)
                        {
                            BPNode.nodes[m].SetValue("TransmittingTo", vesselId);
                        }
                    }

                    dest = this.vessel.GetWorldPos3D();
                    double excess2 = excessList[counter]; double constant2 = constantList[counter];
                    CorrectVesselName = CorrectVesselList[counter].GetDisplayName();
                    source = CorrectVesselList[counter].GetWorldPos3D();
                    double distance = Vector3d.Distance(source, dest);
                    double spotsize = constant2 * distance; received_power = 0;

                    // adding EC that has been received
                    if (recvDiameter < spotsize)
                    {
                        //if (CheckifOccluded(CorrectVesselList[counter], this.vessel)==false)
                        //{
                        received_power = Math.Round(((recvDiameter / spotsize) * recvEfficiency * excess2 * (percentagePower / 100)), 1);
                        //}
                    }
                    else
                    {
                        //if (CheckifOccluded(CorrectVesselList[counter], this.vessel)==false)
                        //{
                        received_power = Math.Round(((recvEfficiency * excess2) * (percentagePower / 100)), 1);
                        //}
                    }
                    this.part.RequestResource(EChash, -received_power * Time.fixedDeltaTime);
                    received_power_ui = Convert.ToSingle(received_power);
                }
                else
                {
                    received_power_ui = 0;
                    received_power = 0;
                    for (int m = 0; m < BPNode.nodes.Count; m++)
                    {
                        string vessel2id = Convert.ToString(CorrectVesselList[counter].id);
                        if (BPNode.nodes[m].GetValue("vesselId") == vessel2id)
                        {
                            BPNode.nodes[m].SetValue("TransmittingTo", "NoVessel");
                        }
                    }
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