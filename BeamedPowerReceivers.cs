using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    // part module for spherical or multi-directional receivers
    public class WirelessReceiver : PartModule
    {
        // UI-right click menu in flight
        [KSPField(guiName = "Power Receiver", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool Listening;

        [KSPField(guiName = "Received Power Limiter", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "%"), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, requireFullControl = true, scene = UI_Scene.Flight)]
        public float percentagePower;

        [KSPField(guiName = "Received Power", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s")]
        public float receivedPower;

        [KSPField(guiName = "Status", isPersistant = false, guiActive = true, guiActiveEditor = false)]
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
        int initFrames; AnimationSync animation;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        ModuleCoreHeat coreHeat; ReceiverPowerCalc calc; ReceivedPower receiver;
        
        public void Start()
        {
            initFrames = 0;
            this.part.AddModule("ReceiverPowerCalc");
            calc = this.part.Modules.GetModule<ReceiverPowerCalc>();
            animation = new AnimationSync();
            receiver = new ReceivedPower();
            SetHeatParams();
        }

        private void SetHeatParams()
        {
            this.part.AddModule("ModuleCoreHeat");
            coreHeat = this.part.Modules.GetModule<ModuleCoreHeat>();
            coreHeat.CoreTempGoal = 1300d;  // placeholder value, there is no optimum temperature
            coreHeat.CoolantTransferMultiplier *= 3d;
            coreHeat.radiatorCoolingFactor *= 2d;
            coreHeat.HeatRadiantMultiplier *= 2d;
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
            return "WirelessReceiver";
        }
        public override string GetModuleDisplayName()
        {
            return "Beamed Power Receiver";
        }
        public override string GetInfo()
        {
            return ("Receiver Type: Sphere" + "\n"
                + "Dish Diameter: " + Convert.ToString(recvDiameter) + "m" + "\n"
                + "Efficiency: " + Convert.ToString(recvEfficiency * 100) + "%" + "\n" + "" + "\n"
                + "Max Core Temp: 900K" + "\n"
                + "Max Skin Temp: 1200K" + "\n" + "" + "\n"
                + "This receiver will shutdown past these temperatures.");
        }

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
            double heatExcess = (1 - recvEfficiency) * (receivedPower / recvEfficiency) * heatModifier;
            wasteHeat = (float)Math.Round(heatExcess, 1);
            coreHeat.AddEnergyToCore(heatExcess * 0.3 * Time.fixedDeltaTime);  // first converted to kJ
            this.part.AddSkinThermalFlux(heatExcess * 0.7);     // some heat added to skin
        }

        // main block of code - runs every physics frame
        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (initFrames < 60)
                {
                    initFrames += 1;
                }
                else
                {
                    AddHeatToCore();
                }
                animation.SyncAnimationState(this.part);

                receiver.Spherical(this.part, Listening, percentagePower, recvDiameter, recvEfficiency, false,
                    false, state, out state, out double recvPower);

                receivedPower = (float)Math.Round(recvPower, 1);

                if (HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().BackgroundProcessing == false)
                {
                    this.part.RequestResource(EChash, -(double)receivedPower * Time.fixedDeltaTime);
                }
            }
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                calc.CalculatePower(recvDiameter, recvEfficiency);
            }
        }
    }


    // part module for directional receivers- can receive from only one source at a time
    public class WirelessReceiverDirectional : PartModule
    {
        // UI-right click menu in flight
        [KSPField(guiName = "Power Receiver", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool Listening;

        [KSPField(guiName = "Received Power Limiter", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "%"), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, requireFullControl = true, scene = UI_Scene.Flight)]
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
        int initFrames;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        ModuleCoreHeat coreHeat; AnimationSync animation;
        ReceiverPowerCalc calc; ReceivedPower receiver;

        [KSPField(isPersistant = true)]
        public int counter;

        public void Start()
        {
            initFrames = 0;
            this.part.AddModule("ReceiverPowerCalc");
            calc = this.part.Modules.GetModule<ReceiverPowerCalc>();
            animation = new AnimationSync();
            receiver = new ReceivedPower();
            SetHeatParams();
        }

        private void SetHeatParams()
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
            counter += 1;
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
                + "Dish Diameter: " + Convert.ToString(recvDiameter) + "m" + "\n"
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
            double heatExcess = (1 - recvEfficiency) * (receivedPower / recvEfficiency) * heatModifier;
            wasteHeat = (float)Math.Round(heatExcess, 1);
            coreHeat.AddEnergyToCore(heatExcess * 0.3 * Time.fixedDeltaTime);  // first converted to kJ
            this.part.AddSkinThermalFlux(heatExcess * 0.7);     // some heat added to skin
        }

        // main block of code - runs every physics frame
        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (initFrames < 60)
                {
                    initFrames += 1;
                }
                else
                {
                    AddHeatToCore();
                }
                animation.SyncAnimationState(this.part);

                receiver.Directional(this.part, counter, Listening, percentagePower, recvDiameter, recvEfficiency,
                    false, false, state, out state, out CorrectVesselName, out double recvPower, out counter);

                receivedPower = (float)Math.Round(recvPower, 1);

                if (HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().BackgroundProcessing == false)
                {
                    this.part.RequestResource(EChash, -(double)receivedPower * Time.fixedDeltaTime);
                }
            }
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                calc.CalculatePower(recvDiameter, recvEfficiency);
            }
        }
    }
}
 