using System;
using System.Collections.Generic;
using UnityEngine;
using CommNet.Occluders;

namespace BeamedPowerStandalone
{
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

        //[KSPField(guiName = "Test", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        //public string TestString;

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
        // they're also displayed in the editor in part right click menu
        [KSPField(guiName = "Receiver Dish Diameter", isPersistant = false, guiActive = false, guiActiveEditor = true, guiUnits = "m")]
        public float recvDiameter;

        [KSPField(guiName = "Receiver Efficiency", isPersistant = false, guiActive = false, guiActiveEditor = true)]
        public float recvEfficiency;

        // declaring frequently used variables
        public double excess2; public double constant2;
        public Vector3d source; public Vector3d dest; bool c2;
        public List<double> excessList; public List<double> constantList;
        public Vessel CorrectVessel; public double frames; public Part CorrectPart;

        public void Start()
        {
            counter = 0;
            frames = 599;
            CorrectVesselList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
        }

        // setting action group to toggle functionality of part module
        [KSPAction(guiName = "Toggle Power Receiver")]
        public void ToggleBPReceiver(KSPActionParam param2)
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

        // get if receiver is occluded from source by a celestial body
        //public bool CheckifOccluded()
        //{
        //    source = CorrectVessel.GetWorldPos3D();
        //    dest = this.vessel.GetWorldPos3D();
        //    invRotation
        //    double radiusx = source.x;
        //    double radiusy = source.y;
        //    double radiusz = source.z;
        //    OccluderHorizonCulling objOccluders = new OccluderHorizonCulling(transform, radiusx, radiusy, radiusz);
        //    bool occluded = objOccluders.Raycast(source, dest);
        //    return occluded;
        //}

        public void FixedUpdate()
        {
            frames += 1;
            if (frames==600)
            {
                if (CorrectVesselList.Count > 0)
                {
                    excessList.Clear();
                    constantList.Clear();
                    CorrectVesselList.Clear();
                }
                for (int x = 0; x < FlightGlobals.Vessels.Count; x++)
                {
                    for (int y = 0; y < FlightGlobals.Vessels[x].Parts.Count; y++)
                    {
                        if (FlightGlobals.Vessels[x].Parts[y].Modules.Contains<WirelessSource>() == true)
                        {
                            CorrectVesselList.Add(FlightGlobals.Vessels[x]);
                            double excess1 = Convert.ToDouble(FlightGlobals.Vessels[x].Parts[y].Modules.GetModule<WirelessSource>().Fields.GetValue("excess"));
                            double constant1 = Convert.ToDouble(FlightGlobals.Vessels[x].Parts[y].Modules.GetModule<WirelessSource>().Fields.GetValue("constant"));
                            excessList.Add(excess1);
                            constantList.Add(constant1);
                            CorrectPart = FlightGlobals.Vessels[x].Parts[y];
                            break;
                        }
                    }
                }
            }
            
            string value = Convert.ToString(CorrectPart.Modules.GetModule<WirelessSource>().Fields.GetValue("TransmittingTo"));
            if (value== this.vessel.GetDisplayName() | value==" ")
            {
                c2 = true;
            }
            else
            {
                c2 = false;
            }

            if (CorrectVesselList.Count > 0 | c2==true)
            {
                CorrectVessel = CorrectVesselList[counter];
                correctVesselName = CorrectVessel.GetDisplayName();
                dest = this.vessel.GetWorldPos3D();
                if (Listening == true)
                {
                    if (Convert.ToString(CorrectPart.Modules.GetModule<WirelessSource>().Fields.GetValue("TransmittingTo"))==" ")
                    {
                        CorrectPart.Modules.GetModule<WirelessSource>().Fields.SetValue("TransmittingTo", this.vessel.GetDisplayName());
                    }
                    
                    excess2 = excessList[counter]; constant2 = constantList[counter];
                    source = CorrectVessel.GetWorldPos3D();
                    double distance = Vector3d.Distance(source, dest);
                    double spot_size = constant2 * distance;
                    int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
                    double received_power;

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
                        this.part.RequestResource(EChash, -received_power * Time.fixedDeltaTime);
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
                        this.part.RequestResource(EChash, -received_power * Time.fixedDeltaTime);
                    }
                    received_power_ui = Convert.ToSingle(received_power);
                }
            }
            if (Listening==false | CorrectVesselList.Count==0)
            {
                received_power_ui = 0;
            }
        }
    }
}