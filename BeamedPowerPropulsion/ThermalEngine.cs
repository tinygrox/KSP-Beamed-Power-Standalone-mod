using System;
using System.Collections.Generic;
using UnityEngine;
using BeamedPowerStandalone;

namespace BeamedPowerPropulsion
{
    // part module for beamed power thermal engines (microwave thermal engine analog)
    // it has to be applied to a part with ModuleEnginesFX
    public class ThermalEngine : PartModule
    {
        [KSPField(guiName = "Received Power", isPersistant = false, guiActive = true, guiActiveEditor = false, guiUnits = "kW")]
        public float receivedHeat;

        [KSPField(guiName = "Received Power Limiter", isPersistant = false, guiActive = true, guiActiveEditor = false, guiUnits = "%"), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Flight)]
        public float powerLimiter;

        [KSPField(guiName = "Receiver Status", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public string state;

        [KSPField(guiName = "Core Temperature", groupName = "HeatInfo", groupDisplayName = "Thermal Receiver Heat Info", groupStartCollapsed = false, guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "K/1000K")]
        public float coreTemp;

        [KSPField(guiName = "Skin Temperature", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "K/1200K")]
        public float skinTemp;

        [KSPField(guiName = "Waste Heat", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "kW")]
        public float wasteHeat;

        [KSPField(guiName = "Propellant", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        public string propellantNameUI;

        // parameters set in part.cfg file
        [KSPField(isPersistant = false)]
        public float recvDiameter;

        [KSPField(isPersistant = false)]
        public float recvEfficiency;        // efficiency of built-in thermal receiver

        [KSPField(isPersistant = false)]
        public float thermalEfficiency;     // meant to be engine heat exchanger's efficiency in tranferring heat to propellant

        [KSPField(isPersistant = false)]
        public float thrustMult = 1f;

        [KSPField(isPersistant = false)]
        public bool IsJetEngine = false;

        //declaring frequently used variables
        int initFrames; ModuleEnginesFX engine;
        ModuleCoreHeat coreHeat; ReceivedPower receiver;

        public void Start()
        {
            initFrames = 0;
            try
            {
                engine = this.part.Modules.GetModule<ModuleEnginesFX>();
            }
            catch
            {
                Debug.LogError(("BeamedPowerPropulsion.ThermalEngine : ModuleEnginesFX not found on part-" + this.part.partName));
            }
            receiver = new ReceivedPower();
            engine.throttleResponseRate /= 5;
            SetHeatParams();
        }

        private void SetHeatParams()
        {
            this.part.AddModule("ModuleCoreHeat");
            coreHeat = this.part.Modules.GetModule<ModuleCoreHeat>();
            coreHeat.CoreTempGoal = 1400d;  // placeholder value, there is no optimum temperature
            coreHeat.CoolantTransferMultiplier *= 2d;
            coreHeat.radiatorCoolingFactor *= 1.5d;
        }

        // adding part info to part description tab in editor
        public string GetModuleTitle()
        {
            return "ThermalEngine";
        }
        public override string GetModuleDisplayName()
        {
            return "Thermal Engine";
        }
        public override string GetInfo()
        {
            return ("Receiver Diameter: " + Convert.ToString(recvDiameter) + "m" + "\n"
                + "Receiver Efficiency: " + Convert.ToString(recvEfficiency * 100) + "%" + "\n"
                + "Thermal Efficiency: " + Convert.ToString(thermalEfficiency * 100) + "%" + "\n" + ""+ "\n"
                + "Max Core Temp: 1000K" + "\n"
                + "Max Skin Temp: 1200K" + "\n" + "" + "\n"
                + "This engine will shutdown past these temperatures.");
        }

        // adds heat to receiver core, not engine core
        // which is why the temp limits are so low
        private void AddHeatToReceiverCore()
        {
            coreTemp = (float)(Math.Round(coreHeat.CoreTemperature, 1));
            skinTemp = (float)(Math.Round(this.part.skinTemperature, 1));
            
            if (coreTemp > 1000f | skinTemp > 1200f)
            {
                state = "Exceeded Temperature Limit";
                engine.Shutdown(); powerLimiter = 0;
            }
            if (state == "Exceeded Temperature Limit" & (coreTemp > 800f | skinTemp > 1000f))
            {
                engine.Shutdown(); powerLimiter = 0;
            }
            else if (coreTemp < 800f & skinTemp < 1000f)
            {
                state = "Operational";
            }
            double heatModifier = (double)HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().PercentHeat / 100;
            double heatExcess = (receivedHeat * (1f / recvEfficiency - thermalEfficiency)) * heatModifier;

            wasteHeat = (float)Math.Round(heatExcess, 1);
            coreHeat.AddEnergyToCore(heatExcess * 0.5 * Time.fixedDeltaTime);  // first converted to kJ
            this.part.AddSkinThermalFlux(heatExcess * 0.5);     // waste heat from receiver + waste heat from engine
        }

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
                    AddHeatToReceiverCore();
                }
                
                // received power code
                receiver.Spherical(this.part, true, powerLimiter, recvDiameter, recvEfficiency, false, false, state,
                    out state, out double recvPower);

                this.part.GetConnectedResourceTotals(engine.propellants[0].id, out double amount, out _);
                if (amount <= 0.5f)
                {
                    recvPower = 0f;
                    state = "Engine has been turned off";
                }
                receivedHeat = (float)(Math.Round(recvPower, 1));

                // code related to engine module
                float currentisp = engine.realIsp;
                float ApproxTemp = (IsJetEngine) ? 1100f : 2400f;

                propellantNameUI = engine.propellants[0].displayName; string propellantname = engine.propellants[0].name;
                float shc = PartResourceLibrary.Instance.GetDefinition(propellantname).specificHeatCapacity * 1.5f / 1000f;    // in kJ kg^-1 K^-1

                // calculate thrust based on power received
                float Thrust = (receivedHeat * thermalEfficiency * 0.7f / (shc * ApproxTemp)) * 9.8f * currentisp;   // in kN
                Thrust = (Thrust < 5f) ? 0f : Thrust; // minimum thrust (min received power) below this there isnt enough heat to reach optimum temperature
                float percentThrust = Thrust * thrustMult / engine.maxThrust;
                engine.thrustPercentage = Mathf.Clamp((float)Math.Round(percentThrust * 100, 2), 0f, 100f);
            }
        }

        // thrust calculator
        [KSPField(guiName = "Distance", groupName = "calculator4", groupDisplayName = "Thrust Calculator", groupStartCollapsed = true, guiUnits = "Mm", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 10000000, stepIncrement = 0.001f, scene = UI_Scene.Editor)]
        public float dist_ui;

        [KSPField(guiName = "Source Dish Diameter", groupName = "calculator4", guiUnits = "m", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float dish_dia_ui;

        [KSPField(guiName = "Source Efficiency", groupName = "calculator4", guiActive = false, guiActiveEditor = true, guiUnits = "%"), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float efficiency;

        [KSPField(guiName = "Power Beamed", groupName = "calculator4", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 100000, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float beamedPower;

        [KSPField(guiName = "Engine Isp", groupName = "calculator4", guiUnits = "s", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 1500, stepIncrement = 5, scene = UI_Scene.Editor)]
        public float Isp;

        [KSPField(guiName = "Thrust", groupName = "calculator4", guiUnits = "kN", guiActive = false, guiActiveEditor = true)]
        public float Thrust;

        [KSPField(guiName = "Beamed Wavelength", groupName = "calculator4", guiActiveEditor = true, guiActive = false)]
        public string wavelength_ui = "Long";

        [KSPEvent(guiName = "Toggle Wavelength", guiActive = false, guiActiveEditor = true, groupName = "calculator4")]
        public void ToggleWavelength()
        {
            wavelength_ui = (wavelength_ui == "Long") ? "Short" : "Long";
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                engine.thrustPercentage = 100f;
                float wavelength_num = (float)((wavelength_ui == "Long") ? Math.Pow(10, -3) : 5 * Math.Pow(10, -8));
                float spotSize = (float)(1.44 * wavelength_num * dist_ui * 1000000 / dish_dia_ui);
                double powerReceived = (spotSize > recvDiameter) ?
                    recvDiameter / spotSize * beamedPower * (efficiency / 100) * recvEfficiency : beamedPower * (efficiency / 100) * recvEfficiency;

                string propellantname = engine.propellants[0].name;
                float shc = PartResourceLibrary.Instance.GetDefinition(propellantname).specificHeatCapacity * 1.5f / 1000f;    // in kJ kg^-1 K^-1
                Thrust = Mathf.Clamp((float)Math.Round(((powerReceived * thermalEfficiency * 0.7f) / (shc * 2400f)) * 9.8f * Isp, 1), 
                    0f, engine.GetMaxThrust());
                Thrust = (Thrust < 5f) ? 0f : Thrust * thrustMult;
            }
        }
    }
}
