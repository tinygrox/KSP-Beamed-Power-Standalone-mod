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

        [KSPField(isPersistant = true)]
        public int counter;

        [KSPEvent(guiName = "Cycle through vessels", guiActive = true, guiActiveEditor = false)]
        public void Counter()
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

        [KSPField(guiName = "Receiving from", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        public string correctVesselName;

        // 'recv_diameter' and 'recv_efficiency' values are set in part cfg file
        [KSPField(isPersistant = false)]
        public float recvDiameter;

        [KSPField(isPersistant = false)]
        public float recvEfficiency;

        // declaring frequently used variables
        public Vector3d source; public Vector3d dest; public int frames;
        public List<double> excessList; public List<double> constantList;
        public Vessel CorrectVessel; public List<Part> CorrectPartList;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;

        // when the part module is first loaded, it initialises 'frames' which increments every frame in fixedupdate
        public void Start()
        {
            frames = 590;
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
            return "Wireless Source Directional";
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

        // Loading all vessels that have WirelessSource module, and adding them to a list to use later
        private void LoadVesselsList()
        {
            CorrectVesselList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            CorrectPartList = new List<Part>();
            frames = 0;

            for (int x = 0; x < FlightGlobals.Vessels.Count; x++)
            {
                for (int y = 0; y < FlightGlobals.Vessels[x].Parts.Count; y++)
                {
                    if (FlightGlobals.Vessels[x].Parts[y].Modules.Contains<WirelessSource>() == true)
                    {
                        CorrectVesselList.Add(FlightGlobals.Vessels[x]);
                        CorrectPartList.Add(FlightGlobals.Vessels[x].Parts[y]);
                        double excess1 = Convert.ToDouble(FlightGlobals.Vessels[x].Parts[y].Modules.GetModule<WirelessSource>().Fields.GetValue("excess"));
                        double constant1 = Convert.ToDouble(FlightGlobals.Vessels[x].Parts[y].Modules.GetModule<WirelessSource>().Fields.GetValue("constant"));
                        excessList.Add(excess1);
                        constantList.Add(constant1);
                        break;
                    }
                }
            }
        }

        // get if receiver is occluded from source by a celestial body (commented for now)
        //public bool CheckifOccluded(Vessel inputSource, Vessel inputDest)
        //{
        //    source = inputSource.GetWorldPos3D();
        //    dest = inputDest.GetWorldPos3D();
        //    CelestialBody celestialbody = new CelestialBody();
        //    OccluderHorizonCulling objOccluders = new OccluderHorizonCulling(OBJ.GetTransform(), OBJ.Radius, OBJ.Radius, OBJ.Radius);
        //    bool occluded = objOccluders.Raycast(source, dest);
        //    return occluded;
        //}


        // main block of code - runs every physics frame
        public void FixedUpdate()
        {
            frames += 1;
            if (frames==600)
            {
                LoadVesselsList();
            }

            if (CorrectVesselList.Count > 0)
            {
                double received_power;
                Part CorrectPart = CorrectPartList[counter]; bool condition2;
                string value = Convert.ToString(CorrectPart.Modules.GetModule<WirelessSource>().Fields.GetValue("TransmittingTo"));
                if (value == this.vessel.GetDisplayName() | value == " ")
                {
                    condition2 = true;
                }
                else
                {
                    condition2 = false;
                }

                CorrectVessel = CorrectVesselList[counter];
                correctVesselName = CorrectVessel.GetDisplayName();
                dest = this.vessel.GetWorldPos3D();
                if (Listening == true && condition2 == true)
                {
                    if (Convert.ToString(CorrectPart.Modules.GetModule<WirelessSource>().Fields.GetValue("TransmittingTo")) == " ")
                    {
                        CorrectPart.Modules.GetModule<WirelessSource>().Fields.SetValue("TransmittingTo", this.vessel.GetDisplayName());
                    }

                    double excess2 = excessList[counter]; double constant2 = constantList[counter];
                    source = CorrectVessel.GetWorldPos3D();
                    double distance = Vector3d.Distance(source, dest);
                    double spot_size = constant2 * distance;

                    // adding EC that has been received
                    if (recvDiameter < spot_size)
                    {
                        //if (CheckifOccluded()==true)
                        //{
                        //    received_power = 0;
                        //}
                        //else
                        //{
                        received_power = Math.Round(((recvDiameter / spot_size) * recvEfficiency * excess2 * (percentagePower / 100)), 1);
                        //}

                    }
                    else
                    {
                        //if (CheckifOccluded() == true)
                        //{
                        //    received_power = 0;
                        //}
                        //else
                        //{
                        received_power = Math.Round(((recvEfficiency * excess2) * (percentagePower / 100)), 1);
                        //}
                    }
                    this.part.RequestResource(EChash, -received_power * Time.fixedDeltaTime);
                    received_power_ui = Convert.ToSingle(received_power);
                }
                if (Listening == false)
                {
                    received_power_ui = 0;
                    received_power = 0;
                    if (Convert.ToString(CorrectPart.Modules.GetModule<WirelessSource>().Fields.GetValue("TransmittingTo")) == this.vessel.GetDisplayName())
                    {
                        CorrectPart.Modules.GetModule<WirelessSource>().Fields.SetValue("TransmittingTo", " ");
                    }
                }
            }
            else
            {
                received_power_ui = 0;
            }
        }
    }
}