using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
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

        [KSPField(guiName = "Status", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public string state;

        [KSPField(guiName = "Core Temperature", groupName = "HeatInfo", groupDisplayName = "Heat Info", groupStartCollapsed = false, guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "K/900K")]
        public float coreTemp;

        [KSPField(guiName = "Skin Temperature", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "K/1200K")]
        public float skinTemp;

        [KSPField(guiName = "Waste Heat", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "kW")]
        public float wasteHeat;

        // 'recv_diameter' and 'recv_efficiency' values are set in part cfg file
        [KSPField(isPersistant = false)]
        public float recvDiameter;

        [KSPField(isPersistant = false)]
        public float recvEfficiency;

        // declaring frequently used variables
        Vector3d source; Vector3d dest;
        int frames; double received_power; int initFrames;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        VesselFinder vesselFinder = new VesselFinder(); ModuleCoreHeat coreHeat;
        AnimationSync animation;

        List <Vessel> CorrectVesselList;
        List<double> excessList; 
        List<double> constantList;
        List<string> targetList;

        [KSPField(isPersistant = true)]
        public int counter = 0;

        public void Start()
        {
            frames = 145;
            initFrames = 0;
            CorrectVesselList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            targetList = new List<string>();
            wavelength_ui = "Long";
            animation = new AnimationSync();
            SetEngineParams();
        }

        private void SetEngineParams()
        {
            this.part.AddModule("ModuleCoreHeat");
            coreHeat = this.part.Modules.GetModule<ModuleCoreHeat>();
            coreHeat.CoreTempGoal = 1300d;  // placeholder value, there is no optimum temperature
            coreHeat.CoolantTransferMultiplier *= 3d;
            coreHeat.radiatorCoolingFactor *= 2d;
            coreHeat.HeatRadiantMultiplier *= 2d;
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
            return "WirelessReceiverDirectional";
        }
        public override string GetModuleDisplayName()
        {
            return "Beamed Power Receiver";
        }
        public override string GetInfo()
        {
            return ("Receiver Type: Directional" + "\n"
                + "Dish Diameter: " + Convert.ToString(recvDiameter) + "\n"
                + "Efficiency: " + Convert.ToString(recvEfficiency * 100) + "%" + "\n" + "" + "\n"
                + "Max Core Temp: 900K" + "\n"
                + "Max Skin Temp: 1200K" + "\n" + "" + "\n"
                + "This receiver will shutdown past these temperatures.");
        }

        // adds heat mechanics to this receiver
        private void AddHeatToCore()
        {
            coreTemp = (float)(Math.Round(coreHeat.CoreTemperature, 1));
            skinTemp = (float)(Math.Round(this.part.skinTemperature, 1));
            if (coreTemp > 900f | skinTemp > 1200f)
            {
                state = "Exceeded Temperature Limit";
                Listening = (Listening) ? false : false;
            }
            if (state == "Exceeded Temperature Limit" & (coreTemp > 700f | skinTemp > 1000f))
            {
                Listening = (Listening) ? false : false;
            }
            if (coreTemp < 700f & skinTemp < 1000f)
            {
                state = "Operational";
            }
            double heatModifier = (double)HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().PercentHeat / 100;
            double heatExcess = (1 - recvEfficiency) * (received_power / recvEfficiency) * heatModifier;
            wasteHeat = (float)Math.Round(heatExcess, 1);
            coreHeat.AddEnergyToCore(heatExcess * 0.3 * 3 * Time.fixedDeltaTime);  // first converted to kJ
            this.part.AddSkinThermalFlux(heatExcess * 0.7);     // some heat added to skin
        }

        // checks for occlusion by celestial body, uses derived class constructor BPOcclusion
        private void IsOccluded(out CelestialBody celestialBody, out bool occluded)
        {
            Transform transform2; double radiusx2; double radiusy2; double radiusz2;
            celestialBody = new CelestialBody(); occluded = false;
            for (int x = 0; x < FlightGlobals.Bodies.Count; x++)
            {
                transform2 = FlightGlobals.Bodies[x].transform;
                radiusx2 = FlightGlobals.Bodies[x].Radius;
                radiusy2 = FlightGlobals.Bodies[x].Radius;
                radiusz2 = FlightGlobals.Bodies[x].Radius;
                celestialBody = FlightGlobals.Bodies[x];
                BPOcclusion occlusion = new BPOcclusion(transform2, radiusx2, radiusy2, radiusz2, celestialBody);
                occluded = occlusion.Raycast(source, dest);
                if (occluded == true)
                {
                    break;
                }
            }
        }

        // main block of code - runs every physics frame
        public void FixedUpdate()
        {
            frames += 1;
            if (frames == 150)
            {
                vesselFinder.SourceData(out CorrectVesselList, out excessList, out constantList, out targetList, out _);
                frames = 0;
            }
            if (initFrames < 60)
            {
                initFrames += 1;
            }
            else
            {
                AddHeatToCore();
            }
            animation.SyncAnimationState(this.part);

            if (CorrectVesselList.Count > 0)
            {
                if (Listening == true & targetList[counter] == this.vessel.GetDisplayName())
                {
                    dest = this.vessel.GetWorldPos3D();
                    double excess2 = excessList[counter]; double constant2 = constantList[counter];
                    CorrectVesselName = CorrectVesselList[counter].GetDisplayName();
                    source = CorrectVesselList[counter].GetWorldPos3D();
                    double distance = Vector3d.Distance(source, dest);
                    double spotsize = constant2 * distance;

                    IsOccluded(out CelestialBody celestial, out bool isOccluded);

                    // adding EC that has been received
                    if (recvDiameter < spotsize)
                    {
                        received_power = Math.Round(((recvDiameter / spotsize) * recvEfficiency * excess2 * (percentagePower / 100)), 1);
                    }
                    else
                    {
                        received_power = Math.Round(((recvEfficiency * excess2) * (percentagePower / 100)), 1);
                    }
                    if (isOccluded == true)
                    {
                        receivedPower = 0;
                        state = "Occluded by " + celestial.GetDisplayName();
                    }

                    if (HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().BackgroundProcessing == false)
                    {
                        this.part.RequestResource(EChash, -received_power * Time.fixedDeltaTime);
                    }
                    receivedPower = Convert.ToSingle(received_power);
                }
                else
                {
                    receivedPower = 0;
                    received_power = 0;
                    CorrectVesselName = "None";
                }
            }
            else
            {
                receivedPower = 0;
                received_power = 0;
                CorrectVesselName = "None";
            }
        }

        // adds received power calculator to receivers right-click menu in editor

        [KSPField(guiName = "Distance", groupName = "calculator1", groupDisplayName = "Received Power Calculator", groupStartCollapsed = true, guiUnits = "Mm", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 20000000, stepIncrement = 0.001f, scene = UI_Scene.Editor)]
        public float dist_ui;

        [KSPField(guiName = "Source Dish Diameter", groupName = "calculator1", guiUnits = "m", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float dish_dia_ui;

        [KSPField(guiName = "Source Efficiency", groupName = "calculator1", guiActive = false, guiActiveEditor = true, guiUnits = "%"), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float efficiency;

        [KSPField(guiName = "Power Beamed", groupName = "calculator1", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 100000, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float beamedPower;

        [KSPField(guiName = "Result", groupName = "calculator1", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true)]
        public float powerReceived;

        [KSPField(guiName = "Beamed Wavelength", groupName = "calculator1", guiActiveEditor = true, guiActive = false)]
        public string wavelength_ui;

        [KSPEvent(guiName = "Toggle Wavelength", guiActive = false, guiActiveEditor = true, groupName = "calculator1")]
        public void ToggleWavelength()
        {
            wavelength_ui = (wavelength_ui == "Long") ? "Short" : "Long";
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                float wavelength_num = (float)((wavelength_ui == "Long") ? Math.Pow(10, -3) : 5 * Math.Pow(10, -8));
                float spot_size = (float)(1.44 * wavelength_num * dist_ui * 1000000 / dish_dia_ui);
                powerReceived = (spot_size > recvDiameter) ?
                    recvDiameter / spot_size * beamedPower * (efficiency/100) * recvEfficiency : beamedPower * (efficiency/100) * recvEfficiency;
                powerReceived = (float)Math.Round(powerReceived, 1);
            }
        }
    }
}