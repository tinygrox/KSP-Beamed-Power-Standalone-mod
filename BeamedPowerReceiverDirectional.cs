using System;
using System.Collections.Generic;
using UnityEngine;
using CommNet.Occluders;

namespace BeamedPowerStandalone
{
    // code relating to planetary occlusion has been commented out for now
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class WirelessReceiverDirectional : PartModule
    {
        // UI-right click menu in flight
        [KSPField(guiName = "Power Receiver", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool Listening;

        [KSPField(guiName = "Received Power Limiter", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, requireFullControl = true, scene = UI_Scene.Flight)]
        public float percentagePower;

        [KSPField(guiName = "Received Power", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s")]
        public float receivedPower;

        [KSPField(guiName = "Receiving from", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        public string CorrectVesselName;

        // 'recv_diameter' and 'recv_efficiency' values are set in part cfg file
        [KSPField(isPersistant = false)]
        public float recvDiameter;

        [KSPField(isPersistant = false)]
        public float recvEfficiency;

        // declaring frequently used variables
        Vector3d source; Vector3d dest;
        int frames; double received_power;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        TransmitterVessel transmitter = new TransmitterVessel();

        List<Vessel> CorrectVesselList;
        List<double> excessList; 
        List<double> constantList;
        List<string> targetList;

        [KSPField(isPersistant = true)]
        public int? counter;

        public void Start()
        {
            if (counter == null)
            {
                counter = 0;
            }
            frames = 145;
            CorrectVesselList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            targetList = new List<string>();
        }

        [KSPEvent(guiName = "Cycle through vessels", guiActive = true, isPersistent = false, requireFullControl = true)]
        public void VesselCounter()
        {
            counter = (counter < CorrectVesselList.Count - 1) ? counter += 1 : counter = 0;
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
                + "Receiver Type: Directional");
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
            int index = Convert.ToInt32(counter);
            if (CorrectVesselList.Count > 0)
            {
                if (Listening == true & targetList[index] == this.vessel.GetDisplayName())
                {
                    dest = this.vessel.GetWorldPos3D();
                    double excess2 = excessList[index]; double constant2 = constantList[index];
                    CorrectVesselName = CorrectVesselList[index].GetDisplayName();
                    source = CorrectVesselList[index].GetWorldPos3D();
                    double distance = Vector3d.Distance(source, dest);
                    double spotsize = constant2 * distance;

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

                    BPSettings settings = new BPSettings();
                    if (settings.BackgroundProcessing == false)
                    {
                        this.part.RequestResource(EChash, -received_power * Time.fixedDeltaTime);
                    }
                    receivedPower = Convert.ToSingle(received_power);
                }
                else
                {
                    receivedPower = 0;
                    received_power = 0;
                }
            }
            else
            {
                received_power = 0;
                receivedPower = 0;
            }
        }
    }

    // get if receiver is occluded from source by a celestial body (commented for now)
    //public class BPOcclusion : OccluderHorizonCulling
    //{
    //    public bool CheckifOccluded(Vessel inputSource, Vessel inputDest)
    //    {
    //        OccluderHorizonCulling horizonCulling = new OccluderHorizonCulling(transform, radiusXRecip, radiusYRecip, radiusZRecip);

    //        Vector3d source = inputSource.GetWorldPos3D();
    //        Vector3d dest = inputDest.GetWorldPos3D();
    //        bool occluded = horizonCulling.Raycast(source, dest);
    //        return occluded;
    //    }
    //}
}